namespace ConvoLab.Application.EvaluationStudio;

public sealed record EvaluationScorecardState(
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
    Task<IReadOnlyList<EvaluationScorecardState>> ListAsync(CancellationToken cancellationToken = default);
    Task<EvaluationScorecardState?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default);
    Task AddAsync(EvaluationScorecardState scorecard, CancellationToken cancellationToken = default);
}
