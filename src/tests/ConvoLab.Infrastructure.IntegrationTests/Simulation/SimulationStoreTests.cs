using ConvoLab.Application.Simulation;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.IntegrationTests.Simulation;

public sealed class SimulationStoreTests
{
    [Fact]
    public async Task Sqlite_Store_Can_List_And_Round_Trip_Simulations()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.MigrateAsync();
        var store = new EfConversationSimulationStore(db);

        Assert.Empty(await store.ListAsync());

        var created = await store.AddAsync(new CreateSimulationCommand(
            "SQLite smoke test",
            "Claims intake",
            "1.0.0",
            "Claims knowledge"));
        var listed = await store.ListAsync();
        var loaded = await store.GetAsync(created.Id);

        Assert.Single(listed);
        Assert.Equal(created.Id, listed[0].Id);
        Assert.NotNull(loaded);
        Assert.Equal(created.Id, loaded.Id);
        Assert.Equal(created.Title, loaded.Title);
        Assert.Equal(created.Workflow, loaded.Workflow);
        Assert.Equal(created.PromptVersion, loaded.PromptVersion);
        Assert.Equal(created.KnowledgeCollection, loaded.KnowledgeCollection);
        Assert.Empty(loaded.Snapshot().Messages);
        Assert.Empty(loaded.Snapshot().Runs);
    }
}
