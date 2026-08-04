using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConvoLab.Api.Health;

public sealed record OperationalReadinessSnapshot(
    string Status,
    DateTimeOffset? LastEvaluatedAt);

public sealed class OperationalReadinessSummary
{
    private OperationalReadinessSnapshot _snapshot = new("Unknown", null);

    public OperationalReadinessSnapshot Snapshot() =>
        Volatile.Read(ref _snapshot);

    public void Record(HealthStatus status) =>
        Volatile.Write(
            ref _snapshot,
            new OperationalReadinessSnapshot(
                status.ToString(),
                DateTimeOffset.UtcNow));
}
