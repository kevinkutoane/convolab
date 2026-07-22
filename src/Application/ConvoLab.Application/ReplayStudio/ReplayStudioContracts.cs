using ConvoLab.Application.Simulation;

namespace ConvoLab.Application.ReplayStudio;

public sealed record ReplayMetricsDto(
    int TotalExperiments,
    int ActiveExperiments,
    int TotalCandidates,
    int ImprovedCandidates,
    int RegressionCandidates,
    double AverageQualityDelta,
    double AverageLatencyDeltaMs,
    decimal AverageCostDelta,
    string Currency);

public sealed record ReplaySourceDto(
    Guid SimulationId,
    string SimulationTitle,
    Guid RunId,
    Guid? ReplayedFromRunId,
    string Status,
    string UserMessage,
    string? Response,
    ReplayRunSnapshotDto Snapshot,
    DateTimeOffset CreatedAt);

public sealed record ReplayExperimentSummaryDto(
    Guid Id,
    string Name,
    Guid SimulationId,
    string SimulationTitle,
    Guid SourceRunId,
    string Status,
    int CandidateCount,
    Guid? BestCandidateId,
    double BestQualityDelta,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ReplayExperimentDetailDto(
    ReplayExperimentSummaryDto Summary,
    ReplaySourceDto Baseline,
    IReadOnlyList<ReplayCandidateDto> Candidates);

public sealed record ReplayCandidateDto(
    Guid Id,
    Guid ExperimentId,
    Guid RunId,
    string Label,
    string Status,
    ReplayConfigurationDto Configuration,
    ReplayRunSnapshotDto Snapshot,
    ReplayComparisonDto Comparison,
    DateTimeOffset CreatedAt);

public sealed record ReplayConfigurationDto(
    string Workflow,
    string PromptVersion,
    string KnowledgeCollection,
    string Provider,
    string Model,
    double Temperature,
    int MaxOutputTokens,
    SimulationMode Mode);

public sealed record ReplayRunSnapshotDto(
    Guid RunId,
    string Status,
    string Workflow,
    string WorkflowVersion,
    string PromptVersion,
    string KnowledgeCollection,
    string Provider,
    string Model,
    SimulationMode Mode,
    double Temperature,
    int MaxOutputTokens,
    double QualityScore,
    double Groundedness,
    double Relevance,
    double Safety,
    string Verdict,
    double DurationMs,
    double ProviderLatencyMs,
    int TotalTokens,
    decimal ActualCost,
    string Currency,
    int Attempts,
    int FallbacksUsed,
    int CitationCount,
    int ResponseLength,
    string? Response,
    string? FailureReason,
    DateTimeOffset CreatedAt);

public sealed record ReplayComparisonDto(
    double QualityDelta,
    double GroundednessDelta,
    double RelevanceDelta,
    double SafetyDelta,
    double DurationDeltaMs,
    double ProviderLatencyDeltaMs,
    int TokenDelta,
    decimal CostDelta,
    int CitationDelta,
    int ResponseLengthDelta,
    string Outcome,
    IReadOnlyList<string> ChangedDimensions,
    IReadOnlyList<string> Findings);

public sealed record ReplayOverviewDto(
    ReplayMetricsDto Metrics,
    IReadOnlyList<ReplayExperimentSummaryDto> RecentExperiments,
    IReadOnlyList<ReplaySourceDto> RecentSources,
    SimulationOptions Options,
    DateTimeOffset GeneratedAt);

public sealed record CreateReplayExperimentCommand(
    string Name,
    Guid SimulationId,
    Guid SourceRunId,
    string CandidateLabel,
    string? Workflow = null,
    string? PromptVersion = null,
    string? KnowledgeCollection = null,
    string Provider = "Deterministic",
    string? Model = null,
    double Temperature = 0.2,
    int MaxOutputTokens = 400,
    SimulationMode Mode = SimulationMode.Normal);

public sealed record AddReplayCandidateCommand(
    string Label,
    string? Workflow = null,
    string? PromptVersion = null,
    string? KnowledgeCollection = null,
    string Provider = "Deterministic",
    string? Model = null,
    double Temperature = 0.2,
    int MaxOutputTokens = 400,
    SimulationMode Mode = SimulationMode.Normal);

public sealed record ReplayExperimentState(
    Guid Id,
    string Name,
    Guid SimulationId,
    Guid SourceRunId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ReplayCandidateState(
    Guid Id,
    Guid ExperimentId,
    Guid RunId,
    string Label,
    string Workflow,
    string PromptVersion,
    string KnowledgeCollection,
    string Provider,
    string Model,
    double Temperature,
    int MaxOutputTokens,
    SimulationMode Mode,
    DateTimeOffset CreatedAt);

public interface IReplayStudioRepository
{
    Task<IReadOnlyList<ReplayExperimentState>> ListExperimentsAsync(int limit = 250, CancellationToken cancellationToken = default);
    Task<ReplayExperimentState?> GetExperimentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ReplayExperimentState?> GetBySourceRunAsync(Guid simulationId, Guid sourceRunId, CancellationToken cancellationToken = default);
    Task AddExperimentAsync(ReplayExperimentState experiment, CancellationToken cancellationToken = default);
    Task UpdateExperimentAsync(ReplayExperimentState experiment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReplayCandidateState>> ListCandidatesAsync(Guid experimentId, CancellationToken cancellationToken = default);
    Task<ReplayCandidateState?> GetCandidateByRunAsync(Guid runId, CancellationToken cancellationToken = default);
    Task AddCandidateAsync(ReplayCandidateState candidate, CancellationToken cancellationToken = default);
}

public interface IReplayStudioService
{
    Task<ReplayOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReplaySourceDto>> ListSourcesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReplayExperimentSummaryDto>> ListExperimentsAsync(CancellationToken cancellationToken = default);
    Task<ReplayExperimentDetailDto> GetExperimentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ReplayExperimentDetailDto> CreateExperimentAsync(CreateReplayExperimentCommand command, CancellationToken cancellationToken = default);
    Task<ReplayExperimentDetailDto> AddCandidateAsync(Guid experimentId, AddReplayCandidateCommand command, CancellationToken cancellationToken = default);
    Task<ReplayExperimentDetailDto> CompleteAsync(Guid experimentId, CancellationToken cancellationToken = default);
    Task<ReplayExperimentDetailDto> ArchiveAsync(Guid experimentId, CancellationToken cancellationToken = default);
}
