namespace ConvoLab.Application.Operations;

public sealed class ProxyOptions
{
    public bool Enabled { get; init; }
    public int ForwardLimit { get; init; } = 1;
    public string[] KnownProxies { get; init; } = [];
    public string[] KnownNetworks { get; init; } = [];
}

public sealed class LocalAuthenticationOptions
{
    public bool ProductionAllowed { get; init; }
}

public sealed class DataProtectionOptions
{
    public string Provider { get; init; } = "LocalFileSystem";
    public string KeyRingPath { get; init; } = string.Empty;
    public string CertificatePemPath { get; init; } = string.Empty;
    public string PrivateKeyPemPath { get; init; } = string.Empty;
}

public sealed class SecretStoreOptions
{
    public int CacheTtlSeconds { get; init; } = 300;
    public string DockerSecretsRoot { get; init; } = string.Empty;
    public AzureKeyVaultOptions AzureKeyVault { get; init; } = new();
}

public sealed class AzureKeyVaultOptions
{
    public string[] AllowedVaultUris { get; init; } = [];
    public int TimeoutSeconds { get; init; } = 10;
    public int MaxRetries { get; init; } = 2;
    public string ManagedIdentityClientId { get; init; } = string.Empty;
}

public sealed class SafeModeOptions
{
    public bool AllowDeterministicVerification { get; init; }
    public bool? BlockAnalyticsExports { get; init; }
}

public sealed class OperationsThresholdOptions
{
    public int WorkerWarningSeconds { get; init; } = 45;
    public int WorkerUnhealthySeconds { get; init; } = 60;
    public int OutboxWarningSeconds { get; init; } = 60;
    public int OutboxUnhealthySeconds { get; init; } = 300;
    public int FailedOutboxDegradedCount { get; init; } = 1;
    public int FailedOutboxUnhealthyCount { get; init; } = 10;
    public int FailedOutboxUnhealthyAgeSeconds { get; init; } = 300;
    public int AggregationWarningSeconds { get; init; } = 120;
    public int AggregationUnhealthySeconds { get; init; } = 600;

    public static bool IsValid(OperationsThresholdOptions value) =>
        value.WorkerWarningSeconds > 0
        && value.WorkerWarningSeconds < value.WorkerUnhealthySeconds
        && value.OutboxWarningSeconds > 0
        && value.OutboxWarningSeconds < value.OutboxUnhealthySeconds
        && value.FailedOutboxDegradedCount > 0
        && value.FailedOutboxDegradedCount < value.FailedOutboxUnhealthyCount
        && value.FailedOutboxUnhealthyAgeSeconds > 0
        && value.AggregationWarningSeconds > 0
        && value.AggregationWarningSeconds < value.AggregationUnhealthySeconds;
}

public sealed class RequiredSecretReadinessOptions
{
    public string[] UatEnvironmentIdsOrNames { get; init; } = [];
    public string[] ProductionEnvironmentIdsOrNames { get; init; } = [];
    public string[] ProvidersWithoutSecrets { get; init; } = ["Deterministic", "ConvoLab Deterministic"];
}

public sealed class TelemetryOptions
{
    public ConsoleTelemetryOptions ConsoleExporter { get; init; } = new();
    public int OperationalSnapshotSeconds { get; init; } = 15;
    public int CollectorProbeSeconds { get; init; } = 30;
    public string ServiceName { get; init; } = "ConvoLab.Api";
}

public sealed class ConsoleTelemetryOptions
{
    public bool Enabled { get; init; }
}

public sealed class BuildOptions
{
    public string? Commit { get; init; }
    public DateTimeOffset? Time { get; init; }
    public Dictionary<string, string> Digests { get; init; } = [];
}

public sealed class AnalyticsWorkerOptions
{
    public int LeaseDurationSeconds { get; init; } = 120;
    public int LeaseRenewalSeconds { get; init; } = 30;
    public int PollIntervalSeconds { get; init; } = 10;
    public int MaximumBatchSize { get; init; } = 100;
    public int RenewalFailureTolerance { get; init; } = 2;

    public static bool IsValid(AnalyticsWorkerOptions value) =>
        value.LeaseDurationSeconds is >= 30 and <= 3600
        && value.LeaseRenewalSeconds > 0
        && value.LeaseRenewalSeconds < value.LeaseDurationSeconds
        && value.PollIntervalSeconds is >= 1 and <= 300
        && value.MaximumBatchSize is >= 1 and <= 1000
        && value.RenewalFailureTolerance is >= 0 and <= 10;
}

public enum OperationalStatusLevel
{
    Healthy,
    Degraded,
    Unhealthy
}

public sealed record AnalyticsPipelineEvidence(
    int PendingCount,
    int FailedCount,
    double? OldestPendingAgeSeconds,
    double? OldestFailedAgeSeconds,
    int AggregationDirtyCheckpointCount,
    int AggregationFailedCheckpointCount,
    double MaximumAggregationLagSeconds,
    DateTimeOffset? LastSuccessfulAggregationAt,
    DateTimeOffset? LastSuccessfulOutboxDispatchAt);

public interface IAnalyticsOperationalEvidenceReader
{
    Task<AnalyticsPipelineEvidence> ReadAsync(CancellationToken ct = default);
}

public static class AnalyticsPipelineStatusEvaluator
{
    public static OperationalStatusLevel Evaluate(
        AnalyticsPipelineEvidence evidence,
        OperationsThresholdOptions thresholds,
        bool recentPartialWorkerFailure = false)
    {
        if ((evidence.OldestPendingAgeSeconds ?? 0) >= thresholds.OutboxUnhealthySeconds
            || evidence.FailedCount >= thresholds.FailedOutboxUnhealthyCount
            || (evidence.FailedCount > 0
                && (evidence.OldestFailedAgeSeconds ?? 0) >= thresholds.FailedOutboxUnhealthyAgeSeconds)
            || evidence.MaximumAggregationLagSeconds >= thresholds.AggregationUnhealthySeconds)
            return OperationalStatusLevel.Unhealthy;

        if ((evidence.OldestPendingAgeSeconds ?? 0) >= thresholds.OutboxWarningSeconds
            || evidence.FailedCount >= thresholds.FailedOutboxDegradedCount
            || evidence.AggregationFailedCheckpointCount > 0
            || evidence.MaximumAggregationLagSeconds >= thresholds.AggregationWarningSeconds
            || recentPartialWorkerFailure)
            return OperationalStatusLevel.Degraded;

        return OperationalStatusLevel.Healthy;
    }
}
