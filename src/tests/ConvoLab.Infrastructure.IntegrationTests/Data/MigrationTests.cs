using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.EvaluationStudio;
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
        "EvaluationScorecards",
        "EvaluationMetricDefinitions",
        "EvaluationRuns",
        "EvaluationMetricResults",
        "EvaluationTestCases",
        "EvaluationBatches",
        "EvaluationBatchItems",
        "Traces",
        "TraceSpans",
        "TraceEvents",
        "TraceArtifacts",
        "ReplayExperiments",
        "ReplayCandidates",
        "PolicyDefinitions",
        "PolicyRules",
        "PolicyDecisions",
        "Plugins",
        "PluginHealthChecks"
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
            "202607190001_EvaluationScorecardsV1",
            "202607220001_EvaluationStudioExpansionV1",
            "202607220002_TraceStudioV1",
            "202607220003_ReplayStudioV1",
            "202607220004_PolicyStudioV1",
            "202607220005_PluginStudioV1"
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

    [Fact]
    public async Task Existing_scorecards_are_preserved_and_backfilled_during_expansion()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """);
        foreach (var migrationId in new[]
        {
            "202607170001_KnowledgeStudioV1",
            "202607170002_PromptStudioV1",
            "202607180001_PlatformHardeningSprint1",
            "202607180002_WorkflowStudioV1",
            "202607190001_EvaluationScorecardsV1"
        })
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({migrationId}, {"8.0.13"})");
        }
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE EvaluationScorecards (
                Id TEXT NOT NULL CONSTRAINT PK_EvaluationScorecards PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT NOT NULL,
                MinimumGroundedness REAL NOT NULL,
                MinimumRelevance REAL NOT NULL,
                MinimumSafety REAL NOT NULL,
                MinimumOverallScore REAL NOT NULL,
                FailureAction TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IX_EvaluationScorecards_Name ON EvaluationScorecards (Name);
            CREATE INDEX IX_EvaluationScorecards_UpdatedAt ON EvaluationScorecards (UpdatedAt);
            """);

        var scorecardId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO EvaluationScorecards
                (Id, Name, Description, MinimumGroundedness, MinimumRelevance, MinimumSafety,
                 MinimumOverallScore, FailureAction, CreatedAt, UpdatedAt)
            VALUES
                ({scorecardId}, {"Existing claims scorecard"}, {"Must survive expansion"},
                 {0.81}, {0.82}, {0.99}, {0.86}, {"Review"}, {now}, {now})
            """);

        await db.Database.MigrateAsync();
        var repository = new EfEvaluationStudioRepository(db);
        await repository.BackfillLegacyScorecardsAsync();
        await repository.BackfillLegacyScorecardsAsync();

        var scorecard = await repository.GetScorecardAsync(scorecardId);
        Assert.NotNull(scorecard);
        Assert.Equal("Existing claims scorecard", scorecard.Name);
        Assert.Equal("Published", scorecard.Status);
        Assert.Equal("1.0", scorecard.Version);
        Assert.Equal(0.86, scorecard.QualityGateThreshold);
        Assert.Equal(3, scorecard.Metrics.Count);
        Assert.Equal(3, await db.EvaluationMetricDefinitions.CountAsync(item => item.ScorecardId == scorecardId));
    }
}
