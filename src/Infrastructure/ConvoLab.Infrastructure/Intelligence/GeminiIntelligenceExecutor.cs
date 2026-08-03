using System.Net.Http.Json;
using System.Text.Json;
using ConvoLab.Domain.Intelligence.Aggregates;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.Interfaces;
using ConvoLab.Domain.Intelligence.ValueObjects;
using ConvoLab.Application.Settings;
using ConvoLab.Application.Operations;

namespace ConvoLab.Infrastructure.Intelligence;

public sealed class GeminiIntelligenceExecutor : IIntelligenceExecutor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecretStore _secretStore;
    private readonly IPlatformOperationalState _operationalState;
    public IReadOnlyCollection<ProviderKind> SupportedProviders { get; } = [ProviderKind.Gemini];

    public GeminiIntelligenceExecutor(
        IHttpClientFactory httpClientFactory,
        ISecretStore secretStore,
        IPlatformOperationalState operationalState)
    {
        _httpClientFactory = httpClientFactory;
        _secretStore = secretStore;
        _operationalState = operationalState;
    }

    public async Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, string renderedPrompt, CancellationToken cancellationToken = default)
    {
        await _operationalState.EnsureExternalExecutionAllowedAsync(cancellationToken);
        using var activity = ConvoLabTelemetry.ActivitySource.StartActivity("provider.invoke");
        activity?.SetTag("provider", "gemini");
        var metricTags = ProviderTags();
        ConvoLabTelemetry.ProviderInvocations.Add(1, metricTags);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var secretReference = ExtractMarker(renderedPrompt, "SECRET_REFERENCE") ?? "env:GEMINI_API_KEY";
        var apiKey = (await _secretStore.ResolveAsync(secretReference, cancellationToken)).RevealValue();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("The configured Gemini credential could not be resolved.");
        var model = ExtractMarker(renderedPrompt, "MODEL");
        if (string.IsNullOrWhiteSpace(model) || model == "default")
            throw new InvalidOperationException("The effective runtime configuration did not resolve a Gemini model.");
        var temperature = double.TryParse(ExtractMarker(renderedPrompt, "TEMPERATURE"), out var parsedTemperature) ? parsedTemperature : 0.2;
        var maxTokens = int.TryParse(ExtractMarker(renderedPrompt, "MAX_OUTPUT_TOKENS"), out var parsedTokens) ? parsedTokens : 400;
        var providerPrompt = StripOperationalMarkers(renderedPrompt);
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent";
        var payload = new
        {
            contents = new[] { new { role = "user", parts = new[] { new { text = providerPrompt } } } },
            generationConfig = new { temperature, maxOutputTokens = maxTokens }
        };
        var httpClient = _httpClientFactory.CreateClient("Gemini");
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload)
        };
        message.Options.Set(SensitiveTelemetryHttpRequestOptions.SuppressAutomaticInstrumentation, true);
        message.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
        using var response = await httpClient.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            ConvoLabTelemetry.ProviderFailures.Add(1, metricTags);
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, "provider_http_error");
            throw new HttpRequestException($"Gemini returned HTTP {(int)response.StatusCode}.");
        }
        using var document = JsonDocument.Parse(body);
        var text = document.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? string.Empty;
        var usage = document.RootElement.TryGetProperty("usageMetadata", out var metadata)
            ? ExecutionUsage.Create(metadata.TryGetProperty("promptTokenCount", out var input) ? input.GetInt32() : Math.Max(1, providerPrompt.Length / 4), metadata.TryGetProperty("candidatesTokenCount", out var output) ? output.GetInt32() : Math.Max(1, text.Length / 4))
            : ExecutionUsage.Create(Math.Max(1, providerPrompt.Length / 4), Math.Max(1, text.Length / 4));
        stopwatch.Stop();
        ConvoLabTelemetry.ProviderDuration.Record(stopwatch.Elapsed.TotalMilliseconds, metricTags);
        ConvoLabTelemetry.ProviderInputTokens.Add(usage.InputTokens, metricTags);
        ConvoLabTelemetry.ProviderOutputTokens.Add(usage.OutputTokens, metricTags);
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

    private static string StripOperationalMarkers(string value)
    {
        foreach (var name in new[] { "PROVIDER", "SECRET_REFERENCE", "MODEL", "TEMPERATURE", "MAX_OUTPUT_TOKENS" })
        {
            var prefix = $"[{name}:";
            while (true)
            {
                var start = value.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (start < 0) break;
                var end = value.IndexOf(']', start + prefix.Length);
                value = end < 0 ? value[..start] : value.Remove(start, end - start + 1);
            }
        }
        return value.TrimStart();
    }

    private static System.Diagnostics.TagList ProviderTags()
    {
        System.Diagnostics.TagList tags = default;
        tags.Add("provider", "gemini");
        return tags;
    }
}
