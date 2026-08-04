using ConvoLab.Application.Settings;
using ConvoLab.Application.Operations;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ConvoLab.Api.Health;

public sealed class WorkerHeartbeatHealthCheck(
    ApplicationDbContext db,
    IOptions<OperationsThresholdOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var heartbeat = await db.OperationalWorkerHeartbeats.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.WorkerName == "analytics-maintenance",
                cancellationToken);
        if (heartbeat is null) return HealthCheckResult.Degraded("No worker heartbeat has been recorded.");
        var age = DateTimeOffset.UtcNow - heartbeat.LastHeartbeatAt;
        if (age >= TimeSpan.FromSeconds(options.Value.WorkerUnhealthySeconds)
            || heartbeat.CurrentStatus is "Failed" or "LeaseLost")
            return HealthCheckResult.Unhealthy("The Analytics worker heartbeat is stale.");
        return age >= TimeSpan.FromSeconds(options.Value.WorkerWarningSeconds)
               || heartbeat.CurrentStatus == "Degraded"
            ? HealthCheckResult.Degraded("The Analytics worker is reporting an impaired iteration.")
            : HealthCheckResult.Healthy("The Analytics worker heartbeat is current.");
    }
}

public sealed class AnalyticsPipelineHealthCheck(
    IAnalyticsOperationalEvidenceReader evidenceReader,
    ApplicationDbContext db,
    IOptions<OperationsThresholdOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var evidence = await evidenceReader.ReadAsync(cancellationToken);
        var partialFailure = await db.OperationalWorkerHeartbeats.AsNoTracking()
            .AnyAsync(item => item.WorkerName == "analytics-maintenance"
                && item.CurrentStatus == "Degraded", cancellationToken);
        var status = AnalyticsPipelineStatusEvaluator.Evaluate(
            evidence,
            options.Value,
            partialFailure);
        var data = new Dictionary<string, object>
        {
            ["pendingCount"] = evidence.PendingCount,
            ["failedCount"] = evidence.FailedCount,
            ["oldestPendingAgeSeconds"] = evidence.OldestPendingAgeSeconds ?? 0,
            ["oldestFailedAgeSeconds"] = evidence.OldestFailedAgeSeconds ?? 0,
            ["aggregationDirtyCheckpointCount"] = evidence.AggregationDirtyCheckpointCount,
            ["aggregationFailedCheckpointCount"] = evidence.AggregationFailedCheckpointCount,
            ["maximumAggregationLagSeconds"] = evidence.MaximumAggregationLagSeconds
        };
        return status switch
        {
            OperationalStatusLevel.Unhealthy => HealthCheckResult.Unhealthy(
                "The Analytics operational pipeline exceeded an unhealthy threshold.",
                data: data),
            OperationalStatusLevel.Degraded => HealthCheckResult.Degraded(
                "The Analytics operational pipeline is impaired.",
                data: data),
            _ => HealthCheckResult.Healthy(
                "The Analytics outbox and aggregation lag are within thresholds.",
                data)
        };
    }
}

public sealed class RequiredSecretsHealthCheck(
    IRequiredSecretReadinessEvaluator evaluator) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await evaluator.EvaluateAsync(cancellationToken);
        var required = snapshot.Environments.Where(item => item.Required).ToArray();
        var data = new Dictionary<string, object>
        {
            ["scopedEnvironments"] = snapshot.Environments.Count,
            ["requiredSecrets"] = required.Length,
            ["dependencyStates"] = snapshot.Environments
                .GroupBy(item => item.DependencyState.ToString())
                .ToDictionary(group => group.Key, group => group.Count()),
            ["failureCodes"] = snapshot.ScopeFailureCodes
                .Concat(required.Where(item => item.FailureCode is not null)
                    .Select(item => item.FailureCode!))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };
        if (snapshot.ScopeFailureCodes.Count > 0
            || required.Any(item => item.DependencyState == OperationalDependencyState.Unavailable))
            return HealthCheckResult.Unhealthy(
                "A required effective external-provider credential is unavailable.",
                data: data);
        if (required.Any(item => item.DependencyState == OperationalDependencyState.Degraded))
            return HealthCheckResult.Degraded(
                "A required effective external-provider credential is degraded.",
                data: data);
        return HealthCheckResult.Healthy(
            "Effective required credentials validate without exposing references or values.",
            data);
    }
}
