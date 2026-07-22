using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.IntelligenceStudio;
using ConvoLab.Application.Simulation;

namespace ConvoLab.Application.Tests.IntelligenceStudio;

public sealed class IntelligenceStudioServiceTests
{
    [Fact]
    public async Task Empty_platform_returns_healthy_zeroed_overview()
    {
        var service = new IntelligenceStudioService(new EmptySimulationStore(), new TestConfiguration());

        var overview = await service.GetOverviewAsync();

        Assert.Equal(0, overview.Metrics.TotalExecutions);
        Assert.Equal(0d, overview.Metrics.SuccessRate);
        Assert.Equal(500m, overview.Budget.Limit);
        Assert.Equal("ZAR", overview.Budget.Currency);
        Assert.Equal(2, overview.Providers.Count);
    }

    [Fact]
    public async Task Plan_preview_calculates_deterministic_cost_and_admission_decisions()
    {
        var service = new IntelligenceStudioService(new EmptySimulationStore(), new TestConfiguration());

        var preview = await service.PreviewPlanAsync(new ExecutionPlanPreviewCommand(
            "Deterministic",
            "convolab-deterministic-primary",
            1_000,
            500,
            true,
            true,
            3,
            ["Chat", "TextGeneration"]));

        Assert.True(preview.IsConfigured);
        Assert.True(preview.CapabilityMatch);
        Assert.True(preview.WithinBudget);
        Assert.Equal(0.04m, preview.EstimatedCost);
        Assert.Equal("ZAR", preview.Currency);
        Assert.DoesNotContain(preview.Decisions, item => item.Status == "Blocked");
    }

    [Fact]
    public async Task Plan_preview_rejects_invalid_numeric_inputs()
    {
        var service = new IntelligenceStudioService(new EmptySimulationStore(), new TestConfiguration());

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.PreviewPlanAsync(new ExecutionPlanPreviewCommand(
                "Deterministic",
                "convolab-deterministic-primary",
                0,
                -1,
                false,
                false,
                11)));

        Assert.Equal("intelligence.plan.invalid", exception.Code);
        Assert.Contains("estimatedInputTokens", exception.ValidationErrors.Keys);
        Assert.Contains("maxOutputTokens", exception.ValidationErrors.Keys);
        Assert.Contains("maxAttempts", exception.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Plan_preview_reports_output_limit_without_clamping_the_request()
    {
        var service = new IntelligenceStudioService(new EmptySimulationStore(), new TestConfiguration());

        var preview = await service.PreviewPlanAsync(new ExecutionPlanPreviewCommand(
            "Deterministic",
            "convolab-deterministic-primary",
            1_000,
            5_000,
            false,
            true,
            3));

        Assert.Equal(5_000, preview.EstimatedOutputTokens);
        Assert.Equal(6_000, preview.EstimatedTotalTokens);
        Assert.Contains(preview.Decisions, item => item.Name == "Output limit" && item.Status == "Blocked");
    }

    [Fact]
    public async Task Overview_maps_persisted_simulator_execution_telemetry()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var simulation = new SimulationState(
            Guid.NewGuid(),
            "Claims simulation",
            "Claims workflow",
            "Claims prompt v1",
            "Claims knowledge",
            createdAt);
        simulation.AddRun(new SimulationRun(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "Completed",
            SimulationMode.Fallback,
            null,
            "Rendered prompt",
            new SimulationKnowledgePackage(Guid.NewGuid(), "Claims knowledge", "Keyword", 0.9, 100, []),
            new SimulationExecutionPlan(
                Guid.NewGuid(),
                "Deterministic",
                "convolab-deterministic-fallback",
                true,
                false,
                3,
                1,
                1_000,
                250,
                0.04m,
                "ZAR",
                220,
                2,
                1),
            new SimulationExecutionMetrics(1_000, 250, 1_250, 0.04m, "ZAR", 480, 220),
            new SimulationEvaluation(0.91, 0.87, 1, "Pass"),
            [],
            null,
            createdAt));
        simulation.AddRun(new SimulationRun(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "Completed",
            SimulationMode.Normal,
            null,
            "Legacy rendered prompt",
            new SimulationKnowledgePackage(Guid.NewGuid(), "Legacy knowledge", "Keyword", 0.75, 80, []),
            new SimulationExecutionPlan(
                Guid.NewGuid(),
                "LegacyProvider",
                "legacy-zar-model",
                false,
                false,
                1,
                0,
                1_000,
                250,
                10m,
                "ZAR",
                220,
                1,
                0),
            new SimulationExecutionMetrics(1_000, 250, 1_250, 10m, "ZAR", 320, 180),
            new SimulationEvaluation(0.88, 0.85, 1, "Pass"),
            [],
            null,
            createdAt.AddMinutes(-1)));
        var service = new IntelligenceStudioService(
            new FixedSimulationStore([simulation]),
            new TestConfiguration());

        var overview = await service.GetOverviewAsync();

        Assert.Equal(2, overview.Metrics.TotalExecutions);
        Assert.Equal(2, overview.Metrics.SuccessfulExecutions);
        Assert.Equal(2_500, overview.Metrics.TotalTokens);
        Assert.Equal(10.04m, overview.Metrics.TotalCost);
        Assert.Equal("ZAR", overview.Metrics.Currency);
        Assert.Equal(1, overview.Metrics.RetryExecutions);
        Assert.Equal(1, overview.Metrics.FallbackExecutions);
        Assert.Equal("convolab-deterministic-fallback", overview.RecentExecutions[0].Model);
    }

    private sealed class TestConfiguration : IIntelligenceStudioConfiguration
    {
        public decimal MonthlyBudgetZar => 500m;

        public IReadOnlyList<IntelligenceProviderDefinition> GetProviders() =>
        [
            new(
                "Deterministic",
                "ConvoLab Deterministic",
                true,
                false,
                "Ready",
                null,
                [new(
                    "convolab-deterministic-primary",
                    "Deterministic Primary",
                    ["Chat", "TextGeneration", "Streaming"],
                    32_000,
                    4_000,
                    140,
                    0.02m,
                    0.04m,
                    "ZAR")]),
            new("Gemini", "Google Gemini", false, true, "Not configured", "Set GEMINI_API_KEY", [])
        ];
    }

    private sealed class EmptySimulationStore : IConversationSimulationStore
    {
        public Task<IReadOnlyList<SimulationState>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SimulationState>>([]);

        public Task<SimulationState?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<SimulationState?>(null);

        public Task<SimulationState> AddAsync(CreateSimulationCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SaveAsync(SimulationState state, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class FixedSimulationStore(IReadOnlyList<SimulationState> items) : IConversationSimulationStore
    {
        public Task<IReadOnlyList<SimulationState>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(items);

        public Task<SimulationState?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(items.FirstOrDefault(item => item.Id == id));

        public Task<SimulationState> AddAsync(CreateSimulationCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SaveAsync(SimulationState state, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}
