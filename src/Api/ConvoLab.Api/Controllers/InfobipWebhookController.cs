using System.Text;
using System.Text.Json;
using ConvoLab.Api.Security;
using ConvoLab.Application.Omnichannel;
using ConvoLab.Domain.Omnichannel;
using ConvoLab.Domain.WorkspaceIdentity;
using ConvoLab.Infrastructure.Analytics;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/connectors/infobip")]
public sealed class InfobipWebhookController : ControllerBase
{
    private readonly IOmnichannelService _omnichannelService;
    private readonly ApplicationDbContext _db;
    private readonly WorkspaceRequestContext _runtime;
    private readonly IConfiguration _config;
    private readonly ILogger<InfobipWebhookController> _logger;

    private static readonly TimeSpan ProcessedMessageCacheDuration = TimeSpan.FromMinutes(30);
    private readonly IMemoryCache _processedMessagesCache;

    public InfobipWebhookController(
        IOmnichannelService omnichannelService,
        ApplicationDbContext db,
        WorkspaceRequestContext runtime,
        IConfiguration config,
        IMemoryCache processedMessagesCache,
        ILogger<InfobipWebhookController> logger)
    {
        _omnichannelService = omnichannelService;
        _db = db;
        _runtime = runtime;
        _config = config;
        _processedMessagesCache = processedMessagesCache;
        _logger = logger;
    }

    /// <summary>
    /// Inbound WhatsApp message webhook conforming to the Infobip WhatsApp Webhook API payload.
    /// Infobip sends POST payloads structured under { results: [ { from, to, message: { text } } ] }
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received Infobip webhook event.");

        // 1. Strict Webhook Authentication (Cryptographic HMAC-SHA256 or shared provider secret)
        Request.EnableBuffering();
        Request.Body.Position = 0;
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        var expectedSecret = _config["Connectors:Infobip:WebhookSecret"]
            ?? _config["Infobip:WebhookSecret"]
            ?? string.Empty;

