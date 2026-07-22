using ConvoLab.Application.EvaluationStudio;
using ConvoLab.Application.Simulation;

namespace ConvoLab.Application.Tests.EvaluationStudio;

public sealed class ExpandedEvaluationStudioServiceTests
{
    [Fact]
    public async Task Empty_platform_seeds_default_published_scorecard()
    {
        var service = new EvaluationStudioService(new InMemoryEvaluationRepository(), new TestSimulationStore([]));

        var overview = await service.GetOverviewAsync();

        var scorecard = Assert.Single(overview.Scorecards);
        Assert.Equal("Published", scorecard.Status);
        Assert.True(scorecard.IsDefault);
        Assert.Equal(4, scorecard.Metrics.Count);
        Assert.Equal(0, overview.Metrics.TotalRuns);
    }

    [Fact]
    public async Task Simulator_run_is_persisted_and_scored_against_quality_gate()
    {
        var run = CreateRun(groundedness: .96, relevance: .92, safety: 1, status: "Completed");
        var simulation = new SimulationState(Guid.NewGuid(), "Claims regression", "Workflow", "Prompt", "Knowledge", DateTimeOffset.UtcNow);
        simulation.AddRun(run);
        var repository = new InMemoryEvaluationRepository();
        var service = new EvaluationStudioService(repository, new TestSimulationStore([simulation]));

        var overview = await service.GetOverviewAsync();

        var evaluation = Assert.Single(overview.RecentRuns);
        Assert.Equal(run.Id, evaluation.SourceRunId);
        Assert.Equal("Passed", evaluation.Verdict);
        Assert.True(evaluation.OverallScore >= .85);
        Assert.All(evaluation.Metrics.Where(item => item.Key != "completeness"), item => Assert.True(item.Passed));
    }

    [Fact]
    public async Task Comparison_marks_lower_candidate_as_regression()
    {
        var repository = new InMemoryEvaluationRepository();
        var service = new EvaluationStudioService(repository, new TestSimulationStore([]));
        var scorecard = Assert.Single(await service.ListScorecardsAsync());
        var baseline = await service.RecordSimulationRunAsync(Guid.NewGuid(), "Baseline", CreateRun(.98, .96, 1, "Completed"), scorecard.Id);
        var candidate = await service.RecordSimulationRunAsync(Guid.NewGuid(), "Candidate", CreateRun(.70, .72, 1, "Completed"), scorecard.Id);

        var comparison = await service.CompareRunsAsync(baseline.Id, candidate.Id);

        Assert.Equal("Regression", comparison.Outcome);
        Assert.True(comparison.OverallDelta < 0);
        Assert.Contains(comparison.Metrics, item => item.Direction == "Regressed");
    }

    private static SimulationRun CreateRun(double groundedness, double relevance, double safety, string status)
    {
        var now = DateTimeOffset.UtcNow;
        return new SimulationRun(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            status,
            SimulationMode.Normal,
            null,
            "Rendered prompt",
            new SimulationKnowledgePackage(Guid.NewGuid(), "Knowledge", "Keyword", .95, 120,
                [new SimulationCitation("Policy", "Claims", "Governed citation")]),
            null,
            new SimulationExecutionMetrics(100, 80, 180, .001m, "ZAR", 120, 90),
            new SimulationEvaluation(groundedness, relevance, safety, "Passed"),
            [
                new(Guid.NewGuid(), "Conversation accepted", "Conversation", "Completed", "ok", now, 1),
                new(Guid.NewGuid(), "Workflow path resolved", "Workflow", "Completed", "ok", now, 1),
                new(Guid.NewGuid(), "Knowledge retrieved", "Knowledge", "Completed", "ok", now, 1),
                new(Guid.NewGuid(), "Prompt rendered", "Prompt", "Completed", "ok", now, 1),
                new(Guid.NewGuid(), "Model execution", "Intelligence", "Completed", "ok", now, 1),
                new(Guid.NewGuid(), "Evaluation completed", "Evaluation", "Completed", "ok", now, 1)
            ],
            status == "Completed" ? null : "failed",
            now);
    }

    private sealed class TestSimulationStore(IReadOnlyList<SimulationState> simulations) : IConversationSimulationStore
    {
        public Task<IReadOnlyList<SimulationState>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(simulations);

        public Task<SimulationState?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(simulations.FirstOrDefault(item => item.Id == id));

        public Task<SimulationState> AddAsync(CreateSimulationCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SaveAsync(SimulationState state, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class InMemoryEvaluationRepository : IEvaluationStudioRepository
    {
        private readonly List<EvaluationScorecardState> _scorecards = [];
        private readonly List<EvaluationRunState> _runs = [];
        private readonly List<EvaluationTestCaseState> _testCases = [];
        private readonly List<EvaluationBatchState> _batches = [];

        public Task BackfillLegacyScorecardsAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<EvaluationScorecardState>> ListScorecardsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EvaluationScorecardState>>(_scorecards.ToList());

        public Task<EvaluationScorecardState?> GetScorecardAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_scorecards.SingleOrDefault(item => item.Id == id));

        public Task AddScorecardAsync(EvaluationScorecardState scorecard, CancellationToken cancellationToken = default)
        {
            _scorecards.Add(scorecard);
            return Task.CompletedTask;
        }

        public Task UpdateScorecardAsync(EvaluationScorecardState scorecard, long expectedRevision, CancellationToken cancellationToken = default)
        {
            var index = _scorecards.FindIndex(item => item.Id == scorecard.Id);
            _scorecards[index] = scorecard;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<EvaluationRunState>> ListRunsAsync(int limit = 250, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EvaluationRunState>>(_runs.OrderByDescending(item => item.CreatedAt).Take(limit).ToList());

        public Task<EvaluationRunState?> GetRunAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_runs.SingleOrDefault(item => item.Id == id));

        public Task<EvaluationRunState?> GetRunBySourceAsync(Guid sourceRunId, Guid scorecardId, CancellationToken cancellationToken = default)
            => Task.FromResult(_runs.SingleOrDefault(item => item.SourceRunId == sourceRunId && item.ScorecardId == scorecardId));

        public Task AddRunAsync(EvaluationRunState run, CancellationToken cancellationToken = default)
        {
            if (_runs.All(item => item.SourceRunId != run.SourceRunId || item.ScorecardId != run.ScorecardId)) _runs.Add(run);
            return Task.CompletedTask;
        }

        public Task UpdateRunReviewAsync(Guid id, string status, string reviewer, string? notes, DateTimeOffset reviewedAt, CancellationToken cancellationToken = default)
        {
            var index = _runs.FindIndex(item => item.Id == id);
            _runs[index] = _runs[index] with { ReviewStatus = status, Reviewer = reviewer, ReviewNotes = notes, ReviewedAt = reviewedAt };
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<EvaluationTestCaseState>> ListTestCasesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EvaluationTestCaseState>>(_testCases.ToList());

        public Task<EvaluationTestCaseState?> GetTestCaseAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_testCases.SingleOrDefault(item => item.Id == id));

        public Task AddTestCaseAsync(EvaluationTestCaseState testCase, CancellationToken cancellationToken = default)
        {
            _testCases.Add(testCase);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<EvaluationBatchState>> ListBatchesAsync(int limit = 25, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EvaluationBatchState>>(_batches.Take(limit).ToList());

        public Task AddBatchAsync(EvaluationBatchState batch, CancellationToken cancellationToken = default)
        {
            _batches.Add(batch);
            return Task.CompletedTask;
        }
    }
}
