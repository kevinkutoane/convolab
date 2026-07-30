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

    private Task AddAnalyticsAsync(
        SimulationConversation simulation,
        SimulationRun run,
        Guid organisationId,
        Guid workspaceId,
        Guid environmentId,
        CancellationToken ct)
    {
        var configuration = run.Configuration;
        var revision = string.IsNullOrWhiteSpace(configuration?.ConfigurationRevision)
            ? "legacy:configuration-unavailable"
            : configuration.ConfigurationRevision;
        var correlationId = string.IsNullOrWhiteSpace(configuration?.CorrelationId)
            ? _runtime?.CorrelationId ?? string.Empty
            : configuration.CorrelationId;

        _db.ExecutionAttributions.Add(new ExecutionAttributionRecord
        {
            Id = Guid.NewGuid(), OrganisationId = organisationId, WorkspaceId = workspaceId,
            EnvironmentId = environmentId, ActorId = _runtime?.ActorId,
            ActorType = _runtime?.ActorType ?? "System", ActorRole = _runtime?.Role,
            SourceResourceType = "SimulationRun", SourceResourceId = run.Id,
            ConfigurationRevision = revision, CorrelationId = correlationId,
            AttributionStatus = revision.StartsWith("legacy:", StringComparison.Ordinal)
                ? "ConfigurationUnavailable"
                : "Original",
            CreatedAt = run.CreatedAt
        });

        var prevented = run.Timeline.Any(item => item.Capability == "Policy" && item.Status == "Denied");
        var provider = configuration?.Provider ?? run.ExecutionPlan?.Provider;
        var model = configuration?.Model ?? run.ExecutionPlan?.Model;
        decimal? actualCost = provider?.Equals("Gemini", StringComparison.OrdinalIgnoreCase) == true
            && run.Metrics?.ActualCost > 0
                ? run.Metrics.ActualCost
                : null;
        var cost = AnalyticsCost.Classify(
            actualCost,
            run.Metrics?.InputTokens,
            run.Metrics?.OutputTokens,
            configuration?.InputPriceZarPer1K,
            configuration?.OutputPriceZarPer1K,
            revision,
            prevented);

        var policyStep = run.Timeline.FirstOrDefault(item => item.Capability == "Policy");
        var providerStep = run.Timeline.FirstOrDefault(item =>
            item.Capability == "Intelligence" && item.Name == "Model execution");
        var evaluationStep = run.Timeline.FirstOrDefault(item => item.Capability == "Evaluation");
        var policyOutcome = prevented ? "Denied" : policyStep is null ? null : "Allowed";
        var evaluationOutcome = run.Evaluation.Verdict;

        void AddEvent(
            string eventType,
            string capability,
            string outcome,
            DateTimeOffset occurredAt,
            Guid sourceId,
            string sourceType,
            int? inputTokens = null,
            int? outputTokens = null,
            AnalyticsCost? eventCost = null,
            double? durationMs = null,
            double? qualityScore = null,
            bool invocationPrevented = false,
            string? eventPolicyOutcome = null,
            string? eventEvaluationOutcome = null)
        {
            var eventKey = AnalyticsKeys.Event(sourceType, sourceId, eventType);
            var analyticsEvent = new AnalyticsEventRecord
            {
                Id = Guid.NewGuid(), EventKey = eventKey, OrganisationId = organisationId,
                WorkspaceId = workspaceId, EnvironmentId = environmentId,
                ActorId = _runtime?.ActorId, ActorType = _runtime?.ActorType ?? "System", ActorRole = _runtime?.Role,
                Capability = capability, EventType = eventType, Outcome = outcome,
                Provider = provider, Model = model, InputTokens = inputTokens, OutputTokens = outputTokens,
                CostZar = eventCost?.AmountZar, CostType = eventCost?.Type.ToString() ?? "Unavailable",
                PricingRevision = eventCost?.PricingRevision, DurationMs = durationMs, QualityScore = qualityScore,
                Groundedness = qualityScore.HasValue ? run.Evaluation.Groundedness : null,
                Relevance = qualityScore.HasValue ? run.Evaluation.Relevance : null,
                Safety = qualityScore.HasValue ? run.Evaluation.Safety : null,
                OverallQuality = qualityScore,
                ProviderInvocationPrevented = invocationPrevented, SourceExecutionId = run.Id,
                SourceType = sourceType, SourceId = sourceId,
                PromptName = configuration?.PromptVersion, WorkflowName = configuration?.Workflow,
                KnowledgeCollectionName = configuration?.KnowledgeCollection,
                PolicyOutcome = eventPolicyOutcome, EvaluationOutcome = eventEvaluationOutcome,
                ConfigurationRevision = revision, CorrelationId = correlationId, OccurredAt = occurredAt
            };
            _db.AnalyticsOutbox.Add(new AnalyticsOutboxRecord
            {
                Id = Guid.NewGuid(), EventKey = eventKey,
                PayloadJson = JsonSerializer.Serialize(analyticsEvent, JsonOptions),
                Status = "Pending", AvailableAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow
            });
        }

        var replayExecution = run.ReplayedFromRunId.HasValue;
        AddEvent(
            replayExecution ? "ReplayStarted" : "SimulationStarted",
            replayExecution ? "Replay" : "Simulation",
            "Started",
            run.CreatedAt,
            run.Id,
            "SimulationRun");
        if (policyStep is not null && configuration?.PolicyEnforcementEnabled == false)
            AddEvent("PolicyEnforcementBypassed", "Policy", "Bypassed", policyStep.StartedAt,
                run.Id, "SimulationRun", durationMs: policyStep.DurationMs,
                eventPolicyOutcome: "Bypassed");

        if (prevented)
        {
            AddEvent("ProviderInvocationPrevented", "Provider", "Denied",
                policyStep?.StartedAt ?? run.CreatedAt, run.Id, "SimulationRun",
                0, 0, cost, invocationPrevented: true, eventPolicyOutcome: "Denied");
        }
        else if (providerStep is not null)
        {
            AddEvent("ProviderInvocationStarted", "Provider", "Started", providerStep.StartedAt,
                run.Id, "SimulationRun");
            AddEvent(
                providerStep.Status == "Completed" ? "ProviderInvocationCompleted" : "ProviderInvocationFailed",
                "Provider",
                providerStep.Status == "Completed" ? "Succeeded" : "Failed",
                providerStep.StartedAt.AddMilliseconds(providerStep.DurationMs),
                run.Id,
                "SimulationRun",
                run.Metrics?.InputTokens,
                run.Metrics?.OutputTokens,
                cost,
                run.Metrics?.ProviderLatencyMs ?? providerStep.DurationMs,
                eventPolicyOutcome: policyOutcome);
        }

        if (evaluationStep is not null)
        {
            var quality = (run.Evaluation.Groundedness + run.Evaluation.Relevance + run.Evaluation.Safety) / 3;
            AddEvent(
                run.Evaluation.Verdict == "Passed" ? "QualityGatePassed" : "QualityGateFailed",
                "Evaluation",
                run.Evaluation.Verdict,
                evaluationStep.StartedAt.AddMilliseconds(evaluationStep.DurationMs),
                run.Id,
                "SimulationRun",
                qualityScore: quality,
                eventEvaluationOutcome: evaluationOutcome);
        }

        AddEvent(
            replayExecution
                ? run.Status == "Completed" ? "ReplayCompleted" : "ReplayFailed"
                : run.Status == "Completed" ? "SimulationCompleted" : "SimulationFailed",
            replayExecution ? "Replay" : "Simulation",
            prevented ? "Denied" : run.Status == "Completed" ? "Succeeded" : "Failed",
            run.CreatedAt.AddMilliseconds(run.Metrics?.TotalDurationMs ?? 0),
            run.Id,
            "SimulationRun",
            durationMs: run.Metrics?.TotalDurationMs,
            eventPolicyOutcome: policyOutcome,
            eventEvaluationOutcome: evaluationOutcome);

        return Task.CompletedTask;
    }

    private static SimulationState ToState(SimulationRecord record)
    {
        var snapshot = JsonSerializer.Deserialize<SimulationConversation>(record.Payload, JsonOptions)
            ?? throw new InvalidOperationException($"Simulation '{record.Id}' could not be deserialized.");
        return SimulationState.FromSnapshot(snapshot);
    }
}
