using System.Collections.Concurrent;
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

    private static readonly ConcurrentDictionary<string, DateTimeOffset> ProcessedMessagesCache = new();

    public InfobipWebhookController(
        IOmnichannelService omnichannelService,
        ApplicationDbContext db,
        WorkspaceRequestContext runtime,
        IConfiguration config,
        ILogger<InfobipWebhookController> logger)
    {
        _omnichannelService = omnichannelService;
        _db = db;
        _runtime = runtime;
        _config = config;
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
            ?? InfobipWebhookSecurity.DefaultSecret;

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
                var sender = item.TryGetProperty("from", out var fromEl) ? fromEl.GetString() : "unknown-sender";
                var receiver = item.TryGetProperty("to", out var toEl) ? toEl.GetString() : null;
                var rawMessageId = item.TryGetProperty("messageId", out var msgIdEl) ? msgIdEl.GetString() : null;
                var messageId = !string.IsNullOrWhiteSpace(rawMessageId) ? rawMessageId : Guid.NewGuid().ToString("N");

                // 2. Strict Tenant Isolation: Resolve target workspace strictly from verified recipient address mapping
                var tenantContext = await ResolveTenantContextAsync(receiver, cancellationToken);
                if (tenantContext is null)
                {
                    _logger.LogWarning("Rejecting Infobip webhook: recipient address '{Receiver}' is not mapped to any configured workspace connector.", receiver);
                    return BadRequest(new
                    {
                        error = $"Tenant resolution failed: recipient address '{receiver ?? "null"}' is not mapped to any active workspace connector."
                    });
                }

                // Prime the ambient runtime context strictly from the resolved tenant
                _runtime.OrganisationId = tenantContext.OrganisationId;
                _runtime.WorkspaceId = tenantContext.WorkspaceId;
                _runtime.EnvironmentId = tenantContext.EnvironmentId;
                _runtime.EnvironmentName = tenantContext.EnvironmentName;
                _runtime.EnvironmentType = tenantContext.EnvironmentType;

                // 3. Webhook Delivery Deduplication
                var deduplicationKey = $"webhook:infobip:{messageId}";

                if (ProcessedMessagesCache.TryGetValue(deduplicationKey, out _))
                {
                    _logger.LogInformation("Duplicate webhook delivery detected (in-memory) for messageId '{MessageId}'. Ignoring retry.", messageId);
                    responses.Add(new
                    {
                        messageId,
                        sender,
                        handled = true,
                        duplicate = true,
                        status = "DUPLICATE_IGNORED",
                        escalatedToHuman = false,
                        reply = (string?)null,
                        quickReplies = Array.Empty<string>()
                    });
                    continue;
                }

                var alreadyProcessed = await _db.AnalyticsEvents.AsNoTracking()
                    .AnyAsync(e => e.EventKey == deduplicationKey, cancellationToken);

                if (alreadyProcessed)
                {
                    ProcessedMessagesCache.TryAdd(deduplicationKey, DateTimeOffset.UtcNow);
                    _logger.LogInformation("Duplicate webhook delivery detected (database) for messageId '{MessageId}'. Ignoring retry.", messageId);
                    responses.Add(new
                    {
                        messageId,
                        sender,
                        handled = true,
                        duplicate = true,
                        status = "DUPLICATE_IGNORED",
                        escalatedToHuman = false,
                        reply = (string?)null,
                        quickReplies = Array.Empty<string>()
                    });
                    continue;
                }

                // Persist processed messageId using unique constraint on EventKey
                var deduplicationEvent = new AnalyticsEventRecord
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

                _db.AnalyticsEvents.Add(deduplicationEvent);

                try
                {
                    await _db.SaveChangesAsync(cancellationToken);
                    ProcessedMessagesCache.TryAdd(deduplicationKey, DateTimeOffset.UtcNow);
                }
                catch (DbUpdateException)
                {
                    _logger.LogWarning("Concurrent duplicate delivery caught by unique constraint for messageId '{MessageId}'.", messageId);
                    ProcessedMessagesCache.TryAdd(deduplicationKey, DateTimeOffset.UtcNow);
                    responses.Add(new
                    {
                        messageId,
                        sender,
                        handled = true,
                        duplicate = true,
                        status = "DUPLICATE_IGNORED",
                        escalatedToHuman = false,
                        reply = (string?)null,
                        quickReplies = Array.Empty<string>()
                    });
                    continue;
                }

                string text = string.Empty;
                if (item.TryGetProperty("message", out var msgObj))
                {
                    if (msgObj.TryGetProperty("text", out var textEl))
                        text = textEl.GetString() ?? string.Empty;
                }

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
                        ["rawSender"] = sender ?? ""
                    },
                    ReceivedAt: DateTimeOffset.UtcNow);

                var processResult = await _omnichannelService.ProcessInboundAsync(envelope, cancellationToken);
                responses.Add(new
                {
                    messageId,
                    sender,
                    handled = processResult.Handled,
                    duplicate = false,
                    status = "PROCESSED",
                    escalatedToHuman = processResult.EscalatedToHuman,
                    reply = processResult.ReplyText,
                    quickReplies = processResult.QuickReplies
                });
            }

            return Ok(new
            {
                status = "PROCESSED",
                count = responses.Count,
                results = responses
            });
        }
    }

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

        // 1. Resolve from SettingValues (explicit phone number connector configuration)
        var setting = await _db.SettingValues.AsNoTracking()
            .Where(s => s.WorkspaceId.HasValue &&
                (s.DefinitionKey == "connector.infobip.phone_number" ||
                 s.DefinitionKey == "connector.infobip.recipient_number" ||
                 s.DefinitionKey == "channel.whatsapp.phone_number" ||
                 s.DefinitionKey == "channel.whatsapp.recipient_number"))
            .FirstOrDefaultAsync(s => s.ValueJson.Contains(normalizedTo), ct);

        if (setting?.WorkspaceId != null)
        {
            var env = await _db.RuntimeEnvironments.AsNoTracking()
                .FirstOrDefaultAsync(e => e.WorkspaceId == setting.WorkspaceId.Value &&
                                          e.Status == "Active" &&
                                          (setting.EnvironmentId.HasValue && e.Id == setting.EnvironmentId.Value || e.IsDefault), ct);

            if (env != null)
            {
                return new TenantResolutionResult(setting.WorkspaceId.Value, env.OrganisationId, env.Id, env.Name, env.EnvironmentType);
            }
        }

        // 2. Resolve from configuration mappings (Connectors:Infobip:Mappings:<to>:WorkspaceId)
        var mappedWorkspaceIdStr = _config[$"Connectors:Infobip:Mappings:{normalizedTo}:WorkspaceId"];
        if (Guid.TryParse(mappedWorkspaceIdStr, out var mappedWorkspaceId))
        {
            var env = await _db.RuntimeEnvironments.AsNoTracking()
                .FirstOrDefaultAsync(e => e.WorkspaceId == mappedWorkspaceId && e.Status == "Active" && e.IsDefault, ct);

            if (env != null)
            {
                return new TenantResolutionResult(mappedWorkspaceId, env.OrganisationId, env.Id, env.Name, env.EnvironmentType);
            }
        }

        // 3. Resolve from configured recipient number matching normalized recipient
        var configuredRecipient = _config["Connectors:Infobip:RecipientNumber"] ?? _config["Infobip:RecipientNumber"];
        if (!string.IsNullOrWhiteSpace(configuredRecipient) &&
            NormalizeRecipient(configuredRecipient) == normalizedTo)
        {
            var configuredWsStr = _config["Connectors:Infobip:WorkspaceId"];
            var wsId = Guid.TryParse(configuredWsStr, out var parsedWsId) ? parsedWsId : WorkspaceIdentityDefaults.WorkspaceId;

            var env = await _db.RuntimeEnvironments.AsNoTracking()
                .FirstOrDefaultAsync(e => e.WorkspaceId == wsId && e.Status == "Active" && e.IsDefault, ct);

            if (env != null)
            {
                return new TenantResolutionResult(wsId, env.OrganisationId, env.Id, env.Name, env.EnvironmentType);
            }
        }

        // Strict tenant isolation: never fall back to global default environment across tenants
        return null;
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
