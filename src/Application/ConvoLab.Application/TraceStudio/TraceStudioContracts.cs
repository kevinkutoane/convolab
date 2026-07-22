using ConvoLab.Application.Simulation;

namespace ConvoLab.Application.TraceStudio;

public sealed record TraceMetricsDto(
    int TotalTraces,
    int CompletedTraces,
    int FailedTraces,
    double SuccessRate,
    int TotalSpans,
    double AverageDurationMs,
    double P95DurationMs,
    long TotalTokens,
    decimal TotalCost,
    string Currency);

public sealed record TraceDailyActivityDto(
    DateOnly Date,
    int Traces,
    int Failed,
    double AverageDurationMs);

public sealed record TraceCapabilityMetricDto(
    string Capability,
    int Spans,
    int Failed,
    double AverageDurationMs,
    double Share);

public sealed record TraceSummaryDto(
    Guid Id,
    Guid CorrelationId,
    string OperationName,
    string Source,
    string Status,
    Guid? SimulationId,
    string? SimulationTitle,
    Guid? SourceRunId,
    Guid? ReplayedFromRunId,
    string? Provider,
    string? Model,
    string? Workflow,
    string? PromptVersion,
    string? KnowledgeCollection,
    string? EvaluationVerdict,
    double DurationMs,
    int SpanCount,
    int FailedSpanCount,
    int TotalTokens,
    decimal ActualCost,
    string Currency,
    string? FailureReason,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record TraceSpanDto(
    Guid Id,
    Guid TraceId,
    Guid? ParentSpanId,
    string Name,
    string Capability,
    string Status,
    string Detail,
    int Sequence,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    double DurationMs,
    IReadOnlyDictionary<string, string> Attributes);

public sealed record TraceEventDto(
    Guid Id,
    Guid TraceId,
    Guid? SpanId,
    string Name,
    string Level,
    string Message,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, string> Attributes);

public sealed record TraceArtifactDto(
    Guid Id,
    Guid TraceId,
    Guid? SpanId,
    string Kind,
    string Name,
    string ContentType,
    string Content,
    bool IsSensitive,
    bool IsRedacted,
    DateTimeOffset CreatedAt);

public sealed record TraceDetailDto(
    TraceSummaryDto Summary,
    IReadOnlyList<TraceSpanDto> Spans,
    IReadOnlyList<TraceEventDto> Events,
    IReadOnlyList<TraceArtifactDto> Artifacts);

public sealed record TraceOverviewDto(
    TraceMetricsDto Metrics,
    IReadOnlyList<TraceDailyActivityDto> Activity,
    IReadOnlyList<TraceCapabilityMetricDto> Capabilities,
    IReadOnlyList<TraceSummaryDto> RecentTraces,
    IReadOnlyList<string> Providers,
    IReadOnlyList<string> Statuses,
    DateTimeOffset GeneratedAt);

public sealed record TraceSearchQuery(
    string? Query = null,
    string? Status = null,
    string? Capability = null,
    string? Provider = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Limit = 250);

public sealed record TraceState(
    Guid Id,
    Guid CorrelationId,
    string OperationName,
    string Source,
    string Status,
    Guid? SimulationId,
    string? SimulationTitle,
    Guid? SourceRunId,
    Guid? ReplayedFromRunId,
    string? Provider,
    string? Model,
    string? Workflow,
    string? PromptVersion,
    string? KnowledgeCollection,
    string? EvaluationVerdict,
    double DurationMs,
    int TotalTokens,
    decimal ActualCost,
    string Currency,
    string? FailureReason,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<TraceSpanState> Spans,
    IReadOnlyList<TraceEventState> Events,
    IReadOnlyList<TraceArtifactState> Artifacts);

public sealed record TraceSpanState(
    Guid Id,
    Guid TraceId,
    Guid? ParentSpanId,
    string Name,
    string Capability,
    string Status,
    string Detail,
    int Sequence,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    double DurationMs,
    IReadOnlyDictionary<string, string> Attributes);

public sealed record TraceEventState(
    Guid Id,
    Guid TraceId,
    Guid? SpanId,
    string Name,
    string Level,
    string Message,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, string> Attributes);

public sealed record TraceArtifactState(
    Guid Id,
    Guid TraceId,
    Guid? SpanId,
    string Kind,
    string Name,
    string ContentType,
    string Content,
    bool IsSensitive,
    DateTimeOffset CreatedAt);

public interface ITraceStudioRepository
{
    Task<IReadOnlyList<TraceState>> ListAsync(int limit = 500, CancellationToken cancellationToken = default);
    Task<TraceState?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TraceState?> GetBySourceRunAsync(Guid sourceRunId, CancellationToken cancellationToken = default);
    Task AddAsync(TraceState trace, CancellationToken cancellationToken = default);
    Task AddSpanAsync(TraceSpanState span, TraceEventState traceEvent, CancellationToken cancellationToken = default);
    Task CompleteAsync(Guid id, string status, DateTimeOffset completedAt, double durationMs, string? failureReason, CancellationToken cancellationToken = default);
}

public interface ITraceStudioService
{
    Task<TraceOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TraceSummaryDto>> ListAsync(TraceSearchQuery query, CancellationToken cancellationToken = default);
    Task<TraceDetailDto> GetAsync(Guid id, bool includeSensitive = false, CancellationToken cancellationToken = default);
    Task<TraceDetailDto> RecordSimulationRunAsync(
        Guid simulationId,
        string simulationTitle,
        SimulationRun run,
        string? responseText = null,
        CancellationToken cancellationToken = default);
}
