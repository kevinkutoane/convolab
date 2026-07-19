using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.IntegrationTests.Data;

public sealed class MigrationTests
{
    private static readonly string[] RequiredTables =
    [
        "ConversationSimulations",
        "KnowledgeCollections",
        "KnowledgeDocuments",
        "KnowledgeChunks",
        "KnowledgeLifecycle",
        "Prompts",
        "PromptVersions",
        "PromptLifecycle",
        "Workflows",
        "WorkflowVersions",
        "WorkflowNodes",
        "WorkflowTransitions",
        "WorkflowAudit",
        "EvaluationScorecards"
    ];

    [Fact]
    public async Task Migrations_Are_Discoverable_And_Create_The_Complete_Schema()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.OpenConnectionAsync();

        var migrations = db.Database.GetMigrations().ToArray();
        Assert.Equal(
        [
            "202607170001_KnowledgeStudioV1",
            "202607170002_PromptStudioV1",
            "202607180001_PlatformHardeningSprint1",
            "202607180002_WorkflowStudioV1",
            "202607190001_EvaluationScorecardsV1"
        ], migrations);

        await db.Database.MigrateAsync();

        foreach (var table in RequiredTables)
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$name";
            parameter.Value = table;
            command.Parameters.Add(parameter);

            Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
        }
    }
}
