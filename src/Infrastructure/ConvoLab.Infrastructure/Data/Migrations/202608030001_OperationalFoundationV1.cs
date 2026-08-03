using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConvoLab.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608030001_OperationalFoundationV1")]
public sealed class OperationalFoundationV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("PlatformOperationalSettings", table => new
        {
            Key = table.Column<string>(maxLength: 80, nullable: false),
            SafeModeEnabled = table.Column<bool>(nullable: false),
            SafeModeReason = table.Column<string>(maxLength: 2000, nullable: true),
            ChangedBy = table.Column<Guid>(nullable: true),
            ChangedAt = table.Column<DateTimeOffset>(nullable: false),
            Revision = table.Column<long>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK_PlatformOperationalSettings", item => item.Key));

        migrationBuilder.CreateTable("OperationalWorkerHeartbeats", table => new
        {
            WorkerName = table.Column<string>(maxLength: 120, nullable: false),
            InstanceId = table.Column<string>(maxLength: 160, nullable: false),
            StartedAt = table.Column<DateTimeOffset>(nullable: false),
            LastHeartbeatAt = table.Column<DateTimeOffset>(nullable: false),
            LastSuccessfulIterationAt = table.Column<DateTimeOffset>(nullable: true),
            LastFailureAt = table.Column<DateTimeOffset>(nullable: true),
            LastFailureSummary = table.Column<string>(maxLength: 1000, nullable: true),
            CurrentStatus = table.Column<string>(maxLength: 40, nullable: false),
            ProcessedCount = table.Column<long>(nullable: false),
            LeaseExpiresAt = table.Column<DateTimeOffset>(nullable: false),
            Revision = table.Column<long>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK_OperationalWorkerHeartbeats", item => item.WorkerName));
        migrationBuilder.CreateIndex(
            "IX_OperationalWorkerHeartbeats_LeaseExpiresAt",
            "OperationalWorkerHeartbeats",
            "LeaseExpiresAt");

        if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            migrationBuilder.Sql("""
                INSERT INTO "PlatformOperationalSettings"
                    ("Key", "SafeModeEnabled", "ChangedAt", "Revision")
                VALUES ('platform', FALSE, TIMESTAMPTZ '1970-01-01 00:00:00+00', 1)
                ON CONFLICT ("Key") DO NOTHING;
                """);
        }
        else
        {
            migrationBuilder.Sql("""
                INSERT OR IGNORE INTO "PlatformOperationalSettings"
                    ("Key", "SafeModeEnabled", "ChangedAt", "Revision")
                VALUES ('platform', 0, '1970-01-01 00:00:00+00:00', 1);
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("OperationalWorkerHeartbeats");
        migrationBuilder.DropTable("PlatformOperationalSettings");
    }
}
