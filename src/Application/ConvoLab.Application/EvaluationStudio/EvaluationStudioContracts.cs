namespace ConvoLab.Application.EvaluationStudio;

public sealed record EvaluationPolicyDto(
    double MinimumGroundedness,
    double MinimumRelevance,
    double MinimumSafety,
    double MinimumOverallScore,
    string FailureAction);

public sealed record EvaluationScorecardDto(
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

public sealed record CreateEvaluationScorecardCommand(
    string Name,
    string Description,
    double MinimumGroundedness,
    double MinimumRelevance,
    double MinimumSafety,
    double MinimumOverallScore,
    string FailureAction);

public sealed record EvaluationMetricSummaryDto(
    string Name,
    double Average,
    double Minimum,
    double Maximum,
    double Threshold,
    int Passing,
    int Failing);

public sealed record EvaluationRunDto(
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

public sealed record EvaluationDailyTrendDto(
    DateOnly Date,
    int Runs,
    double AverageScore,
    double PassRate);

public sealed record EvaluationOverviewDto(
    int TotalRuns,
    int EvaluatedRuns,
    int PassingRuns,
    int FailingRuns,
    double PassRate,
    double AverageOverallScore,
    EvaluationPolicyDto Policy,
    IReadOnlyList<EvaluationMetricSummaryDto> Metrics,
    IReadOnlyList<EvaluationDailyTrendDto> DailyTrend,
    IReadOnlyList<EvaluationRunDto> RecentRuns,
    DateTimeOffset GeneratedAt);

public sealed record EvaluationPreviewCommand(
    double Groundedness,
    double Relevance,
    double Safety,
    Guid? ScorecardId = null,
    double? MinimumGroundedness = null,
    double? MinimumRelevance = null,
    double? MinimumSafety = null,
    double? MinimumOverallScore = null);

public sealed record EvaluationPreviewDto(
    double Groundedness,
    double Relevance,
    double Safety,
    double OverallScore,
    bool Passed,
    string Verdict,
    IReadOnlyList<string> FailedGates,
    IReadOnlyList<EvaluationGateDecisionDto> Decisions);

public sealed record EvaluationGateDecisionDto(
    string Name,
    double Score,
    double Threshold,
    string Status);

public interface IEvaluationStudioConfiguration
{
    EvaluationPolicyDto GetPolicy();
}

public interface IEvaluationStudioService
{
    Task<EvaluationOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvaluationRunDto>> ListRunsAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvaluationScorecardDto>> ListScorecardsAsync(CancellationToken cancellationToken = default);
    Task<EvaluationScorecardDto> CreateScorecardAsync(CreateEvaluationScorecardCommand command, CancellationToken cancellationToken = default);
    Task<EvaluationPreviewDto> PreviewAsync(EvaluationPreviewCommand command, CancellationToken cancellationToken = default);
}
