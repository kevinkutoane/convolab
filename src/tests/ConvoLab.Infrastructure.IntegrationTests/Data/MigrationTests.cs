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
        "PluginHealthChecks",
        "Organisations",
        "Workspaces",
        "IdentityUsers",
        "WorkspaceMemberships",
        "LocalCredentials",
        "AuthenticationSessions",
        "ServiceAccounts",
        "WorkspaceAuditEvents"
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
            "202607220005_PluginStudioV1",
            "202607220006_WorkspaceIdentityAccessV1"
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
        await db.Database.MigrateAsync();

        var scorecardId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.EvaluationScorecards.Add(new EvaluationScorecardRecord
        {
            Id = scorecardId, Name = "Existing claims scorecard", Description = "Must survive expansion",
            Status = "Published", Version = "1.0", QualityGateThreshold = 0.86, Revision = 1,
            MinimumGroundedness = 0.81, MinimumRelevance = 0.82, MinimumSafety = 0.99,
            MinimumOverallScore = 0.86, FailureAction = "Review", CreatedAt = now, UpdatedAt = now
        });
        await db.SaveChangesAsync();
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
