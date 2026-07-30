using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConvoLab.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202607250001_PlatformAnalyticsCompletionV1")]
public sealed class PlatformAnalyticsCompletionV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("SourceExecutionId", "AnalyticsEvents", nullable: true);
        migrationBuilder.AddColumn<string>("KnowledgeCollectionName", "AnalyticsEvents", maxLength: 240, nullable: true);
        migrationBuilder.AddColumn<string>("PolicyOutcome", "AnalyticsEvents", maxLength: 40, nullable: true);
        migrationBuilder.AddColumn<string>("EvaluationOutcome", "AnalyticsEvents", maxLength: 40, nullable: true);
        migrationBuilder.AddColumn<double>("Groundedness", "AnalyticsEvents", nullable: true);
        migrationBuilder.AddColumn<double>("Relevance", "AnalyticsEvents", nullable: true);
        migrationBuilder.AddColumn<double>("Safety", "AnalyticsEvents", nullable: true);
        migrationBuilder.AddColumn<double>("OverallQuality", "AnalyticsEvents", nullable: true);

        foreach (var table in new[] { "AnalyticsHourlyAggregates", "AnalyticsDailyAggregates" })
        {
            migrationBuilder.AddColumn<Guid>("OrganisationId", table, nullable: false, defaultValue: Guid.Empty);
            migrationBuilder.AddColumn<long>("EventCount", table, nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>("ExecutionCount", table, nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>("SimulationCount", table, nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>("EvaluationCount", table, nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>("TraceCount", table, nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>("ReplayCount", table, nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>("ProviderInvocationCount", table, nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>("ProviderInvocationPreventedCount", table, nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>("PolicyEvaluationCount", table, nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>("PolicyAllowedCount", table, nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>("PolicyDeniedCount", table, nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>("PolicyWarningCount", table, nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>("PluginOperationCount", table, nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<double>("MaximumDurationMs", table, nullable: false, defaultValue: 0d);
            migrationBuilder.AddColumn<long>("DurationCount", table, nullable: false, defaultValue: 0L);
        }

        migrationBuilder.AddColumn<DateTimeOffset>("DirtyToUtc", "AnalyticsAggregationCheckpoints", nullable: true);
        migrationBuilder.AddColumn<Guid>("LastProcessedEventId", "AnalyticsAggregationCheckpoints", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("LastSuccessfulRunAt", "AnalyticsAggregationCheckpoints", nullable: true);
        migrationBuilder.AddColumn<string>("Status", "AnalyticsAggregationCheckpoints", maxLength: 30, nullable: false, defaultValue: "Pending");
        migrationBuilder.AddColumn<string>("FailureReason", "AnalyticsAggregationCheckpoints", maxLength: 2000, nullable: true);

        migrationBuilder.AddColumn<bool>("IncludeActor", "AnalyticsExports", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<bool>("IncludeCost", "AnalyticsExports", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<bool>("IncludeTokenUsage", "AnalyticsExports", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<bool>("IncludeProviderDetails", "AnalyticsExports", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<bool>("IncludeSensitiveSource", "AnalyticsExports", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<int>("RetentionDays", "AnalyticsExports", nullable: false, defaultValue: 7);

        migrationBuilder.Sql("""
            UPDATE "AnalyticsEvents"
            SET "SourceExecutionId" = "SourceId"
            WHERE "SourceExecutionId" IS NULL
              AND "SourceType" = 'SimulationRun';
            """);

        foreach (var table in new[] { "AnalyticsHourlyAggregates", "AnalyticsDailyAggregates" })
        {
            migrationBuilder.Sql($"""
                UPDATE "{table}"
                SET "OrganisationId" = COALESCE(
                        (SELECT w."OrganisationId" FROM "Workspaces" w WHERE w."Id" = "{table}"."WorkspaceId"),
                        '00000000-0000-0000-0000-000000000000'),
                    "EventCount" = "Executions",
                    "ExecutionCount" = CASE WHEN "Capability" = 'Simulation' THEN "Executions" ELSE 0 END,
                    "SimulationCount" = CASE WHEN "Capability" = 'Simulation' THEN "Executions" ELSE 0 END;
                """);
        }

        migrationBuilder.Sql("""
            UPDATE "AnalyticsAggregationCheckpoints"
            SET "DirtyFromUtc" = COALESCE("DirtyFromUtc", '1970-01-01T00:00:00+00:00'),
                "DirtyToUtc" = COALESCE("HighWatermarkUtc", CURRENT_TIMESTAMP),
                "Status" = 'Pending';
            """);

        migrationBuilder.CreateIndex(
            "IX_AnalyticsEvents_WorkspaceId_SourceExecutionId_OccurredAt",
            "AnalyticsEvents",
            new[] { "WorkspaceId", "SourceExecutionId", "OccurredAt" });
        migrationBuilder.CreateIndex(
            "IX_AnalyticsEvents_WorkspaceId_Provider_Model_OccurredAt",
            "AnalyticsEvents",
            new[] { "WorkspaceId", "Provider", "Model", "OccurredAt" });
        migrationBuilder.CreateIndex(
            "IX_AnalyticsEvents_WorkspaceId_ConfigurationRevision_OccurredAt",
            "AnalyticsEvents",
            new[] { "WorkspaceId", "ConfigurationRevision", "OccurredAt" });
        migrationBuilder.CreateIndex(
            "IX_AnalyticsEvents_WorkspaceId_EventType_OccurredAt",
            "AnalyticsEvents",
            new[] { "WorkspaceId", "EventType", "OccurredAt" });
        migrationBuilder.CreateIndex(
            "IX_AnalyticsEvents_WorkspaceId_Outcome_OccurredAt",
            "AnalyticsEvents",
            new[] { "WorkspaceId", "Outcome", "OccurredAt" });
        migrationBuilder.CreateIndex(
            "IX_AnalyticsEvents_WorkspaceId_ActorId_OccurredAt",
            "AnalyticsEvents",
            new[] { "WorkspaceId", "ActorId", "OccurredAt" });
        migrationBuilder.CreateIndex(
            "IX_AnalyticsEvents_WorkspaceId_PromptName_OccurredAt",
            "AnalyticsEvents",
            new[] { "WorkspaceId", "PromptName", "OccurredAt" });
        migrationBuilder.CreateIndex(
            "IX_AnalyticsEvents_WorkspaceId_WorkflowName_OccurredAt",
            "AnalyticsEvents",
            new[] { "WorkspaceId", "WorkflowName", "OccurredAt" });
        migrationBuilder.CreateIndex(
            "IX_AnalyticsEvents_WorkspaceId_CostType_OccurredAt",
            "AnalyticsEvents",
            new[] { "WorkspaceId", "CostType", "OccurredAt" });
        migrationBuilder.CreateIndex(
            "IX_AnalyticsHourlyAggregates_OrganisationId_WorkspaceId_BucketStart",
            "AnalyticsHourlyAggregates",
            new[] { "OrganisationId", "WorkspaceId", "BucketStart" });
        migrationBuilder.CreateIndex(
            "IX_AnalyticsDailyAggregates_OrganisationId_WorkspaceId_BucketStart",
            "AnalyticsDailyAggregates",
            new[] { "OrganisationId", "WorkspaceId", "BucketStart" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_AnalyticsEvents_WorkspaceId_SourceExecutionId_OccurredAt", "AnalyticsEvents");
        migrationBuilder.DropIndex("IX_AnalyticsEvents_WorkspaceId_Provider_Model_OccurredAt", "AnalyticsEvents");
        migrationBuilder.DropIndex("IX_AnalyticsEvents_WorkspaceId_ConfigurationRevision_OccurredAt", "AnalyticsEvents");
        migrationBuilder.DropIndex("IX_AnalyticsEvents_WorkspaceId_EventType_OccurredAt", "AnalyticsEvents");
        migrationBuilder.DropIndex("IX_AnalyticsEvents_WorkspaceId_Outcome_OccurredAt", "AnalyticsEvents");
        migrationBuilder.DropIndex("IX_AnalyticsEvents_WorkspaceId_ActorId_OccurredAt", "AnalyticsEvents");
        migrationBuilder.DropIndex("IX_AnalyticsEvents_WorkspaceId_PromptName_OccurredAt", "AnalyticsEvents");
        migrationBuilder.DropIndex("IX_AnalyticsEvents_WorkspaceId_WorkflowName_OccurredAt", "AnalyticsEvents");
        migrationBuilder.DropIndex("IX_AnalyticsEvents_WorkspaceId_CostType_OccurredAt", "AnalyticsEvents");
        migrationBuilder.DropIndex("IX_AnalyticsHourlyAggregates_OrganisationId_WorkspaceId_BucketStart", "AnalyticsHourlyAggregates");
        migrationBuilder.DropIndex("IX_AnalyticsDailyAggregates_OrganisationId_WorkspaceId_BucketStart", "AnalyticsDailyAggregates");

        migrationBuilder.DropColumn("SourceExecutionId", "AnalyticsEvents");
        migrationBuilder.DropColumn("KnowledgeCollectionName", "AnalyticsEvents");
        migrationBuilder.DropColumn("PolicyOutcome", "AnalyticsEvents");
        migrationBuilder.DropColumn("EvaluationOutcome", "AnalyticsEvents");
        migrationBuilder.DropColumn("Groundedness", "AnalyticsEvents");
        migrationBuilder.DropColumn("Relevance", "AnalyticsEvents");
        migrationBuilder.DropColumn("Safety", "AnalyticsEvents");
        migrationBuilder.DropColumn("OverallQuality", "AnalyticsEvents");

        foreach (var table in new[] { "AnalyticsHourlyAggregates", "AnalyticsDailyAggregates" })
        {
            foreach (var column in new[]
            {
                "OrganisationId", "EventCount", "ExecutionCount", "SimulationCount", "EvaluationCount",
                "TraceCount", "ReplayCount", "ProviderInvocationCount", "ProviderInvocationPreventedCount",
                "PolicyEvaluationCount", "PolicyAllowedCount", "PolicyDeniedCount", "PolicyWarningCount",
                "PluginOperationCount", "MaximumDurationMs", "DurationCount"
            })
                migrationBuilder.DropColumn(column, table);
        }

        foreach (var column in new[]
        {
            "DirtyToUtc", "LastProcessedEventId", "LastSuccessfulRunAt", "Status", "FailureReason"
        })
            migrationBuilder.DropColumn(column, "AnalyticsAggregationCheckpoints");

        foreach (var column in new[]
        {
            "IncludeActor", "IncludeCost", "IncludeTokenUsage", "IncludeProviderDetails",
            "IncludeSensitiveSource", "RetentionDays"
        })
            migrationBuilder.DropColumn(column, "AnalyticsExports");
    }
}
