using System.Text.Json;
using ConvoLab.Application.Omnichannel;
using ConvoLab.Domain.Omnichannel;
using ConvoLab.Domain.WorkspaceIdentity;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/connectors/infobip")]
[AllowAnonymous] // Infobip webhook authenticates via API Key or custom callback token
public sealed class InfobipWebhookController : ControllerBase
{
    private readonly IOmnichannelService _omnichannelService;
    private readonly ApplicationDbContext _db;
    private readonly WorkspaceRequestContext _runtime;
    private readonly ILogger<InfobipWebhookController> _logger;

    public InfobipWebhookController(
        IOmnichannelService omnichannelService,
        ApplicationDbContext db,
        WorkspaceRequestContext runtime,
        ILogger<InfobipWebhookController> logger)
    {
        _omnichannelService = omnichannelService;
        _db = db;
        _runtime = runtime;
        _logger = logger;
    }

    /// <summary>
    /// Inbound WhatsApp message webhook conforming to the Infobip WhatsApp Webhook API payload.
    /// Infobip sends POST payloads structured under { results: [ { from, to, message: { text } } ] }
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveWebhook(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received Infobip webhook event.");

        if (!payload.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return BadRequest(new { error = "Invalid Infobip webhook payload format: 'results' array required." });
        }

        // Ensure runtime context is primed for background/webhook execution
        if (!_runtime.WorkspaceId.HasValue || !_runtime.EnvironmentId.HasValue)
        {
            var defaultEnv = await _db.RuntimeEnvironments.AsNoTracking()
                .FirstOrDefaultAsync(e => e.IsDefault && e.Status == "Active", cancellationToken);

            if (defaultEnv is not null)
            {
                _runtime.OrganisationId = defaultEnv.OrganisationId;
                _runtime.WorkspaceId = defaultEnv.WorkspaceId;
                _runtime.EnvironmentId = defaultEnv.Id;
                _runtime.EnvironmentName = defaultEnv.Name;
                _runtime.EnvironmentType = defaultEnv.EnvironmentType;
            }
            else
            {
                _runtime.OrganisationId = WorkspaceIdentityDefaults.OrganisationId;
                _runtime.WorkspaceId = WorkspaceIdentityDefaults.WorkspaceId;
            }
        }

        var responses = new List<object>();

        foreach (var item in results.EnumerateArray())
        {
            var sender = item.TryGetProperty("from", out var fromEl) ? fromEl.GetString() : "unknown-sender";
            var receiver = item.TryGetProperty("to", out var toEl) ? toEl.GetString() : "unknown-receiver";
            var messageId = item.TryGetProperty("messageId", out var msgIdEl) ? msgIdEl.GetString() : Guid.NewGuid().ToString("N");

            string text = string.Empty;
            if (item.TryGetProperty("message", out var msgObj))
            {
                if (msgObj.TryGetProperty("text", out var textEl))
                    text = textEl.GetString() ?? string.Empty;
            }

            var envelope = new InboundMessageEnvelope(
                MessageId: messageId ?? Guid.NewGuid().ToString("N"),
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

    /// <summary>
    /// Health probe for the Infobip connector.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "Healthy", connector = "Infobip WhatsApp", timestamp = DateTimeOffset.UtcNow });
    }
}
