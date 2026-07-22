using ConvoLab.Application.Simulation;

namespace ConvoLab.Application.EvaluationStudio;

public sealed record EvaluationMetricDefinitionDto(
    Guid Id,
    string Key,
    string DisplayName,
    string Description,
    double Weight,
    double Threshold,
    bool Required);

public sealed record EvaluationScorecardDto(
    Guid Id,
    string Name,
    string Description,
    string Status,
    string Version,
    double QualityGateThreshold,
    bool IsDefault,
    long Revision,
    IReadOnlyList<EvaluationMetricDefinitionDto> Metrics,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EvaluationMetricResultDto(
    Guid Id,
    string Key,
    string DisplayName,
    double Score,
    double Threshold,
    double Weight,
    bool Passed,
    string Detail);

public sealed record EvaluationRunDto(
    Guid Id,
    Guid SimulationId,
    string SimulationTitle,
    Guid SourceRunId,
    Guid ScorecardId,
    string ScorecardName,
    string ScorecardVersion,
    string Status,
    string Verdict,
    double OverallScore,
    IReadOnlyList<EvaluationMetricResultDto> Metrics,
    string? FailureReason,
    string ReviewStatus,
    string? ReviewNotes,
    string? Reviewer,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset CreatedAt);

public sealed record EvaluationTestCaseDto(
    Guid Id,
    string Name,
    string Description,
    Guid SimulationId,
    Guid SourceRunId,
    Guid? ScorecardId,
    string ExpectedVerdict,
    IReadOnlyList<string> Tags,
    string Status,
    long Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EvaluationBatchItemDto(
    Guid Id,
    Guid TestCaseId,
    string TestCaseName,
    Guid? EvaluationRunId,
    string Status,
    string ActualVerdict,
    string ExpectedVerdict,
    bool Passed,
    string Detail);

public sealed record EvaluationBatchDto(
    Guid Id,
    string Name,
    Guid ScorecardId,
    string ScorecardName,
    string Status,
    int TotalCases,
    int PassedCases,
    double PassRate,
    IReadOnlyList<EvaluationBatchItemDto> Items,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record EvaluationMetricsDto(
    int TotalRuns,
    int PassedRuns,
    int ReviewRuns,
    int FailedRuns,
    double PassRate,
    double AverageScore,
    int PublishedScorecards,
    int TestCases,
    int RegressionCount);

public sealed record EvaluationDailyQualityDto(
    DateOnly Date,
    int Runs,
    double AverageScore,
    double PassRate);

public sealed record EvaluationOverviewDto(
    EvaluationMetricsDto Metrics,
    IReadOnlyList<EvaluationDailyQualityDto> QualityTrend,
    IReadOnlyList<EvaluationScorecardDto> Scorecards,
    IReadOnlyList<EvaluationRunDto> RecentRuns,
    IReadOnlyList<EvaluationTestCaseDto> TestCases,
    IReadOnlyList<EvaluationBatchDto> RecentBatches,
    DateTimeOffset GeneratedAt);

public sealed record CreateEvaluationMetricCommand(
    string Key,
    string DisplayName,
    string Description,
    double Weight,
    double Threshold,
    bool Required = true);

public sealed record CreateEvaluationScorecardCommand(
    string Name,
    string Description,
    string Version,
    double QualityGateThreshold,
    IReadOnlyList<CreateEvaluationMetricCommand>? Metrics = null,
    bool IsDefault = false);

public sealed record EvaluateSimulationRunCommand(
    Guid SimulationId,
    Guid SourceRunId,
    Guid? ScorecardId = null);

public sealed record ReviewEvaluationRunCommand(
    string Status,
    string Reviewer,
    string? Notes = null);

public sealed record CreateEvaluationTestCaseCommand(
    string Name,
    string Description,
    Guid SimulationId,
    Guid SourceRunId,
    Guid? ScorecardId,
    string ExpectedVerdict,
    IReadOnlyList<string>? Tags = null);

public sealed record RunEvaluationBatchCommand(
    string Name,
    Guid ScorecardId,
    IReadOnlyList<Guid> TestCaseIds);

public sealed record EvaluationComparisonMetricDto(
    string Key,
    string DisplayName,
    double BaselineScore,
    double CandidateScore,
    double Delta,
    string Direction);

public sealed record EvaluationComparisonDto(
    EvaluationRunDto Baseline,
    EvaluationRunDto Candidate,
    double OverallDelta,
    string Outcome,
    IReadOnlyList<EvaluationComparisonMetricDto> Metrics,
    IReadOnlyList<string> Findings);

public sealed record EvaluationScorecardState(
    Guid Id,
    string Name,
    string Description,
    string Status,
    string Version,
    double QualityGateThreshold,
    bool IsDefault,
    long Revision,
    IReadOnlyList<EvaluationMetricDefinitionState> Metrics,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EvaluationMetricDefinitionState(
    Guid Id,
    Guid ScorecardId,
    string Key,
    string DisplayName,
    string Description,
    double Weight,
    double Threshold,
    bool Required);

public sealed record EvaluationRunState(
    Guid Id,
    Guid SimulationId,
    string SimulationTitle,
    Guid SourceRunId,
    Guid ScorecardId,
    string ScorecardName,
    string ScorecardVersion,
    string Status,
    string Verdict,
    double OverallScore,
    IReadOnlyList<EvaluationMetricResultState> Metrics,
    string? FailureReason,
    string ReviewStatus,
    string? ReviewNotes,
    string? Reviewer,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset CreatedAt);

public sealed record EvaluationMetricResultState(
    Guid Id,
    Guid EvaluationRunId,
    string Key,
    string DisplayName,
    double Score,
    double Threshold,
    double Weight,
    bool Passed,
    string Detail);

public sealed record EvaluationTestCaseState(
    Guid Id,
    string Name,
    string Description,
    Guid SimulationId,
    Guid SourceRunId,
    Guid? ScorecardId,
    string ExpectedVerdict,
    IReadOnlyList<string> Tags,
    string Status,
    long Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EvaluationBatchState(
    Guid Id,
    string Name,
    Guid ScorecardId,
    string ScorecardName,
    string Status,
    IReadOnlyList<EvaluationBatchItemState> Items,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record EvaluationBatchItemState(
    Guid Id,
    Guid BatchId,
    Guid TestCaseId,
    string TestCaseName,
    Guid? EvaluationRunId,
    string Status,
    string ActualVerdict,
    string ExpectedVerdict,
    bool Passed,
    string Detail);

public interface IEvaluationStudioRepository
{
    Task BackfillLegacyScorecardsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvaluationScorecardState>> ListScorecardsAsync(CancellationToken cancellationToken = default);
    Task<EvaluationScorecardState?> GetScorecardAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddScorecardAsync(EvaluationScorecardState scorecard, CancellationToken cancellationToken = default);
    Task UpdateScorecardAsync(EvaluationScorecardState scorecard, long expectedRevision, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvaluationRunState>> ListRunsAsync(int limit = 250, CancellationToken cancellationToken = default);
    Task<EvaluationRunState?> GetRunAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EvaluationRunState?> GetRunBySourceAsync(Guid sourceRunId, Guid scorecardId, CancellationToken cancellationToken = default);
    Task AddRunAsync(EvaluationRunState run, CancellationToken cancellationToken = default);
    Task UpdateRunReviewAsync(Guid id, string status, string reviewer, string? notes, DateTimeOffset reviewedAt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvaluationTestCaseState>> ListTestCasesAsync(CancellationToken cancellationToken = default);
    Task<EvaluationTestCaseState?> GetTestCaseAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddTestCaseAsync(EvaluationTestCaseState testCase, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvaluationBatchState>> ListBatchesAsync(int limit = 25, CancellationToken cancellationToken = default);
    Task AddBatchAsync(EvaluationBatchState batch, CancellationToken cancellationToken = default);
}

public interface IEvaluationStudioService
{
    Task<EvaluationOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvaluationScorecardDto>> ListScorecardsAsync(CancellationToken cancellationToken = default);
    Task<EvaluationScorecardDto> GetScorecardAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EvaluationScorecardDto> CreateScorecardAsync(CreateEvaluationScorecardCommand command, CancellationToken cancellationToken = default);
    Task<EvaluationScorecardDto> PublishScorecardAsync(Guid id, long revision, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvaluationRunDto>> ListRunsAsync(int limit = 250, CancellationToken cancellationToken = default);
    Task<EvaluationRunDto> EvaluateRunAsync(EvaluateSimulationRunCommand command, CancellationToken cancellationToken = default);
    Task<EvaluationRunDto> RecordSimulationRunAsync(Guid simulationId, string simulationTitle, SimulationRun run, Guid? scorecardId = null, CancellationToken cancellationToken = default);
    Task<EvaluationRunDto> ReviewRunAsync(Guid id, ReviewEvaluationRunCommand command, CancellationToken cancellationToken = default);
    Task<EvaluationComparisonDto> CompareRunsAsync(Guid baselineId, Guid candidateId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvaluationTestCaseDto>> ListTestCasesAsync(CancellationToken cancellationToken = default);
    Task<EvaluationTestCaseDto> CreateTestCaseAsync(CreateEvaluationTestCaseCommand command, CancellationToken cancellationToken = default);
    Task<EvaluationBatchDto> RunBatchAsync(RunEvaluationBatchCommand command, CancellationToken cancellationToken = default);
}
