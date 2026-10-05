using ConvoLab.Application.Analytics;
using ConvoLab.Application.Settings;
using ConvoLab.Domain.Analytics;
using ConvoLab.Domain.Settings;
using ConvoLab.Domain.WorkspaceIdentity;
using ConvoLab.Infrastructure.Analytics;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.Settings;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ConvoLab.Infrastructure.IntegrationTests.Analytics;

public sealed class ExecutiveFinOpsTests
{
    [Fact]
    public async Task Executive_finops_summary_calculates_roi_and_cost_attribution()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.MigrateAsync();

        var organisationId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var environmentId = Guid.NewGuid();

        db.Organisations.Add(new OrganisationRecord
        {
            Id = organisationId,
            Name = "FinOps Org",
            Slug = "finops-org",
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Workspaces.Add(new WorkspaceRecord
        {
            Id = workspaceId,
            OrganisationId = organisationId,
            Name = "FinOps Workspace",
            Slug = "finops-workspace",
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.RuntimeEnvironments.Add(new RuntimeEnvironmentRecord
        {
            Id = environmentId,
            OrganisationId = organisationId,
            WorkspaceId = workspaceId,
            Name = "Production",
            Slug = "production",
            EnvironmentType = "Production",
            Status = "Active",
            CreatedBy = Guid.Empty,
            IsDefault = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var now = DateTimeOffset.UtcNow;
        var exec1 = Guid.NewGuid();
        var exec2 = Guid.NewGuid();
        var exec3 = Guid.NewGuid();

        // Execution 1: Succeeded, 500 input + 200 output tokens, R0.08 actual cost
        db.AnalyticsEvents.Add(CreateEvent(organisationId, workspaceId, environmentId, "SimulationCompleted", "Succeeded", exec1, now.AddMinutes(-30)));
        db.AnalyticsEvents.Add(CreateProviderEvent(organisationId, workspaceId, environmentId, exec1, "Gemini", "gemini-2.5-flash", "Chat", 500, 200, 0.08m, "Actual", now.AddMinutes(-30)));

        // Execution 2: Succeeded, 1000 input + 500 output tokens, R0.25 estimated cost
        db.AnalyticsEvents.Add(CreateEvent(organisationId, workspaceId, environmentId, "SimulationCompleted", "Succeeded", exec2, now.AddMinutes(-20)));
        db.AnalyticsEvents.Add(CreateProviderEvent(organisationId, workspaceId, environmentId, exec2, "AzureOpenAI", "gpt-4o-mini", "Support", 1000, 500, 0.25m, "Estimated", now.AddMinutes(-20)));

        // Execution 3: Failed, 200 input + 50 output tokens, R0.02 actual cost
        db.AnalyticsEvents.Add(CreateEvent(organisationId, workspaceId, environmentId, "SimulationFailed", "Failed", exec3, now.AddMinutes(-10)));
        db.AnalyticsEvents.Add(CreateProviderEvent(organisationId, workspaceId, environmentId, exec3, "Gemini", "gemini-2.5-flash", "Chat", 200, 50, 0.02m, "Actual", now.AddMinutes(-10)));

        await db.SaveChangesAsync();

        var resolver = new FakeConfigurationResolver();
        var service = new AnalyticsService(db, resolver);

        var query = new AnalyticsQuery(
            workspaceId,
            environmentId,
            now.AddHours(-2),
            now,
            "hour");

        var visibility = new AnalyticsFieldVisibility(true, true, true, true, true);
        var summary = await service.ExecutiveFinOpsAsync(query, visibility, humanBenchmarkCostPerResolution: 45.0m);

        Assert.Equal(0.35m, summary.TotalAiCostZar);
        Assert.Equal(0.10m, summary.ActualSpendZar);
        Assert.Equal(0.25m, summary.EstimatedSpendZar);
        Assert.Equal(3, summary.TotalExecutions);
        Assert.Equal(2, summary.SucceededExecutions);
        Assert.Equal(1, summary.FailedExecutions);

        // Human benchmark: 2 succeeded * R45 = R90.00
        Assert.Equal(90.00m, summary.EquivalentHumanCostZar);
        // Cost savings: R90.00 - R0.35 = R89.65
        Assert.Equal(89.65m, summary.EstimatedCostSavingsZar);
        Assert.True(summary.RoiPercentage > 20000m); // > 20,000% ROI

        // Cost per resolution: R0.35 / 2 = 0.1750
        Assert.Equal(0.175m, summary.CostPerResolutionZar);

        // Token analytics: 2450 total tokens
        Assert.Equal(2450, summary.TotalTokens);
        Assert.Equal(1700, summary.InputTokens);
        Assert.Equal(750, summary.OutputTokens);

        // Attributions
        Assert.NotEmpty(summary.CostByProvider);
        Assert.Contains(summary.CostByProvider, p => p.Key == "Gemini");
        Assert.Contains(summary.CostByProvider, p => p.Key == "AzureOpenAI");

        Assert.NotEmpty(summary.CostByModel);
        Assert.Contains(summary.CostByModel, m => m.Key == "gemini-2.5-flash");
        Assert.Contains(summary.CostByModel, m => m.Key == "gpt-4o-mini");

        Assert.NotEmpty(summary.CostByCapability);
        Assert.Contains(summary.CostByCapability, c => c.Key == "Chat");
        Assert.Contains(summary.CostByCapability, c => c.Key == "Support");

        Assert.NotEmpty(summary.Recommendations);
    }

    [Fact]
    public async Task Finops_category_dashboard_returns_roi_metrics()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.MigrateAsync();

        var organisationId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var environmentId = Guid.NewGuid();

        db.Organisations.Add(new OrganisationRecord
        {
            Id = organisationId,
            Name = "FinOps Org 2",
            Slug = "finops-org-2",
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Workspaces.Add(new WorkspaceRecord
        {
            Id = workspaceId,
            OrganisationId = organisationId,
            Name = "FinOps Workspace 2",
            Slug = "finops-workspace-2",
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.RuntimeEnvironments.Add(new RuntimeEnvironmentRecord
        {
            Id = environmentId,
            OrganisationId = organisationId,
            WorkspaceId = workspaceId,
            Name = "Production",
            Slug = "production",
            EnvironmentType = "Production",
            Status = "Active",
            CreatedBy = Guid.Empty,
            IsDefault = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var now = DateTimeOffset.UtcNow;
        var execId = Guid.NewGuid();
        db.AnalyticsEvents.Add(CreateEvent(organisationId, workspaceId, environmentId, "SimulationCompleted", "Succeeded", execId, now.AddMinutes(-10)));
        db.AnalyticsEvents.Add(CreateProviderEvent(organisationId, workspaceId, environmentId, execId, "Gemini", "gemini-2.5-flash", "Chat", 200, 100, 0.05m, "Actual", now.AddMinutes(-10)));
        await db.SaveChangesAsync();

        var resolver = new FakeConfigurationResolver();
        var service = new AnalyticsService(db, resolver);

        var query = new AnalyticsQuery(
            workspaceId,
            environmentId,
            now.AddHours(-2),
            now,
            "hour");

        var visibility = new AnalyticsFieldVisibility(true, true, true, true, true);
        var dashboard = await service.DashboardAsync("finops", query, visibility);

        Assert.Equal("finops", dashboard.Category);
        Assert.Contains(dashboard.Metrics, m => m.Key == "totalAiCost");
        Assert.Contains(dashboard.Metrics, m => m.Key == "estimatedCostSavings");
        Assert.Contains(dashboard.Metrics, m => m.Key == "roiPercentage");
        Assert.Contains(dashboard.Metrics, m => m.Key == "costPerResolution");
    }

    private static AnalyticsEventRecord CreateEvent(
        Guid orgId, Guid wsId, Guid envId, string eventType, string outcome, Guid execId, DateTimeOffset occurredAt) => new()
    {
        Id = Guid.NewGuid(),
        EventKey = Guid.NewGuid().ToString("N"),
        OrganisationId = orgId,
        WorkspaceId = wsId,
        EnvironmentId = envId,
        ActorType = "System",
        Capability = "Execution",
        EventType = eventType,
        Outcome = outcome,
        SourceExecutionId = execId,
        SourceType = "Execution",
        SourceId = execId,
        ConfigurationRevision = "rev-1",
        CorrelationId = Guid.NewGuid().ToString("N"),
        OccurredAt = occurredAt
    };

    private static AnalyticsEventRecord CreateProviderEvent(
        Guid orgId, Guid wsId, Guid envId, Guid execId, string provider, string model, string capability,
        int inputTokens, int outputTokens, decimal costZar, string costType, DateTimeOffset occurredAt) => new()
    {
        Id = Guid.NewGuid(),
        EventKey = Guid.NewGuid().ToString("N"),
        OrganisationId = orgId,
        WorkspaceId = wsId,
        EnvironmentId = envId,
        ActorType = "System",
        Capability = capability,
        EventType = "ProviderInvocationCompleted",
        Outcome = "Succeeded",
        Provider = provider,
        Model = model,
        InputTokens = inputTokens,
        OutputTokens = outputTokens,
        CostZar = costZar,
        CostType = costType,
        SourceExecutionId = execId,
        SourceType = "Execution",
        SourceId = execId,
        ConfigurationRevision = "rev-1",
        CorrelationId = Guid.NewGuid().ToString("N"),
        OccurredAt = occurredAt
    };

    private sealed class FakeConfigurationResolver : IEffectiveConfigurationResolver
    {
        public Task<IReadOnlyList<EffectiveSettingResult>> ResolveAsync(Guid organisationId, Guid workspaceId, Guid? environmentId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<EffectiveSettingResult>>([]);

        public Task<EffectiveSettingResult?> ResolveOneAsync(Guid organisationId, Guid workspaceId, Guid? environmentId, string key, CancellationToken ct = default)
            => Task.FromResult<EffectiveSettingResult?>(null);

        public Task<ConfigurationSnapshot> CreateSnapshotAsync(Guid organisationId, Guid workspaceId, Guid environmentId, CancellationToken ct = default, IReadOnlyDictionary<string, string?>? executionOverrides = null)
            => throw new NotImplementedException();
    }
}
