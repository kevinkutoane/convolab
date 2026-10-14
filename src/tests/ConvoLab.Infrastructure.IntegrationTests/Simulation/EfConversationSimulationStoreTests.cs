using ConvoLab.Application.Simulation;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.Simulation;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.IntegrationTests.Simulation;

public sealed class EfConversationSimulationStoreTests
{
    [Fact]
    public async Task Simulation_reads_are_isolated_by_workspace_context()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using (var schema = new ApplicationDbContext(options))
            await schema.Database.MigrateAsync();

        var workspaceA = Guid.NewGuid();
        var workspaceB = Guid.NewGuid();
        var organisationA = Guid.NewGuid();
        var organisationB = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.Organisations.AddRange(
                new OrganisationRecord { Id = organisationA, Name = "Organisation A", Slug = $"org-a-{workspaceA:N}", CreatedAt = now, UpdatedAt = now },
                new OrganisationRecord { Id = organisationB, Name = "Organisation B", Slug = $"org-b-{workspaceB:N}", CreatedAt = now, UpdatedAt = now });
            seed.Workspaces.AddRange(
                new WorkspaceRecord { Id = workspaceA, OrganisationId = organisationA, Name = "Workspace A", Slug = $"workspace-a-{workspaceA:N}", CreatedAt = now, UpdatedAt = now },
                new WorkspaceRecord { Id = workspaceB, OrganisationId = organisationB, Name = "Workspace B", Slug = $"workspace-b-{workspaceB:N}", CreatedAt = now, UpdatedAt = now });
            await seed.SaveChangesAsync();
        }

        var runtimeA = new WorkspaceRequestContext { WorkspaceId = workspaceA };
        await using (var dbA = new ApplicationDbContext(options, runtimeA))
        {
            var storeA = new EfConversationSimulationStore(dbA, runtimeA);
            await storeA.AddAsync(new CreateSimulationCommand("Workspace A simulation", "Workflow", "Prompt", "Knowledge"));
            Assert.Single(await storeA.ListAsync());
        }

        var runtimeB = new WorkspaceRequestContext { WorkspaceId = workspaceB };
        await using (var dbB = new ApplicationDbContext(options, runtimeB))
        {
            var storeB = new EfConversationSimulationStore(dbB, runtimeB);
            Assert.Empty(await storeB.ListAsync());
        }
    }

    [Fact]
    public async Task Simulation_store_fails_closed_without_workspace_context()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();

        var store = new EfConversationSimulationStore(db, new WorkspaceRequestContext());

        Assert.Empty(await store.ListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.AddAsync(
            new CreateSimulationCommand("Unscoped", "Workflow", "Prompt", "Knowledge")));
    }
}
