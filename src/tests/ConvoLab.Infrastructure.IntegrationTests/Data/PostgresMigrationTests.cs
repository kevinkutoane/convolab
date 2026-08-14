using ConvoLab.Application.Simulation;
using ConvoLab.Application.Operations;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.Analytics;
using ConvoLab.Infrastructure.EvaluationStudio;
using ConvoLab.Infrastructure.Operations;
using ConvoLab.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit.Abstractions;

namespace ConvoLab.Infrastructure.IntegrationTests.Data;

public sealed class PostgresMigrationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Worker_lease_uses_Postgres_time_and_preserves_single_ownership()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        if (!database.Available) return;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;
        await using (var migration = new ApplicationDbContext(options))
            await migration.Database.MigrateAsync();

        await using var fastServices = BuildLeaseServices(
            database.ConnectionString!,
            new OffsetTimeProvider(TimeSpan.FromDays(3650)));
        await using var slowServices = BuildLeaseServices(
            database.ConnectionString!,
            new OffsetTimeProvider(TimeSpan.FromDays(-3650)));
        var previousInstanceId = Environment.GetEnvironmentVariable("CONVOLAB_INSTANCE_ID");
        OperationalWorkerLease fastLease;
        OperationalWorkerLease slowLease;
        try
        {
            Environment.SetEnvironmentVariable("CONVOLAB_INSTANCE_ID", "postgres-lease-fast-clock");
            fastLease = new OperationalWorkerLease(
                fastServices.GetRequiredService<IServiceScopeFactory>(),
                fastServices.GetRequiredService<TimeProvider>());
            Environment.SetEnvironmentVariable("CONVOLAB_INSTANCE_ID", "postgres-lease-slow-clock");
            slowLease = new OperationalWorkerLease(
                slowServices.GetRequiredService<IServiceScopeFactory>(),
                slowServices.GetRequiredService<TimeProvider>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONVOLAB_INSTANCE_ID", previousInstanceId);
        }

        const string workerName = "postgres-live-lease-test";
        Assert.True(await fastLease.AcquireOrRenewAsync(workerName));
        Assert.False(await slowLease.AcquireOrRenewAsync(workerName));

        await using (var evidence = new ApplicationDbContext(options))
        {
            var remainingSeconds = await RemainingLeaseSecondsAsync(evidence, workerName);
            Assert.InRange(remainingSeconds, 110, 120);
            Assert.Equal(fastLease.InstanceId, (await evidence.OperationalWorkerHeartbeats
                .AsNoTracking().SingleAsync(item => item.WorkerName == workerName)).InstanceId);

            // Simulate a crashed owner whose server-derived lease has expired.
            await evidence.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "OperationalWorkerHeartbeats"
                SET "LeaseExpiresAt" = clock_timestamp() - interval '1 second'
                WHERE "WorkerName" = {workerName};
                """);
        }

        Assert.True(await slowLease.AcquireOrRenewAsync(workerName));
        await fastLease.RecordSuccessAsync(workerName, 100);
        await slowLease.RecordSuccessAsync(workerName, 1);

        await using (var takeoverEvidence = new ApplicationDbContext(options))
        {
            var record = await takeoverEvidence.OperationalWorkerHeartbeats
                .AsNoTracking().SingleAsync(item => item.WorkerName == workerName);
            Assert.Equal(slowLease.InstanceId, record.InstanceId);
            Assert.Equal(1, record.ProcessedCount);
            Assert.InRange(await RemainingLeaseSecondsAsync(takeoverEvidence, workerName), 110, 120);
        }

        const string contentionWorker = "postgres-live-contention-test";
        var attempts = await Task.WhenAll(
            fastLease.AcquireOrRenewAsync(contentionWorker),
            slowLease.AcquireOrRenewAsync(contentionWorker));
        Assert.Single(attempts, acquired => acquired);
    }

    [Fact]
    public async Task Worker_lease_renews_long_work_and_fences_a_stale_owner()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        if (!database.Available) return;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;
        await using (var migration = new ApplicationDbContext(options))
            await migration.Database.MigrateAsync();
        await using var firstServices = BuildLeaseServices(
            database.ConnectionString!, TimeProvider.System);
        await using var secondServices = BuildLeaseServices(
            database.ConnectionString!, TimeProvider.System);
        var shortLease = Options.Create(new AnalyticsWorkerOptions
        {
            LeaseDurationSeconds = 2,
            LeaseRenewalSeconds = 1,
            PollIntervalSeconds = 1,
            MaximumBatchSize = 10
        });
        var previousInstanceId = Environment.GetEnvironmentVariable("CONVOLAB_INSTANCE_ID");
        OperationalWorkerLease first;
        OperationalWorkerLease second;
        try
        {
            Environment.SetEnvironmentVariable("CONVOLAB_INSTANCE_ID", "renewing-owner");
            first = new(
                firstServices.GetRequiredService<IServiceScopeFactory>(),
                TimeProvider.System,
                shortLease);
            Environment.SetEnvironmentVariable("CONVOLAB_INSTANCE_ID", "contending-owner");
            second = new(
                secondServices.GetRequiredService<IServiceScopeFactory>(),
                TimeProvider.System,
                shortLease);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONVOLAB_INSTANCE_ID", previousInstanceId);
        }

        const string worker = "long-running-renewal-test";
        var firstLease = Assert.IsType<WorkerLeaseHandle>(await first.TryAcquireAsync(worker));
        Assert.True(await first.IsOwnedAsync(firstLease));
        await Task.Delay(1200);
        Assert.True(await first.RenewAsync(firstLease));
        DateTimeOffset firstRenewalExpiry;
        await using (var firstRenewalEvidence = new ApplicationDbContext(options))
            firstRenewalExpiry = await firstRenewalEvidence.OperationalWorkerHeartbeats
                .Where(item => item.WorkerName == worker)
                .Select(item => item.LeaseExpiresAt)
                .SingleAsync();
        await Task.Delay(1200);
        Assert.Null(await second.TryAcquireAsync(worker));
        Assert.True(await first.RenewAsync(firstLease));
        DateTimeOffset secondRenewalExpiry;
        await using (var secondRenewalEvidence = new ApplicationDbContext(options))
            secondRenewalExpiry = await secondRenewalEvidence.OperationalWorkerHeartbeats
                .Where(item => item.WorkerName == worker)
                .Select(item => item.LeaseExpiresAt)
                .SingleAsync();
        Assert.True(secondRenewalExpiry > firstRenewalExpiry);

        await Task.Delay(2200);
        var takeover = Assert.IsType<WorkerLeaseHandle>(await second.TryAcquireAsync(worker));
        Assert.True(takeover.Token > firstLease.Token);
        Assert.False(await first.IsOwnedAsync(firstLease));
        Assert.True(await second.IsOwnedAsync(takeover));
        Assert.False(await first.RecordResultAsync(
            firstLease,
            AnalyticsMaintenanceResult.Empty with { OutboxProcessed = 99 }));
        Assert.True(await second.RecordResultAsync(
            takeover,
            AnalyticsMaintenanceResult.Empty with { OutboxProcessed = 1 }));

        await using var evidence = new ApplicationDbContext(options);
        var record = await evidence.OperationalWorkerHeartbeats.AsNoTracking()
            .SingleAsync(item => item.WorkerName == worker);
        Assert.Equal("contending-owner", record.InstanceId);
        Assert.Equal(takeover.Token, record.LeaseToken);
        Assert.Equal(1, record.LastOutboxProcessed);
        Assert.Equal(1, record.CumulativeProcessedCount);
        output.WriteLine(
            "owner={0}; initialToken={1}; initialExpiry={2:O}; firstRenewalExpiry={3:O}; secondRenewalExpiry={4:O}; contenderDuringRenewal=denied; takeoverToken={5}; staleFinalWrite=rejected; finalOutboxProcessed={6}",
            firstLease.Owner,
            firstLease.Token,
            firstLease.LeaseExpiresAt,
            firstRenewalExpiry,
            secondRenewalExpiry,
            takeover.Token,
            record.LastOutboxProcessed);
    }

    [Fact]
    public async Task Analytics_export_claim_is_atomic_fenced_and_retries_abandoned_processing()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        if (!database.Available) return;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;
        var workerName = "analytics-export-claim-test";
        var owner = "claim-owner";
        var firstExportId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.MigrateAsync();
            var now = DateTimeOffset.UtcNow;
            setup.OperationalWorkerHeartbeats.Add(new OperationalWorkerHeartbeatRecord
            {
                WorkerName = workerName,
                InstanceId = owner,
                StartedAt = now,
                LastHeartbeatAt = now,
                CurrentStatus = "Running",
                LeaseToken = 7,
                LeaseExpiresAt = now.AddMinutes(2),
                Revision = 1
            });
            setup.AnalyticsExports.Add(Export(firstExportId, now));
            await setup.SaveChangesAsync();
        }
        var leaseHandle = new WorkerLeaseHandle(
            workerName, owner, 7, DateTimeOffset.UtcNow.AddMinutes(2));

        await using var contenderOne = new ApplicationDbContext(options);
        await using var contenderTwo = new ApplicationDbContext(options);
        var claims = await Task.WhenAll(
            AnalyticsExportClaims.ClaimAsync(contenderOne, leaseHandle, 120, 10, CancellationToken.None),
            AnalyticsExportClaims.ClaimAsync(contenderTwo, leaseHandle, 120, 10, CancellationToken.None));
        Assert.Equal(1, claims.Sum(result => result.Count(item => item.Id == firstExportId)));

        await using (var evidence = new ApplicationDbContext(options))
        {
            var claimed = await evidence.AnalyticsExports.AsNoTracking()
                .SingleAsync(item => item.Id == firstExportId);
            Assert.Equal("Processing", claimed.Status);
            Assert.Equal(owner, claimed.ProcessingOwner);
            Assert.Equal(7, claimed.ProcessingLeaseToken);
            Assert.Equal(1, claimed.AttemptCount);
            await evidence.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "AnalyticsExports"
                SET "ProcessingStartedAt" = clock_timestamp() - interval '121 seconds'
                WHERE "Id" = {firstExportId};
                """);
        }

        await using (var retry = new ApplicationDbContext(options))
        {
            var retried = await AnalyticsExportClaims.ClaimAsync(
                retry, leaseHandle, 120, 10, CancellationToken.None);
            Assert.Contains(retried, item => item.Id == firstExportId);
        }
        var secondExportId = Guid.NewGuid();
        await using (var takeover = new ApplicationDbContext(options))
        {
            takeover.AnalyticsExports.Add(Export(secondExportId, DateTimeOffset.UtcNow));
            var worker = await takeover.OperationalWorkerHeartbeats.SingleAsync(item =>
                item.WorkerName == workerName);
            worker.InstanceId = "takeover-owner";
            worker.LeaseToken = 8;
            worker.LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2);
            await takeover.SaveChangesAsync();
        }
        await using (var stale = new ApplicationDbContext(options))
        {
            Assert.Empty(await AnalyticsExportClaims.ClaimAsync(
                stale, leaseHandle, 120, 10, CancellationToken.None));
        }
        await using (var current = new ApplicationDbContext(options))
        {
            var currentLease = new WorkerLeaseHandle(
                workerName, "takeover-owner", 8, DateTimeOffset.UtcNow.AddMinutes(2));
            var claimed = await AnalyticsExportClaims.ClaimAsync(
                current, currentLease, 120, 10, CancellationToken.None);
            Assert.Contains(claimed, item => item.Id == secondExportId);
        }

        static AnalyticsExportRecord Export(Guid id, DateTimeOffset now) => new()
        {
            Id = id,
            WorkspaceId = Guid.NewGuid(),
            CreatedBy = Guid.NewGuid(),
            Status = "Pending",
            FileName = $"analytics-{id:N}.csv",
            FiltersJson = "{}",
            CreatedAt = now,
            ExpiresAt = now.AddDays(7)
        };
    }

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
            var known = first.Database.GetMigrations().ToArray();
            var applied = (await first.Database.GetAppliedMigrationsAsync()).ToArray();
            Assert.Equal(known, applied);
            Assert.Empty(await first.Database.GetPendingMigrationsAsync());
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
    public async Task Alpha13_database_upgrades_with_idempotent_analytics_backfill_and_restart()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        if (!database.Available) return;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;
        Guid simulationId;

        await using (var alpha13 = new ApplicationDbContext(options))
        {
            var migrator = alpha13.Database.GetService<IMigrator>();
            var alpha13Migration = alpha13.Database.GetMigrations()
                .Single(id => id.EndsWith("_EnvironmentSettingsManagementV1", StringComparison.Ordinal));
            var targetName = alpha13.Database.GetService<IMigrationsIdGenerator>().GetName(alpha13Migration);
            await migrator.MigrateAsync(targetName);
            simulationId = (await new EfConversationSimulationStore(alpha13).AddAsync(
                new CreateSimulationCommand("Alpha 13 analytics upgrade", "Workflow", "Prompt", "Knowledge"))).Id;
        }

        await using (var upgraded = new ApplicationDbContext(options))
        {
            await upgraded.Database.MigrateAsync();
            await upgraded.Database.MigrateAsync();
            Assert.Empty(await upgraded.Database.GetPendingMigrationsAsync());
            Assert.Equal(1, await upgraded.ExecutionAttributions.CountAsync(item =>
                item.SourceResourceType == "Simulation" && item.SourceResourceId == simulationId));
            var attribution = await upgraded.ExecutionAttributions.SingleAsync(item => item.SourceResourceId == simulationId);
            Assert.Equal("BackfilledDefaultEnvironment", attribution.AttributionStatus);
            Assert.Equal("legacy:alpha13-unattributed", attribution.ConfigurationRevision);
        }

        await using (var restarted = new ApplicationDbContext(options))
        {
            Assert.Equal(1, await restarted.ExecutionAttributions.CountAsync(item =>
                item.SourceResourceType == "Simulation" && item.SourceResourceId == simulationId));
            Assert.NotNull(await new EfConversationSimulationStore(restarted).GetAsync(simulationId));
        }
    }

    [Fact]
    public async Task Entra_correction_Postgres_upgrade_preserves_existing_authentication_records()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        if (!database.Available) return;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.ConnectionString).Options;
        var userId = Guid.NewGuid();
        await using (var before = new ApplicationDbContext(options))
        {
            var migrator = before.Database.GetService<IMigrator>();
            var entraMigration = before.Database.GetMigrations()
                .Single(id => id.EndsWith("_EntraHybridAuthenticationV1", StringComparison.Ordinal));
            await migrator.MigrateAsync(before.Database.GetService<IMigrationsIdGenerator>().GetName(entraMigration));
            await before.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "IdentityUsers"
                    ("Id", "Email", "NormalizedEmail", "DisplayName", "Status", "IsPlatformAdministrator",
                     "CreatedAt", "UpdatedAt", "Revision")
                VALUES ({userId}, {"postgres-preserved@example.test"}, {"POSTGRES-PRESERVED@EXAMPLE.TEST"},
                        {"Postgres preserved user"}, {"Active"}, {true}, {DateTimeOffset.UtcNow},
                        {DateTimeOffset.UtcNow}, {4});
                INSERT INTO "LocalCredentials"
                    ("UserId", "PasswordHash", "FailedAttempts", "LockedUntil", "UpdatedAt")
                VALUES ({userId}, {"postgres-preserved-hash"}, {3}, {DateTimeOffset.UtcNow.AddMinutes(4)},
                        {DateTimeOffset.UtcNow});
                """);
        }

        await using (var corrected = new ApplicationDbContext(options))
        {
            await corrected.Database.MigrateAsync();
            var credential = await corrected.LocalCredentials.AsNoTracking().SingleAsync();
            Assert.Equal("postgres-preserved-hash", credential.PasswordHash);
            Assert.Equal(3, credential.FailedAttempts);
            Assert.Equal(0, credential.BreakGlassFailedAttempts);
            Assert.Null(credential.BreakGlassLockedUntil);
            Assert.Null(credential.BreakGlassLastFailedAt);
            Assert.Equal(1, credential.BreakGlassRevision);
            Assert.Empty(await corrected.Database.GetPendingMigrationsAsync());
        }
    }

    [Fact]
    public async Task Alpha14_analytics_database_upgrades_through_completion_migration()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        if (!database.Available) return;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;
        var eventId = Guid.NewGuid();
        var executionId = Guid.NewGuid();

        await using (var alpha14 = new ApplicationDbContext(options))
        {
            var migrator = alpha14.Database.GetService<IMigrator>();
            // The repository deliberately retains its established 12-digit
            // migration IDs. Ask the configured EF ID generator for the target
            // name rather than assuming the conventional 14-digit split point.
            var analyticsMigration = alpha14.Database.GetMigrations()
                .Single(id => id.EndsWith("_PlatformAnalyticsV1", StringComparison.Ordinal));
            var targetName = alpha14.Database.GetService<IMigrationsIdGenerator>()
                .GetName(analyticsMigration);
            await migrator.MigrateAsync(targetName);
            Assert.Contains(
                "202607240001_PlatformAnalyticsV1",
                await alpha14.Database.GetAppliedMigrationsAsync());
            var scope = await alpha14.RuntimeEnvironments.AsNoTracking()
                .Join(
                    alpha14.Workspaces.AsNoTracking(),
                    environment => environment.WorkspaceId,
                    workspace => workspace.Id,
                    (environment, workspace) => new
                    {
                        OrganisationId = workspace.OrganisationId,
                        WorkspaceId = workspace.Id,
                        EnvironmentId = environment.Id
                    })
                .FirstAsync();
            await alpha14.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AnalyticsEvents"
                    ("Id", "EventKey", "OrganisationId", "WorkspaceId", "EnvironmentId",
                     "ActorId", "ActorType", "ActorRole", "Capability", "EventType",
                     "Outcome", "Provider", "Model", "InputTokens", "OutputTokens",
                     "CostZar", "CostType", "PricingRevision", "DurationMs", "QualityScore",
                     "ProviderInvocationPrevented", "SourceType", "SourceId", "PromptName",
                     "WorkflowName", "ConfigurationRevision", "CorrelationId", "OccurredAt")
                VALUES
                    ({eventId}, {$"event-{eventId:N}"}, {scope.OrganisationId},
                     {scope.WorkspaceId}, {scope.EnvironmentId}, {null}, {"System"}, {null},
                     {"Simulation"}, {"SimulationExecution"}, {"Succeeded"},
                     {"Deterministic"}, {"convolab-deterministic-primary"}, {12}, {8},
                     {0.001m}, {"Estimated"}, {"pricing:test"}, {25d}, {0.9d}, {false},
                     {"SimulationRun"}, {executionId}, {"Prompt v1"}, {"Workflow v1"},
                     {"sha256:alpha14"}, {"alpha14-upgrade"}, {DateTimeOffset.UtcNow});
                """);
        }

        await using (var upgraded = new ApplicationDbContext(options))
        {
            await upgraded.Database.MigrateAsync();
            await upgraded.Database.MigrateAsync();
            Assert.Empty(await upgraded.Database.GetPendingMigrationsAsync());
            var analyticsEvent = await upgraded.AnalyticsEvents.SingleAsync(item => item.Id == eventId);
            Assert.Equal(executionId, analyticsEvent.SourceExecutionId);
            Assert.Null(analyticsEvent.KnowledgeCollectionName);
        }

        await using (var restarted = new ApplicationDbContext(options))
        {
            Assert.Equal(
                executionId,
                await restarted.AnalyticsEvents
                    .Where(item => item.Id == eventId)
                    .Select(item => item.SourceExecutionId)
                    .SingleAsync());
        }
    }

    [Fact]
    public async Task Operational_foundation_schema_upgrades_and_preserves_safe_mode_and_worker_evidence()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        if (!database.Available) return;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;
        await using (var foundation = new ApplicationDbContext(options))
        {
            var migrator = foundation.Database.GetService<IMigrator>();
            var foundationMigration = foundation.Database.GetMigrations()
                .Single(id => id.EndsWith("_OperationalFoundationV1", StringComparison.Ordinal));
            await migrator.MigrateAsync(
                foundation.Database.GetService<IMigrationsIdGenerator>().GetName(foundationMigration));
            await foundation.Database.ExecuteSqlRawAsync("""
                UPDATE "PlatformOperationalSettings"
                SET "SafeModeEnabled" = TRUE,
                    "SafeModeReason" = 'preserved correction evidence',
                    "Revision" = 9
                WHERE "Key" = 'platform';
                INSERT INTO "OperationalWorkerHeartbeats"
                    ("WorkerName", "InstanceId", "StartedAt", "LastHeartbeatAt",
                     "CurrentStatus", "ProcessedCount", "LeaseExpiresAt", "Revision")
                VALUES
                    ('analytics-maintenance', 'foundation-owner', clock_timestamp(),
                     clock_timestamp(), 'Running', 7,
                     clock_timestamp() + interval '2 minutes', 4);
                """);
        }

        await using (var corrected = new ApplicationDbContext(options))
        {
            await corrected.Database.MigrateAsync();
            var safeMode = await corrected.PlatformOperationalSettings.AsNoTracking().SingleAsync();
            var worker = await corrected.OperationalWorkerHeartbeats.AsNoTracking().SingleAsync();
            Assert.True(safeMode.SafeModeEnabled);
            Assert.Equal("preserved correction evidence", safeMode.SafeModeReason);
            Assert.Equal(9, safeMode.Revision);
            Assert.Equal("foundation-owner", worker.InstanceId);
            Assert.Equal(7, worker.CumulativeProcessedCount);
            Assert.Equal(0, worker.LeaseToken);
            Assert.Empty(await corrected.Database.GetPendingMigrationsAsync());
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

    private static ServiceProvider BuildLeaseServices(string connectionString, TimeProvider timeProvider)
    {
        var services = new ServiceCollection();
        services.AddSingleton(timeProvider);
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        return services.BuildServiceProvider();
    }

    private static async Task<double> RemainingLeaseSecondsAsync(
        ApplicationDbContext db,
        string workerName)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT EXTRACT(EPOCH FROM ("LeaseExpiresAt" - clock_timestamp()))
            FROM "OperationalWorkerHeartbeats"
            WHERE "WorkerName" = @workerName;
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "workerName";
        parameter.Value = workerName;
        command.Parameters.Add(parameter);
        await db.Database.OpenConnectionAsync();
        return Convert.ToDouble(await command.ExecuteScalarAsync());
    }

    private sealed class OffsetTimeProvider(TimeSpan offset) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.Add(offset);
    }
}
