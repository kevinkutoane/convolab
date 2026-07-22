using ConvoLab.Application.ReplayStudio;
using ConvoLab.Application.Simulation;

namespace ConvoLab.Application.Tests.ReplayStudio;

public sealed class ReplayStudioServiceTests
{
    [Fact]
    public async Task Existing_replay_runs_are_imported_idempotently()
    {
        var simulation = CreateSimulation(out var baseline);
        var replay = CreateRun(simulation, baseline.Id, .98, .96, 1, "candidate response", "Prompt v2", "Knowledge v2");
        simulation.AddRun(replay);
        var store = new TestSimulationStore([simulation]);
        var repository = new InMemoryReplayRepository();
        var service = new ReplayStudioService(repository, store, new TestSimulationService(store));

        var first = await service.GetOverviewAsync();
        var second = await service.GetOverviewAsync();

        Assert.Single(first.RecentExperiments);
        Assert.Equal(1, first.Metrics.TotalCandidates);
        Assert.Single(second.RecentExperiments);
        Assert.Equal(1, second.Metrics.TotalCandidates);
        Assert.Single(await repository.ListCandidatesAsync(first.RecentExperiments[0].Id));
    }

    [Fact]
    public async Task Creating_experiment_executes_override_and_compares_candidate()
    {
        var simulation = CreateSimulation(out var baseline);
        var store = new TestSimulationStore([simulation]);
        var repository = new InMemoryReplayRepository();
        var service = new ReplayStudioService(repository, store, new TestSimulationService(store));

        var result = await service.CreateExperimentAsync(new CreateReplayExperimentCommand(
            "Prompt comparison",
            simulation.Id,
            baseline.Id,
            "Candidate A",
            PromptVersion: "Claims Prompt v2.0",
            KnowledgeCollection: "Claims Knowledge v2",
            Provider: "Deterministic",
            Model: "Primary",
            Temperature: .4,
            MaxOutputTokens: 600));

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(baseline.Id, result.Baseline.RunId);
        Assert.Equal("Claims Prompt v2.0", candidate.Configuration.PromptVersion);
        Assert.Contains("Prompt", candidate.Comparison.ChangedDimensions);
        Assert.Equal("Improved", candidate.Comparison.Outcome);
        Assert.True(candidate.Comparison.QualityDelta > 0);
    }

    [Fact]
    public async Task Completed_experiment_rejects_new_candidates()
    {
        var simulation = CreateSimulation(out var baseline);
        var store = new TestSimulationStore([simulation]);
        var repository = new InMemoryReplayRepository();
        var service = new ReplayStudioService(repository, store, new TestSimulationService(store));
        var created = await service.CreateExperimentAsync(new CreateReplayExperimentCommand(
            "Completion", simulation.Id, baseline.Id, "Candidate A"));

        await service.CompleteAsync(created.Summary.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddCandidateAsync(
            created.Summary.Id,
            new AddReplayCandidateCommand("Candidate B")));
    }

    [Fact]
    public async Task Completed_experiment_can_be_archived_but_active_experiment_cannot()
    {
        var simulation = CreateSimulation(out var baseline);
        var store = new TestSimulationStore([simulation]);
        var repository = new InMemoryReplayRepository();
        var service = new ReplayStudioService(repository, store, new TestSimulationService(store));
        var created = await service.CreateExperimentAsync(new CreateReplayExperimentCommand(
            "Archive lifecycle", simulation.Id, baseline.Id, "Candidate A"));

        await Assert.ThrowsAsync<ConvoLab.Application.Common.Errors.DomainRuleViolationException>(
            () => service.ArchiveAsync(created.Summary.Id));
        await service.CompleteAsync(created.Summary.Id);
        var archived = await service.ArchiveAsync(created.Summary.Id);

        Assert.Equal("Archived", archived.Summary.Status);
    }

    private static SimulationState CreateSimulation(out SimulationRun baseline)
    {
        var simulation = new SimulationState(Guid.NewGuid(), "Claims replay", "Claims Workflow v1.0", "Claims Prompt v1.0", "Claims Knowledge v1", DateTimeOffset.UtcNow);
        var user = simulation.AddMessage("user", "My vehicle was damaged by hail.");
        var assistant = simulation.AddMessage("assistant", "Baseline governed response.");
        baseline = BuildRun(user.Id, assistant.Id, null, .90, .90, 1, "Claims Prompt v1.0", "Claims Knowledge v1", DateTimeOffset.UtcNow);
        simulation.AddRun(baseline);
        return simulation;
    }

    private static SimulationRun CreateRun(SimulationState simulation, Guid sourceRunId, double groundedness, double relevance, double safety, string response, string prompt, string knowledge)
    {
        var source = simulation.FindRun(sourceRunId)!;
        var assistant = simulation.AddMessage("assistant", response, true);
        return BuildRun(source.UserMessageId, assistant.Id, sourceRunId, groundedness, relevance, safety, prompt, knowledge, DateTimeOffset.UtcNow.AddSeconds(1));
    }

