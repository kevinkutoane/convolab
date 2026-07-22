using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.PolicyStudio;
using ConvoLab.Domain.Policy.Enums;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.KnowledgeStudio;
using ConvoLab.Infrastructure.PolicyStudio;
using ConvoLab.Application.PluginStudio;
using ConvoLab.Domain.Plugins.Enums;
using ConvoLab.Infrastructure.PluginStudio;
using ConvoLab.Infrastructure.PromptStudio;
using ConvoLab.Infrastructure.WorkflowStudio;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.IntegrationTests.Data;

public sealed class SqliteRepositoryQueryTests
{
    [Fact]
    public async Task Empty_Studio_Lists_Are_Queryable_With_Sqlite()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.MigrateAsync();

        var knowledge = new EfKnowledgeStudioRepository(db);
        var prompts = new EfPromptStudioRepository(db);
        var workflows = new EfWorkflowStudioRepository(db);

        Assert.Empty(await knowledge.ListCollectionsAsync());
        Assert.Empty(await knowledge.ListDocumentsAsync(Guid.NewGuid()));
        Assert.Empty(await prompts.ListPromptsAsync());
        Assert.Empty(await prompts.ListVersionsAsync(Guid.NewGuid()));
        Assert.Empty(await workflows.ListAsync());
        Assert.Empty(await workflows.ListAuditAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Failed_policy_activation_rolls_back_retirement_of_previous_active_version()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.MigrateAsync();
        var repository = new EfPolicyStudioRepository(db);
        var policyKey = Guid.NewGuid();
        var active = PolicyState(policyKey, 1, PolicyStatus.Active);
        var draft = PolicyState(policyKey, 2, PolicyStatus.Draft);
        await repository.AddPolicyAsync(active);
        await repository.AddPolicyAsync(draft);

        var retired = active with
        {
            Status = PolicyStatus.Retired,
            Revision = active.Revision + 1,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var successor = draft with
        {
            Status = PolicyStatus.Active,
            Revision = draft.Revision + 1,
            UpdatedAt = DateTimeOffset.UtcNow,
            ActivatedAt = DateTimeOffset.UtcNow
        };

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repository.ActivateVersionAsync(
            successor,
            expectedRevision: 999,
            [new PolicyVersionUpdate(retired, active.Revision)]));

        db.ChangeTracker.Clear();
        var persisted = await repository.GetVersionHistoryAsync(policyKey);
        Assert.Equal(PolicyStatus.Active, persisted.Single(item => item.Id == active.Id).Status);
        Assert.Equal(PolicyStatus.Draft, persisted.Single(item => item.Id == draft.Id).Status);
    }

    [Fact]
    public async Task Failed_plugin_successor_activation_rolls_back_previous_version_deactivation()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.MigrateAsync();
        var repository = new EfPluginStudioRepository(db);
        var logicalKey = Guid.NewGuid();
        var active = PluginState(logicalKey, "1.0.0", PluginStatus.Active);
        var successor = PluginState(logicalKey, "1.1.0", PluginStatus.Installed);
        await repository.AddPluginAsync(active);
        await repository.AddPluginAsync(successor);

        var deactivated = active with
        {
            Status = PluginStatus.Inactive,
            Revision = active.Revision + 1,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var invalidSuccessor = successor with
        {
            Version = active.Version,
            Status = PluginStatus.Active,
            Revision = successor.Revision + 1,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await Assert.ThrowsAsync<ResourceConflictException>(() => repository.UpdatePluginsAsync(
        [
            new PluginUpdateState(invalidSuccessor, successor.Revision),
            new PluginUpdateState(deactivated, active.Revision)
        ]));

        db.ChangeTracker.Clear();
        var persisted = await repository.GetVersionHistoryAsync(logicalKey);
        Assert.Equal(PluginStatus.Active, persisted.Single(item => item.Id == active.Id).Status);
        var persistedSuccessor = persisted.Single(item => item.Id == successor.Id);
        Assert.Equal(PluginStatus.Installed, persistedSuccessor.Status);
        Assert.Equal("1.1.0", persistedSuccessor.Version);
    }

    private static PolicyDefinitionState PolicyState(Guid policyKey, int version, PolicyStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        return new PolicyDefinitionState(
            Guid.NewGuid(),
            policyKey,
            version,
            "Atomic activation policy",
            "Infrastructure transaction test",
            "Test suite",
            PolicyDomain.ProviderAccess,
            status,
            PolicyScope.Global,
            "All",
            null,
            PolicyEffect.Allow,
            1,
            now,
            now,
            status == PolicyStatus.Active ? now : null,
            []);
    }

    private static PluginState PluginState(Guid pluginKey, string version, PluginStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        return new PluginState(
            Guid.NewGuid(), pluginKey, "atomic-plugin", "Atomic plugin", "Transaction test", "ConvoLab",
            version, PluginCategory.Tool, status, PluginHealthStatus.Healthy, "Ready",
            "builtin://test/atomic", "AtomicPlugin", "1.0", ["test"], [], "{}",
            new Dictionary<string, string>(), now, 1, now, now);
    }
}
