using ConvoLab.Application.Simulation;
using ConvoLab.Application.TraceStudio;

namespace ConvoLab.Application.Tests.TraceStudio;

public sealed class TraceStudioServiceTests
{
    [Fact]
    public async Task Simulator_run_is_synchronized_into_trace_with_spans_and_artifacts()
    {
        var run = CreateRun();
        var simulation = new SimulationState(Guid.NewGuid(), "Claims trace", "Workflow", "Prompt", "Knowledge", DateTimeOffset.UtcNow);
        simulation.AddMessage("assistant", "Governed response", false);
        simulation.AddRun(run);
        var repository = new InMemoryTraceRepository();
        var service = new TraceStudioService(repository, new TestSimulationStore([simulation]));

        var overview = await service.GetOverviewAsync();

        var summary = Assert.Single(overview.RecentTraces);
        Assert.Equal(run.Id, summary.SourceRunId);
        Assert.Equal("Completed", summary.Status);
        Assert.True(summary.SpanCount >= run.Timeline.Count + 1);
        var detail = await service.GetAsync(summary.Id);
        Assert.Contains(detail.Artifacts, item => item.Kind == "Prompt" && item.IsRedacted);
        Assert.Contains(detail.Spans, item => item.Capability == "Intelligence");
    }

    [Fact]
    public async Task Sensitive_artifacts_are_redacted_until_explicitly_requested()
    {
        var repository = new InMemoryTraceRepository();
        var service = new TraceStudioService(repository, new TestSimulationStore([]));
        var run = CreateRun();
        var recorded = await service.RecordSimulationRunAsync(Guid.NewGuid(), "Sensitive trace", run, "private response");

        var redacted = await service.GetAsync(recorded.Summary.Id, false);
        var revealed = await service.GetAsync(recorded.Summary.Id, true);

        Assert.All(redacted.Artifacts.Where(item => item.IsSensitive), item => Assert.True(item.IsRedacted));
        Assert.Contains(revealed.Artifacts, item => item.Kind == "Response" && item.Content == "private response" && !item.IsRedacted);
    }

    [Fact]
    public async Task Recording_same_simulation_run_is_idempotent()
    {
        var repository = new InMemoryTraceRepository();
        var service = new TraceStudioService(repository, new TestSimulationStore([]));
        var run = CreateRun();

        var first = await service.RecordSimulationRunAsync(Guid.NewGuid(), "Idempotent trace", run);
        var second = await service.RecordSimulationRunAsync(Guid.NewGuid(), "Idempotent trace", run);

        Assert.Equal(first.Summary.Id, second.Summary.Id);
        Assert.Single(await repository.ListAsync());
    }

    private static SimulationRun CreateRun()
    {
        var now = DateTimeOffset.UtcNow;
        return new SimulationRun(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            "Completed",
            SimulationMode.Normal,
            new SimulationWorkflowSnapshot(Guid.NewGuid(), Guid.NewGuid(), "Claims", "1.0", "Published", [], []),
            "Prompt version: Claims v1.0\nRender governed response.",
            new SimulationKnowledgePackage(Guid.NewGuid(), "Knowledge", "Keyword", .96, 120,
                [new SimulationCitation("Policy", "Claims", "Governed citation")]),
            new SimulationExecutionPlan(Guid.NewGuid(), "Deterministic", "Primary", true, false, 3, 1, 120, 80, .001m, "ZAR", 140, 1, 0),
            new SimulationExecutionMetrics(120, 80, 200, .001m, "ZAR", 150, 120),
            new SimulationEvaluation(.96, .94, 1, "Passed"),
            [
                new(Guid.NewGuid(), "Conversation accepted", "Conversation", "Completed", "Accepted", now, 1),
                new(Guid.NewGuid(), "Knowledge retrieved", "Knowledge", "Completed", "Citation retrieved", now.AddMilliseconds(1), 5),
                new(Guid.NewGuid(), "Prompt rendered", "Prompt", "Completed", "Prompt rendered", now.AddMilliseconds(6), 2),
                new(Guid.NewGuid(), "Model execution", "Intelligence", "Completed", "Response normalized", now.AddMilliseconds(8), 120),
                new(Guid.NewGuid(), "Evaluation completed", "Evaluation", "Completed", "Passed", now.AddMilliseconds(128), 3)
            ],
            null,
            now);
    }

    private sealed class TestSimulationStore(IReadOnlyList<SimulationState> simulations) : IConversationSimulationStore
    {
        public Task<IReadOnlyList<SimulationState>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult(simulations);
        public Task<SimulationState?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(simulations.FirstOrDefault(item => item.Id == id));
        public Task<SimulationState> AddAsync(CreateSimulationCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveAsync(SimulationState state, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class InMemoryTraceRepository : ITraceStudioRepository
    {
        private readonly List<TraceState> _traces = [];

        public Task<IReadOnlyList<TraceState>> ListAsync(int limit = 500, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TraceState>>(_traces.OrderByDescending(item => item.StartedAt).Take(limit).ToList());

        public Task<TraceState?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_traces.SingleOrDefault(item => item.Id == id));

        public Task<TraceState?> GetBySourceRunAsync(Guid sourceRunId, CancellationToken cancellationToken = default)
            => Task.FromResult(_traces.SingleOrDefault(item => item.SourceRunId == sourceRunId));

        public Task AddAsync(TraceState trace, CancellationToken cancellationToken = default)
        {
            if (_traces.All(item => item.Id != trace.Id && item.SourceRunId != trace.SourceRunId)) _traces.Add(trace);
            return Task.CompletedTask;
        }

        public Task AddSpanAsync(TraceSpanState span, TraceEventState traceEvent, CancellationToken cancellationToken = default)
        {
            var index = _traces.FindIndex(item => item.Id == span.TraceId);
            var current = _traces[index];
            _traces[index] = current with { Spans = current.Spans.Append(span).ToList(), Events = current.Events.Append(traceEvent).ToList() };
            return Task.CompletedTask;
        }

        public Task CompleteAsync(Guid id, string status, DateTimeOffset completedAt, double durationMs, string? failureReason, CancellationToken cancellationToken = default)
        {
            var index = _traces.FindIndex(item => item.Id == id);
            _traces[index] = _traces[index] with { Status = status, CompletedAt = completedAt, DurationMs = durationMs, FailureReason = failureReason };
            return Task.CompletedTask;
        }
    }
}
