using System.Text.Json;
using ConvoLab.Application.Simulation;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.Analytics;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using ConvoLab.Domain.Analytics;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.Simulation;

public sealed class EfConversationSimulationStore : IConversationSimulationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly WorkspaceRequestContext? _runtime;

    public EfConversationSimulationStore(ApplicationDbContext db, WorkspaceRequestContext? runtime = null)
        => (_db, _runtime) = (db, runtime);

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
        var priorRunIds = record is null
            ? new HashSet<Guid>()
            : (JsonSerializer.Deserialize<SimulationConversation>(record.Payload, JsonOptions)?.Runs
                .Select(run => run.Id).ToHashSet() ?? []);
        if (record is null)
        {
            record = new SimulationRecord { Id = state.Id };
            _db.Simulations.Add(record);
        }
        record.Payload = JsonSerializer.Serialize(snapshot, JsonOptions);
        record.UpdatedAt = snapshot.UpdatedAt;
        if (_runtime?.EnvironmentId is Guid environmentId
            && _runtime.WorkspaceId is Guid workspaceId
            && _runtime.OrganisationId is Guid organisationId)
        {
            foreach (var run in snapshot.Runs.Where(item => !priorRunIds.Contains(item.Id)))
                await AddAnalyticsAsync(snapshot, run, organisationId, workspaceId, environmentId, cancellationToken);
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task AddAnalyticsAsync(
        SimulationConversation simulation,
        SimulationRun run,
        Guid organisationId,
        Guid workspaceId,
        Guid environmentId,
        CancellationToken ct)
    {
        var configuration = run.Configuration;
        var values = new SortedDictionary<string, string?>(StringComparer.Ordinal)
        {
            ["workflow"] = configuration?.Workflow,
            ["promptVersion"] = configuration?.PromptVersion,
            ["knowledgeCollection"] = configuration?.KnowledgeCollection,
            ["provider"] = configuration?.Provider,
            ["model"] = configuration?.Model,
            ["temperature"] = configuration?.Temperature.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["maxOutputTokens"] = configuration?.MaxOutputTokens.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["mode"] = configuration?.Mode.ToString()
        };
        var revision = AnalyticsKeys.ConfigurationRevision(values);
        if (!await _db.ConfigurationSnapshots.AnyAsync(item =>
            item.OrganisationId == organisationId && item.WorkspaceId == workspaceId
            && item.EnvironmentId == environmentId && item.Revision == revision, ct))
        {
            _db.ConfigurationSnapshots.Add(new ConfigurationSnapshotRecord
            {
                Id = Guid.NewGuid(), OrganisationId = organisationId, WorkspaceId = workspaceId,
                EnvironmentId = environmentId, Revision = revision,
                ValuesJson = JsonSerializer.Serialize(values, JsonOptions),
                ProvenanceJson = JsonSerializer.Serialize(values.Keys.ToDictionary(key => key, _ => "ExecutionOverride"), JsonOptions),
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        _db.ExecutionAttributions.Add(new ExecutionAttributionRecord
        {
            Id = Guid.NewGuid(), OrganisationId = organisationId, WorkspaceId = workspaceId,
            EnvironmentId = environmentId, ActorId = _runtime?.ActorId,
            ActorType = _runtime?.ActorType ?? "System", ActorRole = _runtime?.Role,
            SourceResourceType = "SimulationRun", SourceResourceId = run.Id,
            ConfigurationRevision = revision, CorrelationId = _runtime?.CorrelationId ?? string.Empty,
            AttributionStatus = "Original", CreatedAt = run.CreatedAt
        });

        var prevented = run.Timeline.Any(item => item.Capability == "Policy" && item.Status == "Denied");
        var cost = AnalyticsCost.Classify(
            run.Metrics?.ActualCost,
            run.Metrics?.InputTokens,
            run.Metrics?.OutputTokens,
            null,
            null,
            revision,
            prevented);
        var eventKey = AnalyticsKeys.Event("SimulationRun", run.Id, "SimulationExecution");
        var analyticsEvent = new AnalyticsEventRecord
        {
            Id = Guid.NewGuid(), EventKey = eventKey, OrganisationId = organisationId,
            WorkspaceId = workspaceId, EnvironmentId = environmentId,
            ActorId = _runtime?.ActorId, ActorType = _runtime?.ActorType ?? "System", ActorRole = _runtime?.Role,
            Capability = "Simulation", EventType = "SimulationExecution",
            Outcome = prevented ? "Denied" : run.Status == "Completed" ? "Succeeded" : "Failed",
            Provider = configuration?.Provider ?? run.ExecutionPlan?.Provider,
            Model = configuration?.Model ?? run.ExecutionPlan?.Model,
            InputTokens = prevented ? 0 : run.Metrics?.InputTokens,
            OutputTokens = prevented ? 0 : run.Metrics?.OutputTokens,
            CostZar = cost.AmountZar, CostType = cost.Type.ToString(), PricingRevision = cost.PricingRevision,
            DurationMs = run.Metrics?.TotalDurationMs,
            QualityScore = (run.Evaluation.Groundedness + run.Evaluation.Relevance + run.Evaluation.Safety) / 3,
            ProviderInvocationPrevented = prevented, SourceType = "SimulationRun", SourceId = run.Id,
            PromptName = configuration?.PromptVersion, WorkflowName = configuration?.Workflow,
            ConfigurationRevision = revision, CorrelationId = _runtime?.CorrelationId ?? string.Empty,
            OccurredAt = run.CreatedAt
        };
        _db.AnalyticsOutbox.Add(new AnalyticsOutboxRecord
        {
            Id = Guid.NewGuid(), EventKey = eventKey,
            PayloadJson = JsonSerializer.Serialize(analyticsEvent, JsonOptions),
            Status = "Pending", AvailableAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static SimulationState ToState(SimulationRecord record)
    {
        var snapshot = JsonSerializer.Deserialize<SimulationConversation>(record.Payload, JsonOptions)
            ?? throw new InvalidOperationException($"Simulation '{record.Id}' could not be deserialized.");
        return SimulationState.FromSnapshot(snapshot);
    }
}
