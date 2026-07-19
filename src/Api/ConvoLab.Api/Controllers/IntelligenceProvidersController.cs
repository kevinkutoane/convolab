using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.IntelligenceStudio;
using Microsoft.AspNetCore.Mvc;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/intelligence/providers")]
public sealed class IntelligenceProvidersController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IIntelligenceStudioConfiguration _studioConfiguration;

    public IntelligenceProvidersController(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IIntelligenceStudioConfiguration studioConfiguration)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _studioConfiguration = studioConfiguration;
    }

    [HttpGet]
    public ActionResult GetProviders()
        => Ok(_studioConfiguration.GetProviders());

    [HttpPost("{provider}/test")]
    public async Task<ActionResult> Test(string provider, CancellationToken cancellationToken)
    {
        if (provider.Equals("Deterministic", StringComparison.OrdinalIgnoreCase))
            return Ok(new { provider = "Deterministic", status = "Ready", latencyMs = 0 });
        if (!provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            throw new ResourceNotFoundException("provider.not_found", $"Provider '{provider}' was not found.");

        var apiKey = GeminiApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new RequestValidationException(
                "provider.gemini.not_configured",
                "Set GEMINI_API_KEY on the API host before testing Gemini.");

        var model = GeminiModel();
        var client = _httpClientFactory.CreateClient("Gemini");
        var started = DateTimeOffset.UtcNow;
        using var response = await client.PostAsJsonAsync(
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(apiKey)}",
            new { contents = new[] { new { parts = new[] { new { text = "Reply with READY" } } } } },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new ExternalDependencyException(
                "provider.gemini.connection_failed",
                $"Gemini connection test failed with HTTP {(int)response.StatusCode}.");

        return Ok(new
        {
            provider = "Gemini",
            status = "Ready",
            latencyMs = (DateTimeOffset.UtcNow - started).TotalMilliseconds,
            model
        });
    }

    private string? GeminiApiKey()
    {
        var environmentValue = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        return !string.IsNullOrWhiteSpace(environmentValue)
            ? environmentValue
            : _configuration["Gemini:ApiKey"];
    }

    private string GeminiModel()
    {
        var environmentValue = Environment.GetEnvironmentVariable("GEMINI_MODEL");
        return !string.IsNullOrWhiteSpace(environmentValue)
            ? environmentValue
            : _configuration["Gemini:Model"] ?? "gemini-2.5-flash";
    }
}
