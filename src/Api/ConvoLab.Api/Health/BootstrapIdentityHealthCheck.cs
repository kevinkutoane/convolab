using ConvoLab.Domain.WorkspaceIdentity;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConvoLab.Api.Health;

public sealed class BootstrapIdentityHealthCheck(ApplicationDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var configured = await db.LocalCredentials.AsNoTracking().AnyAsync(item => item.UserId == WorkspaceIdentityDefaults.BootstrapUserId, cancellationToken);
        return configured
            ? HealthCheckResult.Healthy("The bootstrap administrator has an active local credential.")
            : HealthCheckResult.Degraded("Workspace identity setup is required. Configure Bootstrap:Administrator:Password and restart without logging a generated secret.", null, new Dictionary<string, object> { ["setupRequired"] = true });
    }
}
