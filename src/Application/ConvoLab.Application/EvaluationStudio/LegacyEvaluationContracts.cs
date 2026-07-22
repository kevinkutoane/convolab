namespace ConvoLab.Application.EvaluationStudio;

public sealed record LegacyEvaluationPolicyDto(
    double MinimumGroundedness,
    double MinimumRelevance,
    double MinimumSafety,
    double MinimumOverallScore,
    string FailureAction);

public sealed record LegacyEvaluationScorecardDto(
    Guid Id,
    string Name,
    string Description,
    double MinimumGroundedness,
    double MinimumRelevance,
    double MinimumSafety,
    double MinimumOverallScore,
    string FailureAction,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateLegacyEvaluationScorecardCommand(
    string Name,
    string Description,
    double MinimumGroundedness,
    double MinimumRelevance,
    double MinimumSafety,
    double MinimumOverallScore,
    string FailureAction);

public sealed record LegacyEvaluationMetricSummaryDto(
    string Name,
    double Average,
    double Minimum,
    double Maximum,
    double Threshold,
    int Passing,
    int Failing);

public sealed record LegacyEvaluationRunDto(
    Guid SimulationId,
    string SimulationTitle,
    Guid RunId,
    string Provider,
    string Model,
    string Status,
    double Groundedness,
    double Relevance,
    double Safety,
    double OverallScore,
    string Verdict,
    bool Passed,
    IReadOnlyList<string> FailedGates,
    DateTimeOffset CreatedAt);

public sealed record LegacyEvaluationDailyTrendDto(
    DateOnly Date,
    int Runs,
    double AverageScore,
    double PassRate);

public sealed record LegacyEvaluationOverviewDto(
    int TotalRuns,
    int EvaluatedRuns,
    int PassingRuns,
    int FailingRuns,
    double PassRate,
    double AverageOverallScore,
    LegacyEvaluationPolicyDto Policy,
    IReadOnlyList<LegacyEvaluationMetricSummaryDto> Metrics,
    IReadOnlyList<LegacyEvaluationDailyTrendDto> DailyTrend,
    IReadOnlyList<LegacyEvaluationRunDto> RecentRuns,
    DateTimeOffset GeneratedAt);

public sealed record LegacyEvaluationPreviewCommand(
    double Groundedness,
    double Relevance,
    double Safety,
    Guid? ScorecardId = null,
    double? MinimumGroundedness = null,
    double? MinimumRelevance = null,
    double? MinimumSafety = null,
    double? MinimumOverallScore = null);

public sealed record LegacyEvaluationPreviewDto(
    double Groundedness,
    double Relevance,
    double Safety,
    double OverallScore,
    bool Passed,
    string Verdict,
    IReadOnlyList<string> FailedGates,
    IReadOnlyList<LegacyEvaluationGateDecisionDto> Decisions);

public sealed record LegacyEvaluationGateDecisionDto(
    string Name,
    double Score,
    double Threshold,
    string Status);

public interface IEvaluationStudioConfiguration
{
    LegacyEvaluationPolicyDto GetPolicy();
}

public interface ILegacyEvaluationStudioService
{
    Task<LegacyEvaluationOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LegacyEvaluationRunDto>> ListRunsAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LegacyEvaluationScorecardDto>> ListScorecardsAsync(CancellationToken cancellationToken = default);
    Task<LegacyEvaluationScorecardDto> CreateScorecardAsync(CreateLegacyEvaluationScorecardCommand command, CancellationToken cancellationToken = default);
    Task<LegacyEvaluationPreviewDto> PreviewAsync(LegacyEvaluationPreviewCommand command, CancellationToken cancellationToken = default);
}
