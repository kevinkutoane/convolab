using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConvoLab.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202607180002_WorkflowStudioV1")]
public partial class WorkflowStudioV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Workflows",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
                Description = table.Column<string>(nullable: false),
                Owner = table.Column<string>(maxLength: 200, nullable: false),
                TagsJson = table.Column<string>(nullable: false),
                IsActive = table.Column<bool>(nullable: false),
                Revision = table.Column<long>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Workflows", item => item.Id));

        migrationBuilder.CreateTable(
            name: "WorkflowVersions",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                WorkflowId = table.Column<Guid>(nullable: false),
                Major = table.Column<int>(nullable: false),
                Minor = table.Column<int>(nullable: false),
                Patch = table.Column<int>(nullable: false),
                Status = table.Column<string>(maxLength: 50, nullable: false),
                ChangeSummary = table.Column<string>(maxLength: 1000, nullable: false),
                ApprovedBy = table.Column<string>(nullable: true),
                ApprovedAt = table.Column<DateTimeOffset>(nullable: true),
                PublishedAt = table.Column<DateTimeOffset>(nullable: true),
                Revision = table.Column<long>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkflowVersions", item => item.Id);
                table.ForeignKey(
                    name: "FK_WorkflowVersions_Workflows_WorkflowId",
                    column: item => item.WorkflowId,
                    principalTable: "Workflows",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "WorkflowNodes",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                WorkflowVersionId = table.Column<Guid>(nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
                Kind = table.Column<string>(maxLength: 50, nullable: false),
                PositionX = table.Column<double>(nullable: false),
                PositionY = table.Column<double>(nullable: false),
                ConfigurationJson = table.Column<string>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkflowNodes", item => item.Id);
                table.ForeignKey(
                    name: "FK_WorkflowNodes_WorkflowVersions_WorkflowVersionId",
                    column: item => item.WorkflowVersionId,
                    principalTable: "WorkflowVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "WorkflowTransitions",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                WorkflowVersionId = table.Column<Guid>(nullable: false),
                FromNodeId = table.Column<Guid>(nullable: false),
                ToNodeId = table.Column<Guid>(nullable: false),
                Label = table.Column<string>(maxLength: 200, nullable: false),
                Condition = table.Column<string>(maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkflowTransitions", item => item.Id);
                table.ForeignKey(
                    name: "FK_WorkflowTransitions_WorkflowVersions_WorkflowVersionId",
                    column: item => item.WorkflowVersionId,
                    principalTable: "WorkflowVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "WorkflowAudit",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                WorkflowVersionId = table.Column<Guid>(nullable: false),
                Actor = table.Column<string>(maxLength: 200, nullable: false),
                Action = table.Column<string>(maxLength: 80, nullable: false),
                Reason = table.Column<string>(nullable: true),
                PreviousStatus = table.Column<string>(nullable: false),
                NewStatus = table.Column<string>(nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkflowAudit", item => item.Id);
                table.ForeignKey(
                    name: "FK_WorkflowAudit_WorkflowVersions_WorkflowVersionId",
                    column: item => item.WorkflowVersionId,
                    principalTable: "WorkflowVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_Workflows_Name", table: "Workflows", column: "Name");
        migrationBuilder.CreateIndex(name: "IX_Workflows_IsActive_UpdatedAt", table: "Workflows", columns: new[] { "IsActive", "UpdatedAt" });
        migrationBuilder.CreateIndex(name: "IX_WorkflowVersions_WorkflowId_Major_Minor_Patch", table: "WorkflowVersions", columns: new[] { "WorkflowId", "Major", "Minor", "Patch" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_WorkflowVersions_Status_UpdatedAt", table: "WorkflowVersions", columns: new[] { "Status", "UpdatedAt" });
        migrationBuilder.CreateIndex(name: "IX_WorkflowNodes_WorkflowVersionId_Kind", table: "WorkflowNodes", columns: new[] { "WorkflowVersionId", "Kind" });
        migrationBuilder.CreateIndex(name: "IX_WorkflowTransitions_WorkflowVersionId_FromNodeId", table: "WorkflowTransitions", columns: new[] { "WorkflowVersionId", "FromNodeId" });
        migrationBuilder.CreateIndex(name: "IX_WorkflowTransitions_WorkflowVersionId_ToNodeId", table: "WorkflowTransitions", columns: new[] { "WorkflowVersionId", "ToNodeId" });
        migrationBuilder.CreateIndex(name: "IX_WorkflowAudit_WorkflowVersionId_CreatedAt", table: "WorkflowAudit", columns: new[] { "WorkflowVersionId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "WorkflowAudit");
        migrationBuilder.DropTable(name: "WorkflowTransitions");
        migrationBuilder.DropTable(name: "WorkflowNodes");
        migrationBuilder.DropTable(name: "WorkflowVersions");
        migrationBuilder.DropTable(name: "Workflows");
    }
}
