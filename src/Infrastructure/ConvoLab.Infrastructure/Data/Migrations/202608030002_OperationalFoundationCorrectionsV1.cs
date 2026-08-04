using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConvoLab.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608030002_OperationalFoundationCorrectionsV1")]
public sealed class OperationalFoundationCorrectionsV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>("LeaseToken", "OperationalWorkerHeartbeats", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<DateTimeOffset>("LastIterationStartedAt", "OperationalWorkerHeartbeats", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("LastIterationCompletedAt", "OperationalWorkerHeartbeats", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("LastDegradedIterationAt", "OperationalWorkerHeartbeats", nullable: true);
        migrationBuilder.AddColumn<string>("LastFailureCode", "OperationalWorkerHeartbeats", maxLength: 160, nullable: true);
        migrationBuilder.AddColumn<int>("LastOutboxProcessed", "OperationalWorkerHeartbeats", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>("LastOutboxFailed", "OperationalWorkerHeartbeats", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>("LastExportsCompleted", "OperationalWorkerHeartbeats", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>("LastExportsFailed", "OperationalWorkerHeartbeats", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>("LastAggregateBucketsCompleted", "OperationalWorkerHeartbeats", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>("LastAggregateBucketsFailed", "OperationalWorkerHeartbeats", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>("LastRetentionRowsRemoved", "OperationalWorkerHeartbeats", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<long>("CumulativeProcessedCount", "OperationalWorkerHeartbeats", nullable: false, defaultValue: 0L);

        migrationBuilder.AddColumn<string>("ProcessingOwner", "AnalyticsExports", maxLength: 160, nullable: true);
        migrationBuilder.AddColumn<long>("ProcessingLeaseToken", "AnalyticsExports", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("ProcessingStartedAt", "AnalyticsExports", nullable: true);
        migrationBuilder.AddColumn<int>("AttemptCount", "AnalyticsExports", nullable: false, defaultValue: 0);
        migrationBuilder.CreateIndex(
            "IX_AnalyticsExports_Status_ProcessingStartedAt",
            "AnalyticsExports",
            new[] { "Status", "ProcessingStartedAt" });

        migrationBuilder.Sql("""
            UPDATE "OperationalWorkerHeartbeats"
            SET "CumulativeProcessedCount" = "ProcessedCount";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_AnalyticsExports_Status_ProcessingStartedAt", "AnalyticsExports");
        migrationBuilder.DropColumn("AttemptCount", "AnalyticsExports");
        migrationBuilder.DropColumn("ProcessingStartedAt", "AnalyticsExports");
        migrationBuilder.DropColumn("ProcessingLeaseToken", "AnalyticsExports");
        migrationBuilder.DropColumn("ProcessingOwner", "AnalyticsExports");

        migrationBuilder.DropColumn("CumulativeProcessedCount", "OperationalWorkerHeartbeats");
        migrationBuilder.DropColumn("LastRetentionRowsRemoved", "OperationalWorkerHeartbeats");
        migrationBuilder.DropColumn("LastAggregateBucketsFailed", "OperationalWorkerHeartbeats");
        migrationBuilder.DropColumn("LastAggregateBucketsCompleted", "OperationalWorkerHeartbeats");
        migrationBuilder.DropColumn("LastExportsFailed", "OperationalWorkerHeartbeats");
        migrationBuilder.DropColumn("LastExportsCompleted", "OperationalWorkerHeartbeats");
        migrationBuilder.DropColumn("LastOutboxFailed", "OperationalWorkerHeartbeats");
        migrationBuilder.DropColumn("LastOutboxProcessed", "OperationalWorkerHeartbeats");
        migrationBuilder.DropColumn("LastFailureCode", "OperationalWorkerHeartbeats");
        migrationBuilder.DropColumn("LastDegradedIterationAt", "OperationalWorkerHeartbeats");
        migrationBuilder.DropColumn("LastIterationCompletedAt", "OperationalWorkerHeartbeats");
        migrationBuilder.DropColumn("LastIterationStartedAt", "OperationalWorkerHeartbeats");
        migrationBuilder.DropColumn("LeaseToken", "OperationalWorkerHeartbeats");
    }
}
