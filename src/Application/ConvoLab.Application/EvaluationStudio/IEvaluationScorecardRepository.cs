namespace ConvoLab.Application.EvaluationStudio;

public sealed record LegacyEvaluationScorecardState(
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

public interface IEvaluationScorecardRepository
{
    Task<IReadOnlyList<LegacyEvaluationScorecardState>> ListAsync(CancellationToken cancellationToken = default);
    Task<LegacyEvaluationScorecardState?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default);
    Task AddAsync(LegacyEvaluationScorecardState scorecard, CancellationToken cancellationToken = default);
}
