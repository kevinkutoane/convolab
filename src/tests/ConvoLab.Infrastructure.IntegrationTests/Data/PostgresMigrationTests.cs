using ConvoLab.Application.Simulation;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.EvaluationStudio;
using ConvoLab.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ConvoLab.Infrastructure.IntegrationTests.Data;

public sealed class PostgresMigrationTests
{
    [Fact]
    public async Task Fresh_Postgres_database_migrates_and_persists_after_reconnect()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        if (!database.Available) return;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;
        Guid simulationId;
        await using (var first = new ApplicationDbContext(options))
        {
            await first.Database.MigrateAsync();
            Assert.Equal(11, (await first.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(1, await first.Organisations.CountAsync());
            Assert.Equal(1, await first.Workspaces.CountAsync());
            var store = new EfConversationSimulationStore(first);
            simulationId = (await store.AddAsync(new CreateSimulationCommand("PostgreSQL restart evidence", "Workflow", "Prompt", "Knowledge"))).Id;
        }

        await using (var restarted = new ApplicationDbContext(options))
        {
            var loaded = await new EfConversationSimulationStore(restarted).GetAsync(simulationId);
            Assert.NotNull(loaded);
            Assert.Equal("PostgreSQL restart evidence", loaded.Title);
        }
    }

    [Fact]
    public async Task Existing_Postgres_scorecard_schema_upgrades_without_data_loss()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        if (!database.Available) return;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;

        var scorecardId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(options);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            );
            CREATE TABLE "EvaluationScorecards" (
                "Id" uuid NOT NULL,
                "Name" character varying(120) NOT NULL,
                "Description" character varying(500) NOT NULL,
                "MinimumGroundedness" double precision NOT NULL,
                "MinimumRelevance" double precision NOT NULL,
                "MinimumSafety" double precision NOT NULL,
                "MinimumOverallScore" double precision NOT NULL,
                "FailureAction" character varying(80) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_EvaluationScorecards" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX "IX_EvaluationScorecards_Name" ON "EvaluationScorecards" ("Name");
            CREATE INDEX "IX_EvaluationScorecards_UpdatedAt" ON "EvaluationScorecards" ("UpdatedAt");
            CREATE TABLE "KnowledgeCollections" ("Id" uuid NOT NULL CONSTRAINT "PK_KnowledgeCollections" PRIMARY KEY);
            CREATE TABLE "Prompts" ("Id" uuid NOT NULL CONSTRAINT "PK_Prompts" PRIMARY KEY);
            CREATE TABLE "Workflows" ("Id" uuid NOT NULL CONSTRAINT "PK_Workflows" PRIMARY KEY);
            CREATE TABLE "ConversationSimulations" ("Id" uuid NOT NULL CONSTRAINT "PK_ConversationSimulations" PRIMARY KEY);
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
                $"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({migrationId}, {"8.0.13"})");
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "EvaluationScorecards"
                ("Id", "Name", "Description", "MinimumGroundedness", "MinimumRelevance", "MinimumSafety",
                 "MinimumOverallScore", "FailureAction", "CreatedAt", "UpdatedAt")
            VALUES ({scorecardId}, {"Upgrade evidence"}, {"Must survive PostgreSQL expansion"},
                    {0.81}, {0.82}, {0.99}, {0.86}, {"Review"}, {DateTimeOffset.UtcNow}, {DateTimeOffset.UtcNow})
            """);

        await db.Database.MigrateAsync();
        var repository = new EfEvaluationStudioRepository(db);
        await repository.BackfillLegacyScorecardsAsync();
        await repository.BackfillLegacyScorecardsAsync();
        var scorecard = await repository.GetScorecardAsync(scorecardId);

        Assert.NotNull(scorecard);
        Assert.Equal("1.0", scorecard.Version);
        Assert.Equal(3, scorecard.Metrics.Count);
        Assert.Equal(3, await db.EvaluationMetricDefinitions.CountAsync(item => item.ScorecardId == scorecardId));
    }

    private sealed class TemporaryPostgresDatabase : IAsyncDisposable
    {
        private readonly string? adminConnectionString;
        private readonly string? databaseName;
        public bool Available => ConnectionString is not null;
        public string? ConnectionString { get; }

        private TemporaryPostgresDatabase(string? connectionString, string? adminConnectionString, string? databaseName)
            => (ConnectionString, this.adminConnectionString, this.databaseName) = (connectionString, adminConnectionString, databaseName);

        public static async Task<TemporaryPostgresDatabase> CreateAsync()
        {
            var configured = Environment.GetEnvironmentVariable("CONVOLAB_POSTGRES_TEST_CONNECTION");
            if (string.IsNullOrWhiteSpace(configured)) return new(null, null, null);
            var testName = $"convolab_test_{Guid.NewGuid():N}";
            var admin = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres" };
            await using var connection = new NpgsqlConnection(admin.ConnectionString);
            await connection.OpenAsync();
            await using (var command = new NpgsqlCommand($"CREATE DATABASE \"{testName}\"", connection)) await command.ExecuteNonQueryAsync();
            var test = new NpgsqlConnectionStringBuilder(configured) { Database = testName };
            return new(test.ConnectionString, admin.ConnectionString, testName);
        }

        public async ValueTask DisposeAsync()
        {
            if (adminConnectionString is null || databaseName is null || !databaseName.StartsWith("convolab_test_", StringComparison.Ordinal)) return;
            await using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();
            await using (var terminate = new NpgsqlCommand("SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name", connection))
            {
                terminate.Parameters.AddWithValue("name", databaseName);
                await terminate.ExecuteNonQueryAsync();
            }
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\"", connection);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
