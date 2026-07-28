using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConvoLab.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202607240001_PlatformAnalyticsV1")]
public sealed class PlatformAnalyticsV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("ConfigurationSnapshots", table => new
        {
            Id = table.Column<Guid>(nullable: false),
            OrganisationId = table.Column<Guid>(nullable: false),
            WorkspaceId = table.Column<Guid>(nullable: false),
            EnvironmentId = table.Column<Guid>(nullable: false),
            Revision = table.Column<string>(maxLength: 80, nullable: false),
            ValuesJson = table.Column<string>(nullable: false),
            ProvenanceJson = table.Column<string>(nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK_ConfigurationSnapshots", item => item.Id));
        migrationBuilder.CreateIndex(
            "IX_ConfigurationSnapshots_OrganisationId_WorkspaceId_EnvironmentId_Revision",
            "ConfigurationSnapshots",
            new[] { "OrganisationId", "WorkspaceId", "EnvironmentId", "Revision" },
            unique: true);

        migrationBuilder.CreateTable("ExecutionAttributions", table => new
        {
            Id = table.Column<Guid>(nullable: false),
            OrganisationId = table.Column<Guid>(nullable: false),
            WorkspaceId = table.Column<Guid>(nullable: false),
            EnvironmentId = table.Column<Guid>(nullable: false),
            ActorId = table.Column<Guid>(nullable: true),
            ActorType = table.Column<string>(maxLength: 40, nullable: false),
            ActorRole = table.Column<string>(maxLength: 50, nullable: true),
            SourceResourceType = table.Column<string>(maxLength: 80, nullable: false),
            SourceResourceId = table.Column<Guid>(nullable: false),
            ConfigurationRevision = table.Column<string>(maxLength: 80, nullable: false),
            CorrelationId = table.Column<string>(maxLength: 100, nullable: false),
            AttributionStatus = table.Column<string>(maxLength: 50, nullable: false),
            BackfilledAt = table.Column<DateTimeOffset>(nullable: true),
            BackfillVersion = table.Column<string>(maxLength: 50, nullable: true),
            CreatedAt = table.Column<DateTimeOffset>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK_ExecutionAttributions", item => item.Id));
        migrationBuilder.CreateIndex(
            "IX_ExecutionAttributions_SourceResourceType_SourceResourceId",
            "ExecutionAttributions",
            new[] { "SourceResourceType", "SourceResourceId" },
            unique: true);
        migrationBuilder.CreateIndex(
            "IX_ExecutionAttributions_WorkspaceId_EnvironmentId_CreatedAt",
            "ExecutionAttributions",
            new[] { "WorkspaceId", "EnvironmentId", "CreatedAt" });

        migrationBuilder.CreateTable("AnalyticsOutbox", table => new
        {
            Id = table.Column<Guid>(nullable: false),
            EventKey = table.Column<string>(maxLength: 64, nullable: false),
            PayloadJson = table.Column<string>(nullable: false),
            Status = table.Column<string>(maxLength: 30, nullable: false),
            Attempts = table.Column<int>(nullable: false),
            LastError = table.Column<string>(maxLength: 2000, nullable: true),
            AvailableAt = table.Column<DateTimeOffset>(nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(nullable: false),
            ProcessedAt = table.Column<DateTimeOffset>(nullable: true)
        }, constraints: table => table.PrimaryKey("PK_AnalyticsOutbox", item => item.Id));
        migrationBuilder.CreateIndex("IX_AnalyticsOutbox_EventKey", "AnalyticsOutbox", "EventKey", unique: true);
        migrationBuilder.CreateIndex("IX_AnalyticsOutbox_Status_AvailableAt", "AnalyticsOutbox", new[] { "Status", "AvailableAt" });

        migrationBuilder.CreateTable("AnalyticsEvents", table => new
        {
            Id = table.Column<Guid>(nullable: false),
            EventKey = table.Column<string>(maxLength: 64, nullable: false),
            OrganisationId = table.Column<Guid>(nullable: false),
            WorkspaceId = table.Column<Guid>(nullable: false),
            EnvironmentId = table.Column<Guid>(nullable: false),
            ActorId = table.Column<Guid>(nullable: true),
            ActorType = table.Column<string>(maxLength: 40, nullable: false),
            ActorRole = table.Column<string>(maxLength: 50, nullable: true),
            Capability = table.Column<string>(maxLength: 80, nullable: false),
            EventType = table.Column<string>(maxLength: 100, nullable: false),
            Outcome = table.Column<string>(maxLength: 40, nullable: false),
            Provider = table.Column<string>(maxLength: 100, nullable: true),
            Model = table.Column<string>(maxLength: 160, nullable: true),
            InputTokens = table.Column<int>(nullable: true),
            OutputTokens = table.Column<int>(nullable: true),
            CostZar = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
            CostType = table.Column<string>(maxLength: 30, nullable: false),
            PricingRevision = table.Column<string>(maxLength: 80, nullable: true),
            DurationMs = table.Column<double>(nullable: true),
            QualityScore = table.Column<double>(nullable: true),
            ProviderInvocationPrevented = table.Column<bool>(nullable: false),
            SourceType = table.Column<string>(maxLength: 80, nullable: false),
            SourceId = table.Column<Guid>(nullable: true),
            PromptName = table.Column<string>(maxLength: 240, nullable: true),
            WorkflowName = table.Column<string>(maxLength: 240, nullable: true),
            ConfigurationRevision = table.Column<string>(maxLength: 80, nullable: false),
            CorrelationId = table.Column<string>(maxLength: 100, nullable: false),
            OccurredAt = table.Column<DateTimeOffset>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK_AnalyticsEvents", item => item.Id));
        migrationBuilder.CreateIndex("IX_AnalyticsEvents_EventKey", "AnalyticsEvents", "EventKey", unique: true);
        migrationBuilder.CreateIndex("IX_AnalyticsEvents_WorkspaceId_EnvironmentId_OccurredAt", "AnalyticsEvents", new[] { "WorkspaceId", "EnvironmentId", "OccurredAt" });
        migrationBuilder.CreateIndex("IX_AnalyticsEvents_WorkspaceId_CorrelationId_OccurredAt", "AnalyticsEvents", new[] { "WorkspaceId", "CorrelationId", "OccurredAt" });
        migrationBuilder.CreateIndex("IX_AnalyticsEvents_WorkspaceId_Capability_OccurredAt", "AnalyticsEvents", new[] { "WorkspaceId", "Capability", "OccurredAt" });

        CreateAggregateTable(migrationBuilder, "AnalyticsHourlyAggregates");
        CreateAggregateTable(migrationBuilder, "AnalyticsDailyAggregates");

        migrationBuilder.CreateTable("AnalyticsAggregationCheckpoints", table => new
        {
            Id = table.Column<Guid>(nullable: false),
            WorkspaceId = table.Column<Guid>(nullable: false),
            Granularity = table.Column<string>(maxLength: 20, nullable: false),
            DirtyFromUtc = table.Column<DateTimeOffset>(nullable: true),
            HighWatermarkUtc = table.Column<DateTimeOffset>(nullable: true),
            Revision = table.Column<long>(nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK_AnalyticsAggregationCheckpoints", item => item.Id));
        migrationBuilder.CreateIndex(
            "IX_AnalyticsAggregationCheckpoints_WorkspaceId_Granularity",
            "AnalyticsAggregationCheckpoints",
            new[] { "WorkspaceId", "Granularity" },
            unique: true);

        migrationBuilder.CreateTable("AnalyticsExports", table => new
        {
            Id = table.Column<Guid>(nullable: false),
            WorkspaceId = table.Column<Guid>(nullable: false),
            CreatedBy = table.Column<Guid>(nullable: false),
            Status = table.Column<string>(maxLength: 30, nullable: false),
            FileName = table.Column<string>(maxLength: 240, nullable: false),
            FiltersJson = table.Column<string>(nullable: false),
            Content = table.Column<byte[]>(nullable: true),
            RowCount = table.Column<long>(nullable: true),
            SizeBytes = table.Column<long>(nullable: true),
            Checksum = table.Column<string>(maxLength: 80, nullable: true),
            FailureReason = table.Column<string>(maxLength: 2000, nullable: true),
            CreatedAt = table.Column<DateTimeOffset>(nullable: false),
            ExpiresAt = table.Column<DateTimeOffset>(nullable: false),
            CompletedAt = table.Column<DateTimeOffset>(nullable: true)
        }, constraints: table => table.PrimaryKey("PK_AnalyticsExports", item => item.Id));
        migrationBuilder.CreateIndex("IX_AnalyticsExports_WorkspaceId_CreatedAt", "AnalyticsExports", new[] { "WorkspaceId", "CreatedAt" });
        migrationBuilder.CreateIndex("IX_AnalyticsExports_Status_CreatedAt", "AnalyticsExports", new[] { "Status", "CreatedAt" });

        BackfillLegacyAttribution(migrationBuilder);
    }

    private static void CreateAggregateTable(MigrationBuilder migrationBuilder, string name)
    {
        migrationBuilder.CreateTable(name, table => new
        {
            Id = table.Column<Guid>(nullable: false),
            AggregateKey = table.Column<string>(maxLength: 64, nullable: false),
            WorkspaceId = table.Column<Guid>(nullable: false),
            EnvironmentId = table.Column<Guid>(nullable: false),
            BucketStart = table.Column<DateTimeOffset>(nullable: false),
            Provider = table.Column<string>(maxLength: 100, nullable: true),
            Model = table.Column<string>(maxLength: 160, nullable: true),
            Capability = table.Column<string>(maxLength: 80, nullable: true),
            Outcome = table.Column<string>(maxLength: 40, nullable: true),
            Executions = table.Column<long>(nullable: false),
            Succeeded = table.Column<long>(nullable: false),
            Failed = table.Column<long>(nullable: false),
            Denied = table.Column<long>(nullable: false),
            InputTokens = table.Column<long>(nullable: false),
            OutputTokens = table.Column<long>(nullable: false),
            ActualCostZar = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
            EstimatedCostZar = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
            UnknownCostCount = table.Column<long>(nullable: false),
            TotalDurationMs = table.Column<double>(nullable: false),
            TotalQualityScore = table.Column<double>(nullable: false),
            QualityCount = table.Column<long>(nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
        }, constraints: table => table.PrimaryKey($"PK_{name}", item => item.Id));
        migrationBuilder.CreateIndex($"IX_{name}_AggregateKey", name, "AggregateKey", unique: true);
        migrationBuilder.CreateIndex($"IX_{name}_WorkspaceId_EnvironmentId_BucketStart", name, new[] { "WorkspaceId", "EnvironmentId", "BucketStart" });
    }

    private void BackfillLegacyAttribution(MigrationBuilder migrationBuilder)
    {
        var now = "2026-07-24 00:00:00+00";
        var idExpression = ActiveProvider.Contains("Npgsql", StringComparison.Ordinal)
            ? "gen_random_uuid()"
            : "upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-4' || substr(upper(hex(randomblob(2))),2) || '-' || substr('89AB',abs(random()) % 4 + 1, 1) || substr(upper(hex(randomblob(2))),2) || '-' || upper(hex(randomblob(6)))";
        var timestamp = ActiveProvider.Contains("Npgsql", StringComparison.Ordinal)
            ? $"TIMESTAMPTZ '{now}'"
            : $"'{now}'";

        foreach (var (table, sourceType) in new[]
        {
            ("ConversationSimulations", "Simulation"),
            ("EvaluationRuns", "EvaluationRun"),
            ("Traces", "Trace"),
            ("ReplayExperiments", "ReplayExperiment"),
            ("PolicyDecisions", "PolicyDecision")
        })
        {
            migrationBuilder.Sql($"""
                INSERT INTO "ExecutionAttributions"
                    ("Id", "OrganisationId", "WorkspaceId", "EnvironmentId", "ActorId", "ActorType",
                     "ActorRole", "SourceResourceType", "SourceResourceId", "ConfigurationRevision",
                     "CorrelationId", "AttributionStatus", "BackfilledAt", "BackfillVersion", "CreatedAt")
                SELECT {idExpression}, e."OrganisationId", s."WorkspaceId", e."Id", NULL, 'System',
                       NULL, '{sourceType}', s."Id", 'legacy:alpha13-unattributed',
                       'legacy-alpha13', 'BackfilledDefaultEnvironment', {timestamp}, 'alpha.14', {timestamp}
                FROM "{table}" s
                JOIN "RuntimeEnvironments" e ON e."WorkspaceId" = s."WorkspaceId" AND e."IsDefault" = {DefaultBoolean}
                WHERE NOT EXISTS (
                    SELECT 1 FROM "ExecutionAttributions" a
                    WHERE a."SourceResourceType" = '{sourceType}' AND a."SourceResourceId" = s."Id"
                );
                """);
        }
    }

    private string DefaultBoolean => ActiveProvider.Contains("Npgsql", StringComparison.Ordinal) ? "true" : "1";

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("AnalyticsExports");
        migrationBuilder.DropTable("AnalyticsAggregationCheckpoints");
        migrationBuilder.DropTable("AnalyticsDailyAggregates");
        migrationBuilder.DropTable("AnalyticsHourlyAggregates");
        migrationBuilder.DropTable("AnalyticsEvents");
        migrationBuilder.DropTable("AnalyticsOutbox");
        migrationBuilder.DropTable("ExecutionAttributions");
        migrationBuilder.DropTable("ConfigurationSnapshots");
    }
}
