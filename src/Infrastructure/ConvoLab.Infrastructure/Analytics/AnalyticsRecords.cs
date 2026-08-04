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
    public double? Groundedness { get; set; }
    public double? Relevance { get; set; }
    public double? Safety { get; set; }
    public double? OverallQuality { get; set; }
    public bool ProviderInvocationPrevented { get; set; }
    public Guid? SourceExecutionId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid? SourceId { get; set; }
    public string? PromptName { get; set; }
    public string? WorkflowName { get; set; }
    public string? KnowledgeCollectionName { get; set; }
    public string? PolicyOutcome { get; set; }
    public string? EvaluationOutcome { get; set; }
    public string ConfigurationRevision { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}

public abstract class AnalyticsAggregateRecord
{
    public Guid Id { get; set; }
    public string AggregateKey { get; set; } = string.Empty;
    public Guid OrganisationId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid EnvironmentId { get; set; }
    public DateTimeOffset BucketStart { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? Capability { get; set; }
    public string? Outcome { get; set; }
    // Deprecated alpha.14 compatibility field. New code uses the explicit measures below.
    public long Executions { get; set; }
    public long EventCount { get; set; }
    public long ExecutionCount { get; set; }
    public long SimulationCount { get; set; }
    public long EvaluationCount { get; set; }
    public long TraceCount { get; set; }
    public long ReplayCount { get; set; }
    public long ProviderInvocationCount { get; set; }
    public long ProviderInvocationPreventedCount { get; set; }
    public long PolicyEvaluationCount { get; set; }
    public long PolicyAllowedCount { get; set; }
    public long PolicyDeniedCount { get; set; }
    public long PolicyWarningCount { get; set; }
    public long PluginOperationCount { get; set; }
    public long Succeeded { get; set; }
    public long Failed { get; set; }
    public long Denied { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public decimal ActualCostZar { get; set; }
    public decimal EstimatedCostZar { get; set; }
    public long UnknownCostCount { get; set; }
    public double TotalDurationMs { get; set; }
    public double MaximumDurationMs { get; set; }
    public long DurationCount { get; set; }
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
    public DateTimeOffset? DirtyToUtc { get; set; }
    public DateTimeOffset? HighWatermarkUtc { get; set; }
    public Guid? LastProcessedEventId { get; set; }
    public DateTimeOffset? LastSuccessfulRunAt { get; set; }
    public string Status { get; set; } = "Pending";
    public string? FailureReason { get; set; }
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
    public bool IncludeActor { get; set; }
    public bool IncludeCost { get; set; }
    public bool IncludeTokenUsage { get; set; }
    public bool IncludeProviderDetails { get; set; }
    public bool IncludeSensitiveSource { get; set; }
    public int RetentionDays { get; set; } = 7;
    public byte[]? Content { get; set; }
    public long? RowCount { get; set; }
    public long? SizeBytes { get; set; }
    public string? Checksum { get; set; }
    public string? FailureReason { get; set; }
    public string? ProcessingOwner { get; set; }
    public long? ProcessingLeaseToken { get; set; }
    public DateTimeOffset? ProcessingStartedAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
