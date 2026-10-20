using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConvoLab.Application.Omnichannel;
using ConvoLab.Application.Settings;
using ConvoLab.Domain.Omnichannel;
using Microsoft.Extensions.Logging;

namespace ConvoLab.Infrastructure.Omnichannel;

public sealed class InfobipOutboundAdapter : IInfobipOutboundAdapter
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecretStore _secretStore;
    private readonly ILogger<InfobipOutboundAdapter> _logger;

    public InfobipOutboundAdapter(
        IHttpClientFactory httpClientFactory,
        ISecretStore secretStore,
        ILogger<InfobipOutboundAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _secretStore = secretStore;
        _logger = logger;
    }

    public Task<InfobipOutboundResult> SendWhatsAppTextMessageAsync(
        OutboundMessageEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        return SendPayloadAsync("/whatsapp/1/message/text", (senderId) => new
        {
            from = senderId,
            to = envelope.RecipientId,
            messageId = Guid.NewGuid().ToString(),
            content = new { text = envelope.Text }
        }, cancellationToken);
    }

    public Task<InfobipOutboundResult> SendWhatsAppButtonsMessageAsync(
        OutboundMessageEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        var options = (envelope.InteractiveOptions == null || envelope.InteractiveOptions.Count == 0)
            ? new[] { "OK" }
            : envelope.InteractiveOptions;

        var buttons = options.Take(3).Select((opt, idx) => new
        {
            type = "REPLY",
            id = $"btn_{idx + 1}_{Guid.NewGuid():N}"[..Math.Min(32, 256)],
            title = opt.Length > 20 ? opt[..20] : opt
        }).ToArray();

        return SendPayloadAsync("/whatsapp/1/message/interactive/buttons", (senderId) => new
        {
            from = senderId,
            to = envelope.RecipientId,
            messageId = Guid.NewGuid().ToString(),
            content = new
            {
                body = new { text = string.IsNullOrWhiteSpace(envelope.Text) ? "Please choose an option:" : envelope.Text },
                action = new { buttons }
            }
        }, cancellationToken);
    }

    public Task<InfobipOutboundResult> SendWhatsAppListMessageAsync(
        OutboundMessageEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        var options = (envelope.InteractiveOptions == null || envelope.InteractiveOptions.Count == 0)
            ? new[] { "Option 1" }
            : envelope.InteractiveOptions;

        var sectionTitle = envelope.Metadata?.GetValueOrDefault("sectionTitle") ?? "Options";
        var actionTitle = envelope.Metadata?.GetValueOrDefault("actionTitle") ?? "Select";
        if (actionTitle.Length > 20) actionTitle = actionTitle[..20];
        if (sectionTitle.Length > 24) sectionTitle = sectionTitle[..24];

        var rows = options.Take(10).Select((opt, idx) => new
        {
            id = $"row_{idx + 1}_{Guid.NewGuid():N}"[..Math.Min(32, 200)],
            title = opt.Length > 24 ? opt[..24] : opt,
            description = string.Empty
        }).ToArray();

        return SendPayloadAsync("/whatsapp/1/message/interactive/list", (senderId) => new
        {
            from = senderId,
            to = envelope.RecipientId,
            messageId = Guid.NewGuid().ToString(),
            content = new
            {
                body = new { text = string.IsNullOrWhiteSpace(envelope.Text) ? "Please select from the list below:" : envelope.Text },
                action = new
                {
                    title = actionTitle,
                    sections = new[]
                    {
                        new
                        {
                            title = sectionTitle,
                            rows
                        }
                    }
                }
            }
        }, cancellationToken);
    }

    public Task<InfobipOutboundResult> SendWhatsAppDocumentMessageAsync(
        OutboundMessageEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(envelope.AttachmentUrl))
        {
            return Task.FromResult(new InfobipOutboundResult(false, null, 400, "AttachmentUrl is required for WhatsApp document messages."));
        }

        var filename = envelope.Metadata?.GetValueOrDefault("filename");
        if (string.IsNullOrWhiteSpace(filename))
        {
            try
            {
                filename = Path.GetFileName(new Uri(envelope.AttachmentUrl, UriKind.RelativeOrAbsolute).LocalPath);
            }
            catch
            {
                filename = null;
            }
        }
        if (string.IsNullOrWhiteSpace(filename)) filename = "document.pdf";

        return SendPayloadAsync("/whatsapp/1/message/document", (senderId) => new
        {
            from = senderId,
            to = envelope.RecipientId,
            messageId = Guid.NewGuid().ToString(),
            content = new
            {
                mediaUrl = envelope.AttachmentUrl,
                caption = envelope.Text,
                filename
            }
        }, cancellationToken);
    }

    private async Task<InfobipOutboundResult> SendPayloadAsync(
        string endpoint,
        Func<string, object> payloadFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var apiKeySecret = await _secretStore.ResolveAsync("channel.infobip.api_key", cancellationToken);
            var baseUrlSecret = await _secretStore.ResolveAsync("channel.infobip.base_url", cancellationToken);
            var senderIdSecret = await _secretStore.ResolveAsync("channel.infobip.sender_id", cancellationToken);

            var apiKey = apiKeySecret.RevealValue() ?? Environment.GetEnvironmentVariable("INFOBIP_API_KEY");
            var baseUrl = baseUrlSecret.RevealValue() ?? Environment.GetEnvironmentVariable("INFOBIP_BASE_URL");
            var senderId = senderIdSecret.RevealValue() ?? Environment.GetEnvironmentVariable("INFOBIP_SENDER_ID") ?? "ConvoLab";

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(baseUrl))
            {
                return new InfobipOutboundResult(false, null, null, "Infobip API Key or Base URL is missing.");
            }

            var client = _httpClientFactory.CreateClient("InfobipClient");
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("App", apiKey);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var payload = payloadFactory(senderId);

            var response = await client.PostAsJsonAsync(endpoint, payload, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                var messageId = content.TryGetProperty("messageId", out var idProp) ? idProp.GetString() : null;
                return new InfobipOutboundResult(true, messageId, (int)response.StatusCode, null);
            }
            else
            {
                _logger.LogWarning("Infobip API request to {Endpoint} failed with status {StatusCode}", endpoint, response.StatusCode);
                return new InfobipOutboundResult(
                    false,
                    null,
                    (int)response.StatusCode,
                    "The Infobip provider rejected the outbound message.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Failed to send outbound message via Infobip ({Endpoint}). ExceptionType={ExceptionType}",
                endpoint,
                ex.GetType().Name);
            return new InfobipOutboundResult(
                false,
                null,
                null,
                "The Infobip provider could not be reached.");
        }
    }
}
