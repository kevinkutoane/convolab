using ConvoLab.Domain.WorkspaceIdentity;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConvoLab.Api.Health;

public sealed class BootstrapIdentityHealthCheck(ApplicationDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var configured = await db.LocalCredentials.AsNoTracking()
            .AnyAsync(item => item.UserId == WorkspaceIdentityDefaults.BootstrapUserId, cancellationToken);
        var activeAdministrator = await db.IdentityUsers.AsNoTracking()
            .AnyAsync(item => item.Id == WorkspaceIdentityDefaults.BootstrapUserId
                              && item.Status == "Active"
                              && item.IsPlatformAdministrator, cancellationToken);
        var runtimeReady = await db.RuntimeEnvironments.AsNoTracking()
            .AnyAsync(item => item.Status == "Active" && item.IsDefault, cancellationToken);
        if (configured && activeAdministrator && runtimeReady)
            return HealthCheckResult.Healthy(
                "Identity and default runtime-environment bootstrap are ready.");

        return HealthCheckResult.Degraded(
            "Identity or default runtime-environment bootstrap is incomplete.",
            null,
            new Dictionary<string, object>
            {
                ["credentialConfigured"] = configured,
                ["administratorActive"] = activeAdministrator,
                ["defaultRuntimeReady"] = runtimeReady
            });
    }
}
