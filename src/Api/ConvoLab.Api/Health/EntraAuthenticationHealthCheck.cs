using ConvoLab.Api.Security;
using ConvoLab.Application.Operations;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ConvoLab.Api.Health;

public sealed class EntraAuthenticationHealthCheck(
    IOptions<AuthenticationOptions> authentication,
    EntraDependencyEvidence evidence) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = authentication.Value;
        if (options.Mode == ConvoLabAuthenticationMode.Local || !options.Entra.Enabled)
            return Task.FromResult(HealthCheckResult.Healthy("Microsoft Entra is not configured for Local mode.",
                data: new Dictionary<string, object> { ["state"] = OperationalDependencyState.NotConfigured.ToString() }));
        var snapshot = evidence.Snapshot();
        var data = new Dictionary<string, object>
        {
            ["state"] = snapshot.State.ToString(),
            ["lastCheckedAt"] = snapshot.CheckedAt?.ToString("O") ?? string.Empty,
            ["failureCode"] = snapshot.FailureCode ?? string.Empty
        };
        return Task.FromResult(snapshot.State switch
        {
            OperationalDependencyState.Unavailable or OperationalDependencyState.Degraded =>
                HealthCheckResult.Degraded("Microsoft Entra is unavailable; existing application sessions remain valid.", data: data),
            _ => HealthCheckResult.Healthy("Microsoft Entra authentication configuration is available.", data: data)
        });
    }
}
