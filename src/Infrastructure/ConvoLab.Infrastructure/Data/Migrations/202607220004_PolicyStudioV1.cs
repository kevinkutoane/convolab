using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConvoLab.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202607220004_PolicyStudioV1")]
public partial class PolicyStudioV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PolicyDefinitions",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                PolicyKey = table.Column<Guid>(nullable: false),
                Version = table.Column<int>(nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
                Description = table.Column<string>(maxLength: 2000, nullable: false),
                Owner = table.Column<string>(maxLength: 200, nullable: false),
                Domain = table.Column<string>(maxLength: 80, nullable: false),
                Status = table.Column<string>(maxLength: 50, nullable: false),
                Scope = table.Column<string>(maxLength: 50, nullable: false),
                Environment = table.Column<string>(maxLength: 100, nullable: false),
                TenantId = table.Column<Guid>(nullable: true),
                DefaultEffect = table.Column<string>(maxLength: 50, nullable: false),
                Revision = table.Column<long>(nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(nullable: false),
                ActivatedAt = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_PolicyDefinitions", item => item.Id));

        migrationBuilder.CreateTable(
            name: "PolicyRules",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                PolicyId = table.Column<Guid>(nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
                Effect = table.Column<string>(maxLength: 50, nullable: false),
                Priority = table.Column<int>(nullable: false),
                MatchJson = table.Column<string>(nullable: false),
                ConstraintsJson = table.Column<string>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PolicyRules", item => item.Id);
                table.ForeignKey(
                    name: "FK_PolicyRules_PolicyDefinitions_PolicyId",
                    column: item => item.PolicyId,
                    principalTable: "PolicyDefinitions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PolicyDecisions",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                PolicyId = table.Column<Guid>(nullable: true),
                PolicyKey = table.Column<Guid>(nullable: true),
                PolicyVersion = table.Column<int>(nullable: true),
                PolicyName = table.Column<string>(maxLength: 200, nullable: false),
                Domain = table.Column<string>(maxLength: 80, nullable: false),
                Effect = table.Column<string>(maxLength: 50, nullable: false),
                Reason = table.Column<string>(maxLength: 2000, nullable: false),
                ContextJson = table.Column<string>(nullable: false),
                ConstraintsJson = table.Column<string>(nullable: false),
                Source = table.Column<string>(maxLength: 100, nullable: false),
                CorrelationId = table.Column<string>(maxLength: 100, nullable: false),
                SimulationId = table.Column<Guid>(nullable: true),
                RunId = table.Column<Guid>(nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PolicyDecisions", item => item.Id);
                table.ForeignKey(
                    name: "FK_PolicyDecisions_PolicyDefinitions_PolicyId",
                    column: item => item.PolicyId,
                    principalTable: "PolicyDefinitions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PolicyDefinitions_PolicyKey_Version",
            table: "PolicyDefinitions",
            columns: new[] { "PolicyKey", "Version" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_PolicyDefinitions_Domain_Status_Environment",
            table: "PolicyDefinitions",
            columns: new[] { "Domain", "Status", "Environment" });
        migrationBuilder.CreateIndex(name: "IX_PolicyDefinitions_TenantId", table: "PolicyDefinitions", column: "TenantId");
        migrationBuilder.CreateIndex(
            name: "IX_PolicyRules_PolicyId_Name",
            table: "PolicyRules",
            columns: new[] { "PolicyId", "Name" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_PolicyRules_PolicyId_Priority",
            table: "PolicyRules",
            columns: new[] { "PolicyId", "Priority" });
        migrationBuilder.CreateIndex(
            name: "IX_PolicyDecisions_Domain_Effect_CreatedAt",
            table: "PolicyDecisions",
            columns: new[] { "Domain", "Effect", "CreatedAt" });
        migrationBuilder.CreateIndex(name: "IX_PolicyDecisions_CorrelationId", table: "PolicyDecisions", column: "CorrelationId");
        migrationBuilder.CreateIndex(name: "IX_PolicyDecisions_SimulationId", table: "PolicyDecisions", column: "SimulationId");
        migrationBuilder.CreateIndex(name: "IX_PolicyDecisions_RunId", table: "PolicyDecisions", column: "RunId");
        migrationBuilder.CreateIndex(name: "IX_PolicyDecisions_PolicyId", table: "PolicyDecisions", column: "PolicyId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PolicyDecisions");
        migrationBuilder.DropTable(name: "PolicyRules");
        migrationBuilder.DropTable(name: "PolicyDefinitions");
    }
}