    private static SimulationRun BuildRun(Guid userMessageId, Guid assistantMessageId, Guid? replayedFrom, double groundedness, double relevance, double safety, string prompt, string knowledge, DateTimeOffset createdAt)
        => new(
            Guid.NewGuid(),
            userMessageId,
            assistantMessageId,
            replayedFrom,
            "Completed",
            SimulationMode.Normal,
            new SimulationWorkflowSnapshot(Guid.NewGuid(), Guid.NewGuid(), "Claims Workflow", "1.0", "Published", [], []),
            $"[TEMPERATURE:0.20]\n[MAX_OUTPUT_TOKENS:400]\nPROMPT VERSION: {prompt}",
            new SimulationKnowledgePackage(Guid.NewGuid(), knowledge, "Keyword", .95, 100, [new SimulationCitation("Policy", "Claims", "Covered")]),
            new SimulationExecutionPlan(Guid.NewGuid(), "Deterministic", "Primary", true, false, 3, 1, 100, 80, .001m, "ZAR", 100, 1, 0),
            new SimulationExecutionMetrics(100, 80, 180, .001m, "ZAR", 120, 100),
            new SimulationEvaluation(groundedness, relevance, safety, groundedness >= .9 ? "Passed" : "Review"),
            [new SimulationTimelineStep(Guid.NewGuid(), "Model execution", "Intelligence", "Completed", "Done", createdAt, 100)],
            null,
            createdAt,
            new SimulationRunConfiguration("Claims Workflow v1.0", prompt, knowledge, "Deterministic", "Primary", .2, 400, SimulationMode.Normal));

    private sealed class TestSimulationStore(IReadOnlyList<SimulationState> values) : IConversationSimulationStore
    {
        private readonly List<SimulationState> _values = values.ToList();
        public Task<IReadOnlyList<SimulationState>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SimulationState>>(_values);
        public Task<SimulationState?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_values.SingleOrDefault(item => item.Id == id));
        public Task<SimulationState> AddAsync(CreateSimulationCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveAsync(SimulationState state, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class TestSimulationService(TestSimulationStore store) : IConversationSimulationService
    {
        public Task<SimulationOptions> GetOptionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new SimulationOptions(
            ["Claims Workflow v1.0"], ["Claims Prompt v1.0", "Claims Prompt v2.0"], ["Claims Knowledge v1", "Claims Knowledge v2"],
            Enum.GetNames<SimulationMode>(), [new SimulationProviderOption("Deterministic", "Deterministic", "Primary", true, false, "Ready", null)]));
        public Task<IReadOnlyList<SimulationSummary>> ListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SimulationConversation?> GetAsync(Guid simulationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SimulationConversation> CreateAsync(CreateSimulationCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SimulationConversation?> SendMessageAsync(Guid simulationId, SendSimulationMessageCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public async Task<SimulationConversation?> ReplayAsync(Guid simulationId, ReplaySimulationCommand command, CancellationToken cancellationToken = default)
        {
            var simulation = await store.GetAsync(simulationId, cancellationToken);
            if (simulation is null) return null;
            var source = simulation.FindRun(command.RunId)!;
            var assistant = simulation.AddMessage("assistant", "Improved candidate response.", true);
            var run = BuildRun(
                source.UserMessageId,
                assistant.Id,
                source.Id,
                .99,
                .97,
                1,
                command.PromptVersion ?? source.Configuration?.PromptVersion ?? "Prompt",
                command.KnowledgeCollection ?? source.Configuration?.KnowledgeCollection ?? "Knowledge",
                DateTimeOffset.UtcNow.AddSeconds(2)) with
            {
                Configuration = new SimulationRunConfiguration(
                    command.Workflow ?? source.Configuration?.Workflow ?? "Workflow",
                    command.PromptVersion ?? source.Configuration?.PromptVersion ?? "Prompt",
                    command.KnowledgeCollection ?? source.Configuration?.KnowledgeCollection ?? "Knowledge",
                    command.Provider,
                    command.Model ?? "Primary",
                    command.Temperature,
                    command.MaxOutputTokens,
                    command.Mode)
            };
            simulation.AddRun(run);
            return simulation.Snapshot();
        }
    }

    private sealed class InMemoryReplayRepository : IReplayStudioRepository
    {
        private readonly List<ReplayExperimentState> _experiments = [];
        private readonly List<ReplayCandidateState> _candidates = [];

        public Task<IReadOnlyList<ReplayExperimentState>> ListExperimentsAsync(int limit = 250, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReplayExperimentState>>(_experiments.OrderByDescending(item => item.UpdatedAt).Take(limit).ToList());
        public Task<ReplayExperimentState?> GetExperimentAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_experiments.SingleOrDefault(item => item.Id == id));
        public Task<ReplayExperimentState?> GetBySourceRunAsync(Guid simulationId, Guid sourceRunId, CancellationToken cancellationToken = default)
            => Task.FromResult(_experiments.Where(item => item.SimulationId == simulationId && item.SourceRunId == sourceRunId).OrderByDescending(item => item.UpdatedAt).FirstOrDefault());
        public Task AddExperimentAsync(ReplayExperimentState experiment, CancellationToken cancellationToken = default) { _experiments.Add(experiment); return Task.CompletedTask; }
        public Task UpdateExperimentAsync(ReplayExperimentState experiment, CancellationToken cancellationToken = default)
        {
            var index = _experiments.FindIndex(item => item.Id == experiment.Id);
            _experiments[index] = experiment;
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<ReplayCandidateState>> ListCandidatesAsync(Guid experimentId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReplayCandidateState>>(_candidates.Where(item => item.ExperimentId == experimentId).OrderByDescending(item => item.CreatedAt).ToList());
        public Task<ReplayCandidateState?> GetCandidateByRunAsync(Guid runId, CancellationToken cancellationToken = default)
            => Task.FromResult(_candidates.SingleOrDefault(item => item.RunId == runId));
        public Task AddCandidateAsync(ReplayCandidateState candidate, CancellationToken cancellationToken = default)
        {
            if (_candidates.All(item => item.RunId != candidate.RunId)) _candidates.Add(candidate);
            return Task.CompletedTask;
        }
    }
}
