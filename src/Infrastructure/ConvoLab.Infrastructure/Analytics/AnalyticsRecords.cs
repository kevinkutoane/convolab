namespace ConvoLab.Infrastructure.Analytics;

public sealed class ConfigurationSnapshotRecord
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string Revision { get; set; } = string.Empty;
    public string ValuesJson { get; set; } = "{}";
    public string ProvenanceJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ExecutionAttributionRecord
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid EnvironmentId { get; set; }
    public Guid? ActorId { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string? ActorRole { get; set; }
    public string SourceResourceType { get; set; } = string.Empty;
    public Guid SourceResourceId { get; set; }
    public string ConfigurationRevision { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string AttributionStatus { get; set; } = "Original";
    public DateTimeOffset? BackfilledAt { get; set; }
    public string? BackfillVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AnalyticsOutboxRecord
{
    public Guid Id { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset AvailableAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}

public sealed class AnalyticsEventRecord
{
    public Guid Id { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public Guid OrganisationId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid EnvironmentId { get; set; }
    public Guid? ActorId { get; set; }
    public string ActorType { get; set; } = "System";
    public string? ActorRole { get; set; }
    public string Capability { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public decimal? CostZar { get; set; }
    public string CostType { get; set; } = "Unavailable";
    public string? PricingRevision { get; set; }
    public double? DurationMs { get; set; }
    public double? QualityScore { get; set; }
    public bool ProviderInvocationPrevented { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid? SourceId { get; set; }
    public string? PromptName { get; set; }
    public string? WorkflowName { get; set; }
    public string ConfigurationRevision { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}

public abstract class AnalyticsAggregateRecord
{
    public Guid Id { get; set; }
    public string AggregateKey { get; set; } = string.Empty;
    public Guid WorkspaceId { get; set; }
    public Guid EnvironmentId { get; set; }
    public DateTimeOffset BucketStart { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? Capability { get; set; }
    public string? Outcome { get; set; }
    public long Executions { get; set; }
    public long Succeeded { get; set; }
    public long Failed { get; set; }
    public long Denied { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public decimal ActualCostZar { get; set; }
    public decimal EstimatedCostZar { get; set; }
    public long UnknownCostCount { get; set; }
    public double TotalDurationMs { get; set; }
    public double TotalQualityScore { get; set; }
    public long QualityCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class AnalyticsHourlyAggregateRecord : AnalyticsAggregateRecord;
public sealed class AnalyticsDailyAggregateRecord : AnalyticsAggregateRecord;

public sealed class AnalyticsAggregationCheckpointRecord
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Granularity { get; set; } = string.Empty;
    public DateTimeOffset? DirtyFromUtc { get; set; }
    public DateTimeOffset? HighWatermarkUtc { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class AnalyticsExportRecord
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid CreatedBy { get; set; }
    public string Status { get; set; } = "Pending";
    public string FileName { get; set; } = string.Empty;
    public string FiltersJson { get; set; } = "{}";
    public byte[]? Content { get; set; }
    public long? RowCount { get; set; }
    public long? SizeBytes { get; set; }
    public string? Checksum { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