        if (!InfobipWebhookSecurity.VerifyWebhookRequest(Request, rawBody, expectedSecret))
        {
            _logger.LogWarning("Infobip webhook rejected: signature or shared secret verification failed.");
            return Unauthorized(new { error = "Unauthorized: Invalid or missing webhook signature or secret." });
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawBody);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Infobip webhook JSON payload.");
            return BadRequest(new { error = "Invalid JSON payload." });
        }

        using (doc)
        {
            var payload = doc.RootElement;
            if (!payload.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            {
                return BadRequest(new { error = "Invalid Infobip webhook payload format: 'results' array required." });
            }

            var responses = new List<object>();

            foreach (var item in results.EnumerateArray())
            {
                var (response, tenantError) = await ProcessWebhookItemAsync(item, cancellationToken);
                if (tenantError is not null)
                {
                    return BadRequest(new { error = tenantError });
                }

                if (response is not null)
                {
                    responses.Add(response);
                }
            }

            return Ok(new
            {
                status = "PROCESSED",
                count = responses.Count,
                results = responses
            });
        }
    }

    private async Task<(object? Response, string? TenantError)> ProcessWebhookItemAsync(
        JsonElement item,
        CancellationToken cancellationToken)
    {
        var sender = item.TryGetProperty("from", out var fromEl) ? fromEl.GetString() : "unknown-sender";
        var receiver = item.TryGetProperty("to", out var toEl) ? toEl.GetString() : null;
        var rawMessageId = item.TryGetProperty("messageId", out var msgIdEl) ? msgIdEl.GetString() : null;
        var messageId = !string.IsNullOrWhiteSpace(rawMessageId) ? rawMessageId : Guid.NewGuid().ToString("N");

        var tenantContext = await ResolveTenantContextAsync(receiver, cancellationToken);
        if (tenantContext is null)
        {
            _logger.LogWarning("Rejecting Infobip webhook: recipient address '{Receiver}' is not mapped to any configured workspace connector.", receiver);
            return (null, $"Tenant resolution failed: recipient address '{receiver ?? "null"}' is not mapped to any active workspace connector.");
        }

        _runtime.OrganisationId = tenantContext.OrganisationId;
        _runtime.WorkspaceId = tenantContext.WorkspaceId;
        _runtime.EnvironmentId = tenantContext.EnvironmentId;
        _runtime.EnvironmentName = tenantContext.EnvironmentName;
        _runtime.EnvironmentType = tenantContext.EnvironmentType;

        var workspaceScope = tenantContext.WorkspaceId.ToString("N");
        var deduplicationKey = $"webhook:infobip:{workspaceScope}:{messageId}";
        if (await IsDuplicateDeliveryAsync(deduplicationKey, tenantContext, cancellationToken))
        {
            return (CreateDuplicateResponse(messageId, sender), null);
        }

        var text = ExtractMessageText(item);
        var envelope = new InboundMessageEnvelope(
            MessageId: messageId,
            Channel: "WhatsApp",
            SenderId: sender ?? "unknown",
            ReceiverId: receiver ?? "unknown",
            Text: text,
            MessageType: OmnichannelMessageType.Text,
            ChannelMetadata: new Dictionary<string, string>
            {
                ["provider"] = "Infobip",
                ["workspaceId"] = workspaceScope,
                ["rawSender"] = sender ?? ""
            },
            ReceivedAt: DateTimeOffset.UtcNow);

        var processResult = await _omnichannelService.ProcessInboundAsync(envelope, cancellationToken);
        await RecordProcessedDeliveryAsync(deduplicationKey, tenantContext, cancellationToken);
        return (new
        {
            messageId,
            sender,
            handled = processResult.Handled,
            duplicate = false,
            status = "PROCESSED",
            escalatedToHuman = processResult.EscalatedToHuman,
            reply = processResult.ReplyText,
            quickReplies = processResult.QuickReplies
        }, null);
    }

    private static string ExtractMessageText(JsonElement item)
    {
        if (item.TryGetProperty("message", out var msgObj) && msgObj.TryGetProperty("text", out var textEl))
        {
            return textEl.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private async Task<bool> IsDuplicateDeliveryAsync(
        string deduplicationKey,
        TenantResolutionResult tenantContext,
        CancellationToken cancellationToken)
    {
        if (_processedMessagesCache.TryGetValue(deduplicationKey, out _))
        {
            _logger.LogInformation(
                "Duplicate webhook delivery detected (memory cache) for key '{Key}'. Ignoring retry.",
                deduplicationKey);
            return true;
        }

        var alreadyProcessed = await _db.AnalyticsEvents.AsNoTracking()
            .AnyAsync(e => e.EventKey == deduplicationKey
                && e.WorkspaceId == tenantContext.WorkspaceId
                && e.EventType == "WebhookDeliveryProcessed"
                && e.Outcome == "Success", cancellationToken);

        if (!alreadyProcessed)
            return false;

        CacheProcessedDelivery(deduplicationKey);
        _logger.LogInformation(
            "Duplicate webhook delivery detected (database) for key '{Key}'. Ignoring retry.",
            deduplicationKey);
        return true;
    }

    private async Task RecordProcessedDeliveryAsync(
        string deduplicationKey,
        TenantResolutionResult tenantContext,
        CancellationToken cancellationToken)
    {
        // This durable success marker is deliberately written only after ProcessInboundAsync completes.
        var deliveryEvent = new AnalyticsEventRecord
        {
            Id = Guid.NewGuid(),
            EventKey = deduplicationKey,
            OrganisationId = tenantContext.OrganisationId,
            WorkspaceId = tenantContext.WorkspaceId,
            EnvironmentId = tenantContext.EnvironmentId,
            Capability = "Omnichannel",
            EventType = "WebhookDeliveryProcessed",
            Outcome = "Success",
            CostType = "Unavailable",
            SourceType = "InfobipWebhook",
            SourceId = Guid.Empty,
            ConfigurationRevision = "v1",
            CorrelationId = HttpContext.TraceIdentifier,
            OccurredAt = DateTimeOffset.UtcNow
        };

        _db.AnalyticsEvents.Add(deliveryEvent);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _db.Entry(deliveryEvent).State = EntityState.Detached;
            try
            {
                var markerExists = await _db.AnalyticsEvents.AsNoTracking()
                    .AnyAsync(e => e.EventKey == deduplicationKey
                        && e.WorkspaceId == tenantContext.WorkspaceId,
                        CancellationToken.None);

                if (markerExists)
                {
                    _logger.LogInformation(
                        "Delivery marker for '{Key}' already exists after concurrent processing.",
                        deduplicationKey);
                }
                else
                {
                    _logger.LogError(
                        ex,
                        "Inbound message was processed, but its durable delivery marker could not be saved for '{Key}'.",
                        deduplicationKey);
                }
            }
            catch (Exception lookupException)
            {
                _logger.LogError(
                    lookupException,
                    "Could not verify the durable delivery marker for successfully processed message '{Key}'.",
                    deduplicationKey);
            }
        }
        catch (Exception ex)
        {
            _db.Entry(deliveryEvent).State = EntityState.Detached;
            _logger.LogError(
                ex,
                "Inbound message was processed, but its durable delivery marker could not be saved for '{Key}'.",
                deduplicationKey);
        }
        finally
        {
            // The process call has succeeded, so the bounded retry guard may now be populated.
            CacheProcessedDelivery(deduplicationKey);
        }
    }

    private void CacheProcessedDelivery(string deduplicationKey)
    {
        _processedMessagesCache.Set(deduplicationKey, true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ProcessedMessageCacheDuration,
            Size = 1
        });
    }

    private static object CreateDuplicateResponse(string messageId, string? sender) => new
    {
        messageId,
        sender,
        handled = true,
        duplicate = true,
        status = "DUPLICATE_IGNORED",
        escalatedToHuman = false,
        reply = (string?)null,
        quickReplies = Array.Empty<string>()
    };

    /// <summary>
    /// Health probe for the Infobip connector.
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health()
    {
        return Ok(new { status = "Healthy", connector = "Infobip WhatsApp", timestamp = DateTimeOffset.UtcNow });
    }

    private async Task<TenantResolutionResult?> ResolveTenantContextAsync(string? recipientAddress, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recipientAddress))
            return null;

        var normalizedTo = NormalizeRecipient(recipientAddress);

        var fromSettings = await ResolveFromSettingValuesAsync(normalizedTo, ct);
        if (fromSettings is not null) return fromSettings;

        var fromMappings = await ResolveFromConfigMappingsAsync(normalizedTo, ct);
        if (fromMappings is not null) return fromMappings;

        return await ResolveFromConfiguredRecipientAsync(normalizedTo, ct);
    }

    private async Task<TenantResolutionResult?> ResolveFromSettingValuesAsync(string normalizedTo, CancellationToken ct)
    {
        var setting = await _db.SettingValues.AsNoTracking()
            .Where(s => s.WorkspaceId.HasValue &&
                (s.DefinitionKey == "connector.infobip.phone_number" ||
                 s.DefinitionKey == "connector.infobip.recipient_number" ||
                 s.DefinitionKey == "channel.whatsapp.phone_number" ||
                 s.DefinitionKey == "channel.whatsapp.recipient_number"))
            .FirstOrDefaultAsync(s => s.ValueJson.Contains(normalizedTo), ct);

        if (setting?.WorkspaceId == null) return null;

        var env = await _db.RuntimeEnvironments.AsNoTracking()
            .FirstOrDefaultAsync(e => e.WorkspaceId == setting.WorkspaceId.Value &&
                                      e.Status == "Active" &&
                                      (setting.EnvironmentId.HasValue && e.Id == setting.EnvironmentId.Value || e.IsDefault), ct);

        return env != null
            ? new TenantResolutionResult(setting.WorkspaceId.Value, env.OrganisationId, env.Id, env.Name, env.EnvironmentType)
            : null;
    }

    private async Task<TenantResolutionResult?> ResolveFromConfigMappingsAsync(string normalizedTo, CancellationToken ct)
    {
        var mappedWorkspaceIdStr = _config[$"Connectors:Infobip:Mappings:{normalizedTo}:WorkspaceId"];
        if (!Guid.TryParse(mappedWorkspaceIdStr, out var mappedWorkspaceId)) return null;

        var env = await _db.RuntimeEnvironments.AsNoTracking()
            .FirstOrDefaultAsync(e => e.WorkspaceId == mappedWorkspaceId && e.Status == "Active" && e.IsDefault, ct);

        return env != null
            ? new TenantResolutionResult(mappedWorkspaceId, env.OrganisationId, env.Id, env.Name, env.EnvironmentType)
            : null;
    }

    private async Task<TenantResolutionResult?> ResolveFromConfiguredRecipientAsync(string normalizedTo, CancellationToken ct)
    {
        var configuredRecipient = _config["Connectors:Infobip:RecipientNumber"] ?? _config["Infobip:RecipientNumber"];
        if (string.IsNullOrWhiteSpace(configuredRecipient) || NormalizeRecipient(configuredRecipient) != normalizedTo)
            return null;

        var configuredWsStr = _config["Connectors:Infobip:WorkspaceId"];
        var wsId = Guid.TryParse(configuredWsStr, out var parsedWsId) ? parsedWsId : WorkspaceIdentityDefaults.WorkspaceId;

        var env = await _db.RuntimeEnvironments.AsNoTracking()
            .FirstOrDefaultAsync(e => e.WorkspaceId == wsId && e.Status == "Active" && e.IsDefault, ct);

        return env != null
            ? new TenantResolutionResult(wsId, env.OrganisationId, env.Id, env.Name, env.EnvironmentType)
            : null;
    }

    private static string NormalizeRecipient(string recipient)
    {
        return recipient.Trim().Trim('"', '\'').TrimStart('+').Replace(" ", "").Replace("-", "");
    }

    private sealed record TenantResolutionResult(
        Guid WorkspaceId,
        Guid OrganisationId,
        Guid EnvironmentId,
        string EnvironmentName,
        string EnvironmentType);
}
