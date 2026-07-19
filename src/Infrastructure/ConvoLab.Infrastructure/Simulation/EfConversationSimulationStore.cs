using System.Text.Json;
using ConvoLab.Application.Simulation;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.Simulation;

public sealed class EfConversationSimulationStore : IConversationSimulationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;

    public EfConversationSimulationStore(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<SimulationState>> ListAsync(CancellationToken cancellationToken = default)
    {
        // SQLite cannot translate ORDER BY for DateTimeOffset values. Materialize
        // first so the same store works with both the local SQLite provider and
        // PostgreSQL without changing the persisted timestamp contract.
        var records = await _db.Simulations.AsNoTracking().ToListAsync(cancellationToken);
        return records.OrderByDescending(x => x.UpdatedAt).Select(ToState).ToList();
    }

    public async Task<SimulationState?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _db.Simulations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return record is null ? null : ToState(record);
    }

    public async Task<SimulationState> AddAsync(CreateSimulationCommand command, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var state = new SimulationState(Guid.NewGuid(), string.IsNullOrWhiteSpace(command.Title) ? "Untitled simulation" : command.Title.Trim(), command.Workflow, command.PromptVersion, command.KnowledgeCollection, now);
        await UpsertAsync(state, cancellationToken);
        return state;
    }

    public Task SaveAsync(SimulationState state, CancellationToken cancellationToken = default) => UpsertAsync(state, cancellationToken);

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _db.Simulations.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (record is null) return false;
        _db.Simulations.Remove(record);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task UpsertAsync(SimulationState state, CancellationToken cancellationToken)
    {
        var snapshot = state.Snapshot();
        var record = await _db.Simulations.SingleOrDefaultAsync(x => x.Id == state.Id, cancellationToken);
        if (record is null)
        {
            record = new SimulationRecord { Id = state.Id };
            _db.Simulations.Add(record);
        }
        record.Payload = JsonSerializer.Serialize(snapshot, JsonOptions);
        record.UpdatedAt = snapshot.UpdatedAt;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static SimulationState ToState(SimulationRecord record)
    {
        var snapshot = JsonSerializer.Deserialize<SimulationConversation>(record.Payload, JsonOptions)
            ?? throw new InvalidOperationException($"Simulation '{record.Id}' could not be deserialized.");
        return SimulationState.FromSnapshot(snapshot);
    }
}
