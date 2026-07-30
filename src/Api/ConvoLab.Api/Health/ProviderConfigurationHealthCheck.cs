using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConvoLab.Api.Health;

public sealed class ProviderConfigurationHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy(
            "The local deterministic provider is ready. External providers are validated per environment.",
            data: new Dictionary<string, object>
            {
                ["deterministic"] = "Ready",
                ["externalProviders"] = "Environment scoped"
            }));
    }
}
