using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConvoLab.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202607180001_PlatformHardeningSprint1")]
public partial class PlatformHardeningSprint1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "Revision",
            table: "Prompts",
            nullable: false,
            defaultValue: 1L);
        migrationBuilder.AddColumn<long>(
            name: "Revision",
            table: "PromptVersions",
            nullable: false,
            defaultValue: 1L);
        migrationBuilder.AddColumn<long>(
            name: "Revision",
            table: "KnowledgeCollections",
            nullable: false,
            defaultValue: 1L);
        migrationBuilder.AddColumn<long>(
            name: "Revision",
            table: "KnowledgeDocuments",
            nullable: false,
            defaultValue: 1L);

        // Early Docker builds created this table before EF migrations were introduced.
        // Create it idempotently so those installations retain their saved simulations.
        if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "ConversationSimulations" (
                    "Id" uuid NOT NULL,
                    "Payload" text NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_ConversationSimulations" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_ConversationSimulations_UpdatedAt"
                    ON "ConversationSimulations" ("UpdatedAt");
                """);
        }
        else if (ActiveProvider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "ConversationSimulations" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_ConversationSimulations" PRIMARY KEY,
                    "Payload" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_ConversationSimulations_UpdatedAt"
                    ON "ConversationSimulations" ("UpdatedAt");
                """);
        }
        else
        {
            migrationBuilder.CreateTable(
                name: "ConversationSimulations",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    Payload = table.Column<string>(nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_ConversationSimulations", item => item.Id));

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSimulations_UpdatedAt",
                table: "ConversationSimulations",
                column: "UpdatedAt");
        }

        migrationBuilder.CreateIndex(
            name: "IX_Prompts_Status_UpdatedAt",
            table: "Prompts",
            columns: new[] { "Status", "UpdatedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_PromptVersions_Status_UpdatedAt",
            table: "PromptVersions",
            columns: new[] { "Status", "UpdatedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_PromptLifecycle_PromptVersionId_CreatedAt",
            table: "PromptLifecycle",
            columns: new[] { "PromptVersionId", "CreatedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_KnowledgeCollections_Status_UpdatedAt",
            table: "KnowledgeCollections",
            columns: new[] { "Status", "UpdatedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_KnowledgeDocuments_Status_UpdatedAt",
            table: "KnowledgeDocuments",
            columns: new[] { "Status", "UpdatedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_KnowledgeChunks_DocumentId_Sequence",
            table: "KnowledgeChunks",
            columns: new[] { "DocumentId", "Sequence" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_KnowledgeLifecycle_DocumentId_At",
            table: "KnowledgeLifecycle",
            columns: new[] { "DocumentId", "At" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ConversationSimulations");
        migrationBuilder.DropIndex(name: "IX_Prompts_Status_UpdatedAt", table: "Prompts");
        migrationBuilder.DropIndex(name: "IX_PromptVersions_Status_UpdatedAt", table: "PromptVersions");
        migrationBuilder.DropIndex(name: "IX_PromptLifecycle_PromptVersionId_CreatedAt", table: "PromptLifecycle");
        migrationBuilder.DropIndex(name: "IX_KnowledgeCollections_Status_UpdatedAt", table: "KnowledgeCollections");
        migrationBuilder.DropIndex(name: "IX_KnowledgeDocuments_Status_UpdatedAt", table: "KnowledgeDocuments");
        migrationBuilder.DropIndex(name: "IX_KnowledgeChunks_DocumentId_Sequence", table: "KnowledgeChunks");
        migrationBuilder.DropIndex(name: "IX_KnowledgeLifecycle_DocumentId_At", table: "KnowledgeLifecycle");

        migrationBuilder.DropColumn(name: "Revision", table: "Prompts");
        migrationBuilder.DropColumn(name: "Revision", table: "PromptVersions");
        migrationBuilder.DropColumn(name: "Revision", table: "KnowledgeCollections");
        migrationBuilder.DropColumn(name: "Revision", table: "KnowledgeDocuments");
    }
}
