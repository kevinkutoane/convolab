using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.KnowledgeStudio;
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
}
