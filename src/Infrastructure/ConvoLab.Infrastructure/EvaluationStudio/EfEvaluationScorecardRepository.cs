using ConvoLab.Application.EvaluationStudio;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.EvaluationStudio;

public sealed class EfEvaluationScorecardRepository(ApplicationDbContext db)
    : IEvaluationScorecardRepository
{
    public async Task<IReadOnlyList<EvaluationScorecardState>> ListAsync(
        CancellationToken cancellationToken = default)
        => (await db.EvaluationScorecards.AsNoTracking().ToListAsync(cancellationToken))
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => Map(item)!)
            .ToList();

    public async Task<EvaluationScorecardState?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => Map(await db.EvaluationScorecards.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken));

    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default)
        => db.EvaluationScorecards.AsNoTracking()
            .AnyAsync(item => item.Name.ToLower() == name.ToLower(), cancellationToken);

    public Task AddAsync(
        EvaluationScorecardState scorecard,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        db.EvaluationScorecards.Add(new EvaluationScorecardRecord
        {
            Id = scorecard.Id,
            Name = scorecard.Name,
            Description = scorecard.Description,
            MinimumGroundedness = scorecard.MinimumGroundedness,
            MinimumRelevance = scorecard.MinimumRelevance,
            MinimumSafety = scorecard.MinimumSafety,
            MinimumOverallScore = scorecard.MinimumOverallScore,
            FailureAction = scorecard.FailureAction,
            CreatedAt = scorecard.CreatedAt,
            UpdatedAt = scorecard.UpdatedAt
        });
        return Task.CompletedTask;
    }

    private static EvaluationScorecardState? Map(EvaluationScorecardRecord? record)
        => record is null
            ? null
            : new EvaluationScorecardState(
                record.Id,
                record.Name,
                record.Description,
                record.MinimumGroundedness,
                record.MinimumRelevance,
                record.MinimumSafety,
                record.MinimumOverallScore,
                record.FailureAction,
                record.CreatedAt,
                record.UpdatedAt);
}
