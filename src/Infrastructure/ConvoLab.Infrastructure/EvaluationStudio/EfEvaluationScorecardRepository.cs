using ConvoLab.Application.EvaluationStudio;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.EvaluationStudio;

public sealed class EfEvaluationScorecardRepository(ApplicationDbContext db)
    : IEvaluationScorecardRepository
{
    public async Task<IReadOnlyList<LegacyEvaluationScorecardState>> ListAsync(
        CancellationToken cancellationToken = default)
        => (await db.EvaluationScorecards.AsNoTracking().ToListAsync(cancellationToken))
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => Map(item)!)
            .ToList();

    public async Task<LegacyEvaluationScorecardState?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => Map(await db.EvaluationScorecards.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken));

    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default)
        => db.EvaluationScorecards.AsNoTracking()
            .AnyAsync(item => item.Name.ToLower() == name.ToLower(), cancellationToken);

    public Task AddAsync(
        LegacyEvaluationScorecardState scorecard,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        db.EvaluationScorecards.Add(new EvaluationScorecardRecord
        {
            Id = scorecard.Id,
            Name = scorecard.Name,
            Description = scorecard.Description,
            Status = "Published",
            Version = "1.0",
            QualityGateThreshold = scorecard.MinimumOverallScore,
            IsDefault = false,
            Revision = 1,
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

    private static LegacyEvaluationScorecardState? Map(EvaluationScorecardRecord? record)
        => record is null
            ? null
            : new LegacyEvaluationScorecardState(
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
