using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConvoLab.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202607220001_EvaluationStudioExpansionV1")]
public partial class EvaluationStudioExpansionV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_EvaluationScorecards_Name", table: "EvaluationScorecards");
        if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            migrationBuilder.AlterColumn<string>(name: "Name", table: "EvaluationScorecards", maxLength: 200, nullable: false, oldClrType: typeof(string), oldMaxLength: 120);
            migrationBuilder.AlterColumn<string>(name: "Description", table: "EvaluationScorecards", maxLength: 2000, nullable: false, oldClrType: typeof(string), oldMaxLength: 500);
        }
        migrationBuilder.AddColumn<string>(name: "Status", table: "EvaluationScorecards", maxLength: 50, nullable: false, defaultValue: "Published");
        migrationBuilder.AddColumn<string>(name: "Version", table: "EvaluationScorecards", maxLength: 50, nullable: false, defaultValue: "1.0");
        migrationBuilder.AddColumn<double>(name: "QualityGateThreshold", table: "EvaluationScorecards", nullable: false, defaultValue: 0d);
        migrationBuilder.AddColumn<bool>(name: "IsDefault", table: "EvaluationScorecards", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<long>(name: "Revision", table: "EvaluationScorecards", nullable: false, defaultValue: 1L);

        migrationBuilder.CreateTable(
            name: "EvaluationTestCases",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
                Description = table.Column<string>(maxLength: 2000, nullable: false),
                SimulationId = table.Column<Guid>(nullable: false),
                SourceRunId = table.Column<Guid>(nullable: false),
                ScorecardId = table.Column<Guid>(nullable: true),
                ExpectedVerdict = table.Column<string>(maxLength: 50, nullable: false),
                TagsJson = table.Column<string>(nullable: false),
                Status = table.Column<string>(maxLength: 50, nullable: false),
                Revision = table.Column<long>(nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_EvaluationTestCases", item => item.Id));

        migrationBuilder.CreateTable(
            name: "EvaluationMetricDefinitions",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                ScorecardId = table.Column<Guid>(nullable: false),
                Key = table.Column<string>(maxLength: 100, nullable: false),
                DisplayName = table.Column<string>(maxLength: 160, nullable: false),
                Description = table.Column<string>(maxLength: 1000, nullable: false),
                Weight = table.Column<double>(nullable: false),
                Threshold = table.Column<double>(nullable: false),
                Required = table.Column<bool>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EvaluationMetricDefinitions", item => item.Id);
                table.ForeignKey(
                    name: "FK_EvaluationMetricDefinitions_EvaluationScorecards_ScorecardId",
                    column: item => item.ScorecardId,
                    principalTable: "EvaluationScorecards",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "EvaluationRuns",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                SimulationId = table.Column<Guid>(nullable: false),
                SimulationTitle = table.Column<string>(maxLength: 240, nullable: false),
                SourceRunId = table.Column<Guid>(nullable: false),
                ScorecardId = table.Column<Guid>(nullable: false),
                ScorecardName = table.Column<string>(maxLength: 200, nullable: false),
                ScorecardVersion = table.Column<string>(maxLength: 50, nullable: false),
                Status = table.Column<string>(maxLength: 50, nullable: false),
                Verdict = table.Column<string>(maxLength: 50, nullable: false),
                OverallScore = table.Column<double>(nullable: false),
                FailureReason = table.Column<string>(nullable: true),
                ReviewStatus = table.Column<string>(maxLength: 50, nullable: false),
                ReviewNotes = table.Column<string>(nullable: true),
                Reviewer = table.Column<string>(maxLength: 200, nullable: true),
                ReviewedAt = table.Column<DateTimeOffset>(nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EvaluationRuns", item => item.Id);
                table.ForeignKey(
                    name: "FK_EvaluationRuns_EvaluationScorecards_ScorecardId",
                    column: item => item.ScorecardId,
                    principalTable: "EvaluationScorecards",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EvaluationBatches",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
                ScorecardId = table.Column<Guid>(nullable: false),
                ScorecardName = table.Column<string>(maxLength: 200, nullable: false),
                Status = table.Column<string>(maxLength: 50, nullable: false),
                StartedAt = table.Column<DateTimeOffset>(nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EvaluationBatches", item => item.Id);
                table.ForeignKey(
                    name: "FK_EvaluationBatches_EvaluationScorecards_ScorecardId",
                    column: item => item.ScorecardId,
                    principalTable: "EvaluationScorecards",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EvaluationMetricResults",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                EvaluationRunId = table.Column<Guid>(nullable: false),
                Key = table.Column<string>(maxLength: 100, nullable: false),
                DisplayName = table.Column<string>(maxLength: 160, nullable: false),
                Score = table.Column<double>(nullable: false),
                Threshold = table.Column<double>(nullable: false),
                Weight = table.Column<double>(nullable: false),
                Passed = table.Column<bool>(nullable: false),
                Detail = table.Column<string>(maxLength: 1000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EvaluationMetricResults", item => item.Id);
                table.ForeignKey(
                    name: "FK_EvaluationMetricResults_EvaluationRuns_EvaluationRunId",
                    column: item => item.EvaluationRunId,
                    principalTable: "EvaluationRuns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "EvaluationBatchItems",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                BatchId = table.Column<Guid>(nullable: false),
                TestCaseId = table.Column<Guid>(nullable: false),
                TestCaseName = table.Column<string>(maxLength: 200, nullable: false),
                EvaluationRunId = table.Column<Guid>(nullable: true),
                Status = table.Column<string>(maxLength: 50, nullable: false),
                ActualVerdict = table.Column<string>(maxLength: 50, nullable: false),
                ExpectedVerdict = table.Column<string>(maxLength: 50, nullable: false),
                Passed = table.Column<bool>(nullable: false),
                Detail = table.Column<string>(maxLength: 1000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EvaluationBatchItems", item => item.Id);
                table.ForeignKey(
                    name: "FK_EvaluationBatchItems_EvaluationBatches_BatchId",
                    column: item => item.BatchId,
                    principalTable: "EvaluationBatches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_EvaluationScorecards_Name_Version", table: "EvaluationScorecards", columns: new[] { "Name", "Version" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_EvaluationScorecards_Status_IsDefault", table: "EvaluationScorecards", columns: new[] { "Status", "IsDefault" });
        migrationBuilder.CreateIndex(name: "IX_EvaluationMetricDefinitions_ScorecardId_Key", table: "EvaluationMetricDefinitions", columns: new[] { "ScorecardId", "Key" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_EvaluationRuns_ScorecardId", table: "EvaluationRuns", column: "ScorecardId");
        migrationBuilder.CreateIndex(name: "IX_EvaluationRuns_SourceRunId_ScorecardId", table: "EvaluationRuns", columns: new[] { "SourceRunId", "ScorecardId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_EvaluationRuns_Verdict_CreatedAt", table: "EvaluationRuns", columns: new[] { "Verdict", "CreatedAt" });
        migrationBuilder.CreateIndex(name: "IX_EvaluationRuns_SimulationId", table: "EvaluationRuns", column: "SimulationId");
        migrationBuilder.CreateIndex(name: "IX_EvaluationMetricResults_EvaluationRunId_Key", table: "EvaluationMetricResults", columns: new[] { "EvaluationRunId", "Key" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_EvaluationTestCases_Status_UpdatedAt", table: "EvaluationTestCases", columns: new[] { "Status", "UpdatedAt" });
        migrationBuilder.CreateIndex(name: "IX_EvaluationTestCases_SourceRunId", table: "EvaluationTestCases", column: "SourceRunId");
        migrationBuilder.CreateIndex(name: "IX_EvaluationBatches_ScorecardId", table: "EvaluationBatches", column: "ScorecardId");
        migrationBuilder.CreateIndex(name: "IX_EvaluationBatches_StartedAt", table: "EvaluationBatches", column: "StartedAt");
        migrationBuilder.CreateIndex(name: "IX_EvaluationBatchItems_BatchId_TestCaseId", table: "EvaluationBatchItems", columns: new[] { "BatchId", "TestCaseId" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "EvaluationBatchItems");
        migrationBuilder.DropTable(name: "EvaluationMetricResults");
        migrationBuilder.DropTable(name: "EvaluationTestCases");
        migrationBuilder.DropTable(name: "EvaluationBatches");
        migrationBuilder.DropTable(name: "EvaluationRuns");
        migrationBuilder.DropTable(name: "EvaluationMetricDefinitions");
        migrationBuilder.DropIndex(name: "IX_EvaluationScorecards_Name_Version", table: "EvaluationScorecards");
        migrationBuilder.DropIndex(name: "IX_EvaluationScorecards_Status_IsDefault", table: "EvaluationScorecards");
        migrationBuilder.DropColumn(name: "Status", table: "EvaluationScorecards");
        migrationBuilder.DropColumn(name: "Version", table: "EvaluationScorecards");
        migrationBuilder.DropColumn(name: "QualityGateThreshold", table: "EvaluationScorecards");
        migrationBuilder.DropColumn(name: "IsDefault", table: "EvaluationScorecards");
        migrationBuilder.DropColumn(name: "Revision", table: "EvaluationScorecards");
        if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            migrationBuilder.AlterColumn<string>(name: "Name", table: "EvaluationScorecards", maxLength: 120, nullable: false, oldClrType: typeof(string), oldMaxLength: 200);
            migrationBuilder.AlterColumn<string>(name: "Description", table: "EvaluationScorecards", maxLength: 500, nullable: false, oldClrType: typeof(string), oldMaxLength: 2000);
        }
        migrationBuilder.CreateIndex(name: "IX_EvaluationScorecards_Name", table: "EvaluationScorecards", column: "Name", unique: true);
    }
}
