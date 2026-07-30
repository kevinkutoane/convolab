using System.Net.Http.Json;
using System.Text.Json;
using ConvoLab.Domain.Intelligence.Aggregates;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.Interfaces;
using ConvoLab.Domain.Intelligence.ValueObjects;
using ConvoLab.Application.Settings;

namespace ConvoLab.Infrastructure.Intelligence;

public sealed class GeminiIntelligenceExecutor : IIntelligenceExecutor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecretStore _secretStore;
    public IReadOnlyCollection<ProviderKind> SupportedProviders { get; } = [ProviderKind.Gemini];

    public GeminiIntelligenceExecutor(
        IHttpClientFactory httpClientFactory,
        ISecretStore secretStore)
    {
        _httpClientFactory = httpClientFactory;
        _secretStore = secretStore;
    }

    public async Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, string renderedPrompt, CancellationToken cancellationToken = default)
    {
        var secretReference = ExtractMarker(renderedPrompt, "SECRET_REFERENCE") ?? "env:GEMINI_API_KEY";
        var apiKey = _secretStore.Resolve(secretReference);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException($"Gemini secret reference '{secretReference}' could not be resolved.");
        var model = ExtractMarker(renderedPrompt, "MODEL");
        if (string.IsNullOrWhiteSpace(model) || model == "default")
            throw new InvalidOperationException("The effective runtime configuration did not resolve a Gemini model.");
        var temperature = double.TryParse(ExtractMarker(renderedPrompt, "TEMPERATURE"), out var parsedTemperature) ? parsedTemperature : 0.2;
        var maxTokens = int.TryParse(ExtractMarker(renderedPrompt, "MAX_OUTPUT_TOKENS"), out var parsedTokens) ? parsedTokens : 400;
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(apiKey)}";
        var payload = new
        {
            contents = new[] { new { role = "user", parts = new[] { new { text = renderedPrompt } } } },
            generationConfig = new { temperature, maxOutputTokens = maxTokens }
        };
        var httpClient = _httpClientFactory.CreateClient("Gemini");
        using var response = await httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Gemini returned {(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        var text = document.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? string.Empty;
        var usage = document.RootElement.TryGetProperty("usageMetadata", out var metadata)
            ? ExecutionUsage.Create(metadata.TryGetProperty("promptTokenCount", out var input) ? input.GetInt32() : Math.Max(1, renderedPrompt.Length / 4), metadata.TryGetProperty("candidatesTokenCount", out var output) ? output.GetInt32() : Math.Max(1, text.Length / 4))
            : ExecutionUsage.Create(Math.Max(1, renderedPrompt.Length / 4), Math.Max(1, text.Length / 4));
        return ExecutionResult.FromText(text, usage, ExecutionCost.Zero("ZAR"));
    }

    private static string? ExtractMarker(string value, string name)
    {
        var prefix = $"[{name}:";
        var start = value.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        start += prefix.Length;
        var end = value.IndexOf(']', start);
        return end < 0 ? null : value[start..end].Trim();
    }
}
