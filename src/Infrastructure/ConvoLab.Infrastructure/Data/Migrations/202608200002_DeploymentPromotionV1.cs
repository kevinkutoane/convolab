using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConvoLab.Infrastructure.Data.Migrations
{
    [Migration("202608200002_DeploymentPromotionV1")]
    public partial class DeploymentPromotionV1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeploymentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReleaseManifestId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ReleaseVersion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SourceCommitSha = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ApiImageDigest = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StudioImageDigest = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MigrationVersion = table.Column<string>(type: "text", nullable: true),
                    SbomSha256 = table.Column<string>(type: "text", nullable: true),
                    ProvenanceReference = table.Column<string>(type: "text", nullable: true),
                    Environment = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovalReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BackupIdBeforeMigration = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HealthCheckSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SmokeTestSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PreviousReleaseManifestId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentRecords_Environment_CreatedAt",
                table: "DeploymentRecords",
                columns: new[] { "Environment", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentRecords_ReleaseManifestId",
                table: "DeploymentRecords",
                column: "ReleaseManifestId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeploymentRecords");
        }
    }
}
