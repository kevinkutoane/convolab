using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Common.Persistence;
using ConvoLab.Application.EvaluationStudio;
using ConvoLab.Application.Simulation;

namespace ConvoLab.Application.Tests.EvaluationStudio;

public sealed class EvaluationStudioServiceTests
{
    [Fact]
    public async Task Empty_platform_returns_zeroed_quality_overview()
    {
        var service = CreateService();

        var overview = await service.GetOverviewAsync();

        Assert.Equal(0, overview.TotalRuns);
        Assert.Equal(0d, overview.PassRate);
        Assert.Equal(.82, overview.Policy.MinimumOverallScore);
        Assert.Equal(4, overview.Metrics.Count);
    }

    [Fact]
    public async Task Preview_identifies_failed_quality_gates()
    {
        var service = CreateService();

        var preview = await service.PreviewAsync(new EvaluationPreviewCommand(.7, .9, .99));

        Assert.False(preview.Passed);
        Assert.Contains("Groundedness", preview.FailedGates);
        Assert.Equal("Review", preview.Verdict);
        Assert.Equal(.8425, preview.OverallScore);
    }

    [Fact]
    public async Task Preview_rejects_scores_outside_the_unit_interval()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.PreviewAsync(new EvaluationPreviewCommand(1.1, .9, -.1)));

        Assert.Equal("evaluation.preview.invalid", exception.Code);
        Assert.Contains("groundedness", exception.ValidationErrors.Keys);
        Assert.Contains("safety", exception.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Created_scorecard_is_persisted_and_drives_preview_gates()
    {
        var repository = new InMemoryScorecardRepository();
        var service = CreateService(repository);
        var scorecard = await service.CreateScorecardAsync(new CreateEvaluationScorecardCommand(
            "Strict release gate",
            "Used before production release.",
            .95,
            .9,
            .99,
            .94,
            "Block"));

        var listed = await service.ListScorecardsAsync();
        var preview = await service.PreviewAsync(new EvaluationPreviewCommand(.9, .95, .995, scorecard.Id));

        Assert.Single(listed);
        Assert.Equal(scorecard.Id, listed[0].Id);
        Assert.False(preview.Passed);
        Assert.Equal("Block", preview.Verdict);
        Assert.Contains("Groundedness", preview.FailedGates);
    }

    private static EvaluationStudioService CreateService(InMemoryScorecardRepository? repository = null)
        => new(
            new EmptySimulationStore(),
            new TestConfiguration(),
            repository ?? new InMemoryScorecardRepository(),
            new TestUnitOfWork());

    private sealed class TestConfiguration : IEvaluationStudioConfiguration
    {
        public EvaluationPolicyDto GetPolicy() => new(.8, .8, .95, .82, "Review");
    }

    private sealed class EmptySimulationStore : IConversationSimulationStore
    {
        public Task<IReadOnlyList<SimulationState>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SimulationState>>([]);

        public Task<SimulationState?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<SimulationState?>(null);

        public Task<SimulationState> AddAsync(
            CreateSimulationCommand command,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SaveAsync(SimulationState state, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class InMemoryScorecardRepository : IEvaluationScorecardRepository
    {
        private readonly List<EvaluationScorecardState> _items = [];

        public Task<IReadOnlyList<EvaluationScorecardState>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EvaluationScorecardState>>(_items);

        public Task<EvaluationScorecardState?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(item => item.Id == id));

        public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(EvaluationScorecardState scorecard, CancellationToken cancellationToken = default)
        {
            _items.Add(scorecard);
            return Task.CompletedTask;
        }
    }

    private sealed class TestUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(1);
    }
}
