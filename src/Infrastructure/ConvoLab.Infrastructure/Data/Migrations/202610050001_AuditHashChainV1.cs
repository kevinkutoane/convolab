using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ConvoLab.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202610050001_AuditHashChainV1")]
public sealed class AuditHashChainV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Nullable: events written before this migration stay valid and are reported as unsealed.
        migrationBuilder.AddColumn<string>("ChainKey", "WorkspaceAuditEvents", maxLength: 40, nullable: true);
        migrationBuilder.AddColumn<long>("Sequence", "WorkspaceAuditEvents", nullable: true);
        migrationBuilder.AddColumn<string>("PreviousHash", "WorkspaceAuditEvents", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<string>("Hash", "WorkspaceAuditEvents", maxLength: 64, nullable: true);
        migrationBuilder.CreateIndex("IX_WorkspaceAuditEvents_ChainKey_Sequence", "WorkspaceAuditEvents",
            new[] { "ChainKey", "Sequence" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_WorkspaceAuditEvents_ChainKey_Sequence", "WorkspaceAuditEvents");
        migrationBuilder.DropColumn("ChainKey", "WorkspaceAuditEvents");
        migrationBuilder.DropColumn("Sequence", "WorkspaceAuditEvents");
        migrationBuilder.DropColumn("PreviousHash", "WorkspaceAuditEvents");
        migrationBuilder.DropColumn("Hash", "WorkspaceAuditEvents");
    }
}
