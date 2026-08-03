using ConvoLab.Application.Settings;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConvoLab.Api.Health;

public sealed class WorkerHeartbeatHealthCheck(ApplicationDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var heartbeats = await db.OperationalWorkerHeartbeats.AsNoTracking()
            .ToListAsync(cancellationToken);
        var heartbeat = heartbeats.OrderByDescending(item => item.LastHeartbeatAt).FirstOrDefault();
        if (heartbeat is null) return HealthCheckResult.Degraded("No worker heartbeat has been recorded.");
        var age = DateTimeOffset.UtcNow - heartbeat.LastHeartbeatAt;
        if (age > TimeSpan.FromSeconds(60))
            return HealthCheckResult.Unhealthy("The Analytics worker heartbeat is stale.");
        return heartbeat.CurrentStatus == "Degraded"
            ? HealthCheckResult.Degraded("The Analytics worker is reporting an impaired iteration.")
            : HealthCheckResult.Healthy("The Analytics worker heartbeat is current.");
    }
}

public sealed class AnalyticsPipelineHealthCheck(ApplicationDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        DateTimeOffset? oldestOutbox;
        DateTimeOffset? oldestAggregation;
        if (db.Database.IsSqlite())
        {
            var outboxTimes = await db.AnalyticsOutbox.AsNoTracking()
                .Where(item => item.Status == "Pending")
                .Select(item => item.CreatedAt)
                .ToListAsync(cancellationToken);
            var aggregationTimes = await db.AnalyticsAggregationCheckpoints.AsNoTracking()
                .Where(item => item.Status == "Pending" || item.Status == "Failed")
                .Select(item => item.UpdatedAt)
                .ToListAsync(cancellationToken);
            oldestOutbox = outboxTimes.Count == 0 ? null : outboxTimes.Min();
            oldestAggregation = aggregationTimes.Count == 0 ? null : aggregationTimes.Min();
        }
        else
        {
            oldestOutbox = await db.AnalyticsOutbox.AsNoTracking()
                .Where(item => item.Status == "Pending")
                .MinAsync(item => (DateTimeOffset?)item.CreatedAt, cancellationToken);
            oldestAggregation = await db.AnalyticsAggregationCheckpoints.AsNoTracking()
                .Where(item => item.Status == "Pending" || item.Status == "Failed")
                .MinAsync(item => (DateTimeOffset?)item.UpdatedAt, cancellationToken);
        }
        var outboxAge = oldestOutbox.HasValue ? now - oldestOutbox.Value : TimeSpan.Zero;
        var aggregationAge = oldestAggregation.HasValue ? now - oldestAggregation.Value : TimeSpan.Zero;
        if (outboxAge >= TimeSpan.FromSeconds(300) || aggregationAge >= TimeSpan.FromSeconds(600))
            return HealthCheckResult.Unhealthy("The Analytics operational pipeline exceeded an unhealthy threshold.");
        if (outboxAge >= TimeSpan.FromSeconds(60) || aggregationAge >= TimeSpan.FromSeconds(120))
            return HealthCheckResult.Degraded("The Analytics operational pipeline exceeded a warning threshold.");
        return HealthCheckResult.Healthy("The Analytics outbox and aggregation lag are within thresholds.");
    }
}

public sealed class RequiredSecretsHealthCheck(
    ApplicationDbContext db,
    ISecretStore secretStore) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var rawReferences = await db.SettingValues.AsNoTracking()
            .Where(item => item.DefinitionKey == "ai.secret_reference")
            .Select(item => item.ValueJson)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var raw in rawReferences)
        {
            var reference = raw.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(reference)) continue;
            var result = await secretStore.ValidateAsync(reference, cancellationToken);
            if (!result.IsValid)
                return HealthCheckResult.Unhealthy("A required external-provider credential is unavailable.");
        }
        return HealthCheckResult.Healthy(
            "Required secret references resolve without exposing their values.",
            data: new Dictionary<string, object>
            {
                ["configuredReferences"] = rawReferences.Count
            });
    }
}
