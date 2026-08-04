namespace ConvoLab.Infrastructure.Operations;

public sealed class PlatformOperationalSettingRecord
{
    public string Key { get; set; } = "platform";
    public bool SafeModeEnabled { get; set; }
    public string? SafeModeReason { get; set; }
    public Guid? ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public long Revision { get; set; } = 1;
}

public sealed class OperationalWorkerHeartbeatRecord
{
    public string WorkerName { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset LastHeartbeatAt { get; set; }
    public DateTimeOffset? LastIterationStartedAt { get; set; }
    public DateTimeOffset? LastIterationCompletedAt { get; set; }
    public DateTimeOffset? LastSuccessfulIterationAt { get; set; }
    public DateTimeOffset? LastDegradedIterationAt { get; set; }
    public DateTimeOffset? LastFailureAt { get; set; }
    public string? LastFailureCode { get; set; }
    public string? LastFailureSummary { get; set; }
    public string CurrentStatus { get; set; } = "Starting";
    public long ProcessedCount { get; set; }
    public int LastOutboxProcessed { get; set; }
    public int LastOutboxFailed { get; set; }
    public int LastExportsCompleted { get; set; }
    public int LastExportsFailed { get; set; }
    public int LastAggregateBucketsCompleted { get; set; }
    public int LastAggregateBucketsFailed { get; set; }
    public int LastRetentionRowsRemoved { get; set; }
    public long CumulativeProcessedCount { get; set; }
    public long LeaseToken { get; set; }
    public DateTimeOffset LeaseExpiresAt { get; set; }
    public long Revision { get; set; } = 1;
}
