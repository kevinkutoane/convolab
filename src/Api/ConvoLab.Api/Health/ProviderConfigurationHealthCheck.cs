using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConvoLab.Api.Health;

public sealed class ProviderConfigurationHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var configured = !string.IsNullOrWhiteSpace(
            configuration["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY"));
        return Task.FromResult(configured
            ? HealthCheckResult.Healthy("Deterministic and Gemini providers are configured.")
            : HealthCheckResult.Degraded(
                "Deterministic provider is ready; Gemini is not configured.",
                data: new Dictionary<string, object>
                {
                    ["deterministic"] = "Ready",
                    ["gemini"] = "Not configured"
                }));
    }
}
