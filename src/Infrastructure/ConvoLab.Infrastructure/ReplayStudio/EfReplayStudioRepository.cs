using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.ReplayStudio;
using ConvoLab.Application.Simulation;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.ReplayStudio;

public sealed class EfReplayStudioRepository(ApplicationDbContext db) : IReplayStudioRepository
{
    public async Task<IReadOnlyList<ReplayExperimentState>> ListExperimentsAsync(int limit = 250, CancellationToken cancellationToken = default)
        => (await db.ReplayExperiments.AsNoTracking().ToListAsync(cancellationToken))
            .OrderByDescending(item => item.UpdatedAt).Take(limit).Select(Map).ToList();

    public async Task<ReplayExperimentState?> GetExperimentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await db.ReplayExperiments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return record is null ? null : Map(record);
    }

    public async Task<ReplayExperimentState?> GetBySourceRunAsync(Guid simulationId, Guid sourceRunId, CancellationToken cancellationToken = default)
    {
        var records = await db.ReplayExperiments.AsNoTracking()
            .Where(item => item.SimulationId == simulationId && item.SourceRunId == sourceRunId)
            .ToListAsync(cancellationToken);
        var record = records.OrderByDescending(item => item.UpdatedAt).FirstOrDefault();
        return record is null ? null : Map(record);
    }

    public async Task AddExperimentAsync(ReplayExperimentState experiment, CancellationToken cancellationToken = default)
    {
        db.ReplayExperiments.Add(MapRecord(experiment));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateExperimentAsync(ReplayExperimentState experiment, CancellationToken cancellationToken = default)
    {
        var record = await db.ReplayExperiments.SingleOrDefaultAsync(item => item.Id == experiment.Id, cancellationToken)
            ?? throw new ResourceNotFoundException("replay.experiment.not_found", $"Replay experiment '{experiment.Id}' was not found.");
        record.Name = experiment.Name;
        record.Status = experiment.Status;
        record.UpdatedAt = experiment.UpdatedAt;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReplayCandidateState>> ListCandidatesAsync(Guid experimentId, CancellationToken cancellationToken = default)
        => (await db.ReplayCandidates.AsNoTracking().Where(item => item.ExperimentId == experimentId && db.ReplayExperiments.Any(experiment => experiment.Id == item.ExperimentId))
                .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.CreatedAt).Select(Map).ToList();

    public async Task<ReplayCandidateState?> GetCandidateByRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var record = await db.ReplayCandidates.AsNoTracking().SingleOrDefaultAsync(item => item.RunId == runId && db.ReplayExperiments.Any(experiment => experiment.Id == item.ExperimentId), cancellationToken);
        return record is null ? null : Map(record);
    }

    public async Task AddCandidateAsync(ReplayCandidateState candidate, CancellationToken cancellationToken = default)
    {
        if (await db.ReplayCandidates.AsNoTracking().AnyAsync(item => item.RunId == candidate.RunId, cancellationToken)) return;
        db.ReplayCandidates.Add(MapRecord(candidate));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var exists = await db.ReplayCandidates.AsNoTracking().AnyAsync(item => item.RunId == candidate.RunId, cancellationToken);
            if (!exists) throw;
            db.ChangeTracker.Clear();
        }
    }

    private static ReplayExperimentState Map(ReplayExperimentRecord record)
        => new(record.Id, record.Name, record.SimulationId, record.SourceRunId, record.Status, record.CreatedAt, record.UpdatedAt);

    private static ReplayCandidateState Map(ReplayCandidateRecord record)
        => new(record.Id, record.ExperimentId, record.RunId, record.Label, record.Workflow, record.PromptVersion,
            record.KnowledgeCollection, record.Provider, record.Model, record.Temperature, record.MaxOutputTokens,
            Enum.TryParse<SimulationMode>(record.Mode, true, out var mode) ? mode : SimulationMode.Normal, record.CreatedAt);

    private static ReplayExperimentRecord MapRecord(ReplayExperimentState state) => new()
    {
        Id = state.Id,
        Name = state.Name,
        SimulationId = state.SimulationId,
        SourceRunId = state.SourceRunId,
        Status = state.Status,
        CreatedAt = state.CreatedAt,
        UpdatedAt = state.UpdatedAt
    };

    private static ReplayCandidateRecord MapRecord(ReplayCandidateState state) => new()
    {
        Id = state.Id,
        ExperimentId = state.ExperimentId,
        RunId = state.RunId,
        Label = state.Label,
        Workflow = state.Workflow,
        PromptVersion = state.PromptVersion,
        KnowledgeCollection = state.KnowledgeCollection,
        Provider = state.Provider,
        Model = state.Model,
        Temperature = state.Temperature,
        MaxOutputTokens = state.MaxOutputTokens,
        Mode = state.Mode.ToString(),
        CreatedAt = state.CreatedAt
    };
}
