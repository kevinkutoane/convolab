using ConvoLab.Application.Operations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConvoLab.Api.Health;

public sealed class ProductionConfigurationHealthCheck(IProductionReadinessValidator validator) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(cancellationToken);
        return result.IsReady
            ? HealthCheckResult.Healthy("Static production configuration is valid.")
            : HealthCheckResult.Unhealthy(
                "Static production configuration is invalid.",
                data: new Dictionary<string, object>
                {
                    ["findingCodes"] = result.Findings.Select(item => item.Code).ToArray()
                });
    }
}

public sealed class DataProtectionReadinessHealthCheck(IDataProtectionProvider provider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var protector = provider.CreateProtector("ConvoLab.OperationalReadiness.v1");
            var sentinel = Guid.NewGuid().ToString("N");
            var protectedValue = protector.Protect(sentinel);
            var restored = protector.Unprotect(protectedValue);
            return Task.FromResult(restored == sentinel
                ? HealthCheckResult.Healthy("The ConvoLab key ring can protect and unprotect data.")
                : HealthCheckResult.Unhealthy("The data-protection round trip produced invalid data."));
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "The data-protection protect/unprotect probe failed.", exception));
        }
    }
}
