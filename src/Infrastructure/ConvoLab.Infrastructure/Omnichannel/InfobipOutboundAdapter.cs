using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    public async Task<InfobipOutboundResult> SendWhatsAppTextMessageAsync(
        OutboundMessageEnvelope envelope,
        CancellationToken cancellationToken = default)
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
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var payload = new
            {
                from = senderId,
                to = envelope.RecipientId,
                messageId = Guid.NewGuid().ToString(),
                content = new { text = envelope.Text }
            };

            var response = await client.PostAsJsonAsync("/whatsapp/1/message/text", payload, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                var messageId = content.TryGetProperty("messageId", out var idProp) ? idProp.GetString() : null;
                return new InfobipOutboundResult(true, messageId, (int)response.StatusCode, null);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Infobip API error: {StatusCode} {ErrorContent}", response.StatusCode, errorContent);
                return new InfobipOutboundResult(false, null, (int)response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send outbound message via Infobip");
            return new InfobipOutboundResult(false, null, null, ex.Message);
        }
    }
}
