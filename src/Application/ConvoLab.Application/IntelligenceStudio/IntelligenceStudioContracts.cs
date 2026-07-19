namespace ConvoLab.Application.IntelligenceStudio;

public sealed record IntelligenceModelDefinition(
    string Key,
    string DisplayName,
    IReadOnlyList<string> Capabilities,
    int MaxContextTokens,
    int MaxOutputTokens,
    double TypicalLatencyMs,
    decimal? InputPricePer1K,
    decimal? OutputPricePer1K,
    string Currency);

public sealed record IntelligenceProviderDefinition(
    string Key,
    string DisplayName,
    bool IsConfigured,
    bool IsLive,
    string Status,
    string? ConfigurationHint,
    IReadOnlyList<IntelligenceModelDefinition> Models);

public sealed record IntelligenceMetricsDto(
    int TotalExecutions,
    int SuccessfulExecutions,
    double SuccessRate,
    double AverageLatencyMs,
    long TotalTokens,
    decimal TotalCost,
    string Currency,
    int RetryExecutions,
    int FallbackExecutions);

public sealed record IntelligenceProviderUsageDto(
    string Provider,
    int Executions,
    double SuccessRate,
    double AverageLatencyMs,
    long TotalTokens,
    decimal TotalCost,
    string Currency);

public sealed record IntelligenceDailyUsageDto(
    DateOnly Date,
    int Executions,
    long Tokens,
    decimal Cost,
    double AverageLatencyMs);

public sealed record IntelligenceBudgetDto(
    decimal Limit,
    decimal Consumed,
    decimal Remaining,
    double Utilisation,
    string Currency,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    string Status);

public sealed record IntelligenceExecutionDto(
    Guid SimulationId,
    string SimulationTitle,
    Guid RunId,
    string Status,
    string Mode,
    string Provider,
    string Model,
    int Attempts,
    int FallbacksUsed,
    int InputTokens,
    int OutputTokens,
    int TotalTokens,
    decimal Cost,
    string Currency,
    double DurationMs,
    double ProviderLatencyMs,
    double Groundedness,
    double Relevance,
    string Verdict,
    DateTimeOffset CreatedAt,
    string? FailureReason);

public sealed record IntelligenceOverviewDto(
    IntelligenceMetricsDto Metrics,
    IntelligenceBudgetDto Budget,
    IReadOnlyList<IntelligenceProviderDefinition> Providers,
    IReadOnlyList<IntelligenceProviderUsageDto> ProviderUsage,
    IReadOnlyList<IntelligenceDailyUsageDto> DailyUsage,
    IReadOnlyList<IntelligenceExecutionDto> RecentExecutions,
    DateTimeOffset GeneratedAt);

public sealed record ExecutionPlanPreviewCommand(
    string Provider,
    string Model,
    int EstimatedInputTokens,
    int MaxOutputTokens,
    bool Streaming,
    bool AllowFallback,
    int MaxAttempts,
    IReadOnlyList<string>? RequiredCapabilities = null);

public sealed record ExecutionPlanDecisionDto(string Name, string Status, string Detail);

public sealed record ExecutionPlanPreviewDto(
    string Provider,
    string Model,
    bool IsConfigured,
    bool CapabilityMatch,
    int EstimatedInputTokens,
    int EstimatedOutputTokens,
    long EstimatedTotalTokens,
    decimal? EstimatedCost,
    string Currency,
    double EstimatedLatencyMs,
    decimal BudgetRemaining,
    bool WithinBudget,
    IReadOnlyList<ExecutionPlanDecisionDto> Decisions,
    IReadOnlyList<string> Warnings);

public interface IIntelligenceStudioConfiguration
{
    IReadOnlyList<IntelligenceProviderDefinition> GetProviders();
    decimal MonthlyBudgetZar { get; }
}

public interface IIntelligenceStudioService
{
    Task<IntelligenceOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IntelligenceExecutionDto>> ListExecutionsAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task<ExecutionPlanPreviewDto> PreviewPlanAsync(ExecutionPlanPreviewCommand command, CancellationToken cancellationToken = default);
}
