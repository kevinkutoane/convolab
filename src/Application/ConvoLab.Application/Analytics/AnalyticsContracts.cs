namespace ConvoLab.Application.Analytics;

public sealed record AnalyticsQuery(
    Guid WorkspaceId,
    Guid? EnvironmentId,
    DateTimeOffset From,
    DateTimeOffset To,
    string Granularity = "day",
    string? Provider = null,
    string? Model = null,
    string? Capability = null,
    string? Outcome = null,
    string? ConfigurationRevision = null,
    string? Prompt = null,
    string? Workflow = null);

public sealed record AnalyticsScopeDto(
    Guid WorkspaceId,
    Guid? EnvironmentId,
    DateTimeOffset From,
    DateTimeOffset To,
    string Granularity,
    IReadOnlyDictionary<string, string?> Filters);

public sealed record AnalyticsMetricDto(
    string Key,
    string Label,
    decimal? Value,
    string Unit,
    string? Classification = null,
    string? Detail = null);

public sealed record AnalyticsPointDto(
    DateTimeOffset Bucket,
    long Executions,
    long Succeeded,
    long Failed,
    long Denied,
    long InputTokens,
    long OutputTokens,
    decimal ActualCostZar,
    decimal EstimatedCostZar,
    long UnknownCostCount,
    double AverageDurationMs,
    double? AverageQuality);

public sealed record AnalyticsIndicatorDto(
    string Key,
    string Severity,
    string Title,
    string Detail,
    DateTimeOffset DetectedAt);

public sealed record AnalyticsDashboardDto(
    string Category,
    AnalyticsScopeDto Scope,
    IReadOnlyList<AnalyticsMetricDto> Metrics,
    IReadOnlyList<AnalyticsPointDto> Series,
    IReadOnlyList<AnalyticsIndicatorDto> Indicators,
    bool IsPartial,
    DateTimeOffset GeneratedAt);

public sealed record AnalyticsEventDto(
    Guid Id,
    Guid WorkspaceId,
    Guid EnvironmentId,
    Guid? ActorId,
    string ActorType,
    string? ActorRole,
    string Capability,
    string EventType,
    string Outcome,
    string? Provider,
    string? Model,
    int? InputTokens,
    int? OutputTokens,
    decimal? CostZar,
    string CostType,
    double? DurationMs,
    double? QualityScore,
    bool ProviderInvocationPrevented,
    string SourceType,
    Guid? SourceId,
    string ConfigurationRevision,
    string CorrelationId,
    DateTimeOffset OccurredAt);

public sealed record AnalyticsEventPageDto(
    AnalyticsScopeDto Scope,
    IReadOnlyList<AnalyticsEventDto> Items,
    string? NextCursor);

public sealed record AnalyticsFilterOptionsDto(
    IReadOnlyList<string> Providers,
    IReadOnlyList<string> Models,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Outcomes,
    IReadOnlyList<string> Prompts,
    IReadOnlyList<string> Workflows,
    IReadOnlyList<string> ConfigurationRevisions,
    IReadOnlyList<string> EventTypes);

public sealed record CreateAnalyticsExportRequest(
    Guid? EnvironmentId,
    DateTimeOffset From,
    DateTimeOffset To,
    string? Provider,
    string? Model,
    string? Capability,
    string? Outcome);

public sealed record AnalyticsExportDto(
    Guid Id,
    string Status,
    string FileName,
    long? RowCount,
    long? SizeBytes,
    string? Checksum,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? CompletedAt);

public interface IAnalyticsService
{
    Task<AnalyticsFilterOptionsDto> FilterOptionsAsync(AnalyticsQuery query, CancellationToken ct = default);
    Task<AnalyticsDashboardDto> DashboardAsync(string category, AnalyticsQuery query, CancellationToken ct = default);
    Task<AnalyticsEventPageDto> EventsAsync(AnalyticsQuery query, int take, string? cursor, bool includeActor, CancellationToken ct = default);
    Task<AnalyticsEventDto> EventAsync(Guid workspaceId, Guid eventId, bool includeActor, CancellationToken ct = default);
    Task<IReadOnlyList<AnalyticsEventDto>> CorrelationAsync(Guid workspaceId, string correlationId, bool includeActor, CancellationToken ct = default);
    Task<AnalyticsExportDto> CreateExportAsync(Guid workspaceId, Guid actorId, CreateAnalyticsExportRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AnalyticsExportDto>> ExportsAsync(Guid workspaceId, CancellationToken ct = default);
    Task<AnalyticsExportDto> ExportAsync(Guid workspaceId, Guid exportId, CancellationToken ct = default);
    Task<(byte[] Content, string FileName)> DownloadExportAsync(Guid workspaceId, Guid exportId, CancellationToken ct = default);
}
