using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConvoLab.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202607220003_ReplayStudioV1")]
public partial class ReplayStudioV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ReplayExperiments",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
                SimulationId = table.Column<Guid>(nullable: false),
                SourceRunId = table.Column<Guid>(nullable: false),
                Status = table.Column<string>(maxLength: 50, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ReplayExperiments", item => item.Id));

        migrationBuilder.CreateTable(
            name: "ReplayCandidates",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                ExperimentId = table.Column<Guid>(nullable: false),
                RunId = table.Column<Guid>(nullable: false),
                Label = table.Column<string>(maxLength: 200, nullable: false),
                Workflow = table.Column<string>(maxLength: 300, nullable: false),
                PromptVersion = table.Column<string>(maxLength: 200, nullable: false),
                KnowledgeCollection = table.Column<string>(maxLength: 200, nullable: false),
                Provider = table.Column<string>(maxLength: 160, nullable: false),
                Model = table.Column<string>(maxLength: 200, nullable: false),
                Temperature = table.Column<double>(nullable: false),
                MaxOutputTokens = table.Column<int>(nullable: false),
                Mode = table.Column<string>(maxLength: 50, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReplayCandidates", item => item.Id);
                table.ForeignKey(
                    name: "FK_ReplayCandidates_ReplayExperiments_ExperimentId",
                    column: item => item.ExperimentId,
                    principalTable: "ReplayExperiments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ReplayExperiments_SimulationId_SourceRunId",
            table: "ReplayExperiments",
            columns: new[] { "SimulationId", "SourceRunId" });
        migrationBuilder.CreateIndex(
            name: "IX_ReplayExperiments_Status_UpdatedAt",
            table: "ReplayExperiments",
            columns: new[] { "Status", "UpdatedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_ReplayCandidates_RunId",
            table: "ReplayCandidates",
            column: "RunId",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ReplayCandidates_ExperimentId_CreatedAt",
            table: "ReplayCandidates",
            columns: new[] { "ExperimentId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ReplayCandidates");
        migrationBuilder.DropTable(name: "ReplayExperiments");
    }
}
