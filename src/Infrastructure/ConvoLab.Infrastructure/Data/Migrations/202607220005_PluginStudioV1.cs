using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConvoLab.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202607220005_PluginStudioV1")]
public partial class PluginStudioV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Plugins",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                PluginKey = table.Column<Guid>(nullable: false),
                Key = table.Column<string>(maxLength: 160, nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
                Description = table.Column<string>(maxLength: 2000, nullable: false),
                Publisher = table.Column<string>(maxLength: 200, nullable: false),
                Version = table.Column<string>(maxLength: 80, nullable: false),
                Category = table.Column<string>(maxLength: 80, nullable: false),
                Status = table.Column<string>(maxLength: 50, nullable: false),
                HealthStatus = table.Column<string>(maxLength: 50, nullable: false),
                HealthMessage = table.Column<string>(maxLength: 2000, nullable: false),
                ManifestUrl = table.Column<string>(maxLength: 1000, nullable: false),
                EntryPoint = table.Column<string>(maxLength: 500, nullable: false),
                PlatformApiVersion = table.Column<string>(maxLength: 50, nullable: false),
                CapabilitiesJson = table.Column<string>(nullable: false),
                PermissionsJson = table.Column<string>(nullable: false),
                ConfigurationSchema = table.Column<string>(nullable: false),
                MetadataJson = table.Column<string>(nullable: false),
                LastHealthCheckAt = table.Column<DateTimeOffset>(nullable: true),
                Revision = table.Column<long>(nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Plugins", item => item.Id));

        migrationBuilder.CreateTable(
            name: "PluginHealthChecks",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                PluginId = table.Column<Guid>(nullable: false),
                Status = table.Column<string>(maxLength: 50, nullable: false),
                Message = table.Column<string>(maxLength: 2000, nullable: false),
                DurationMs = table.Column<int>(nullable: false),
                Source = table.Column<string>(maxLength: 80, nullable: false),
                CheckedAt = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PluginHealthChecks", item => item.Id);
                table.ForeignKey(
                    name: "FK_PluginHealthChecks_Plugins_PluginId",
                    column: item => item.PluginId,
                    principalTable: "Plugins",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Plugins_PluginKey_Version",
            table: "Plugins",
            columns: new[] { "PluginKey", "Version" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_Plugins_PluginKey_Active",
            table: "Plugins",
            column: "PluginKey",
            unique: true,
            filter: "\"Status\" = 'Active'");
        migrationBuilder.CreateIndex(
            name: "IX_Plugins_Key_Status",
            table: "Plugins",
            columns: new[] { "Key", "Status" });
        migrationBuilder.CreateIndex(
            name: "IX_Plugins_Category_HealthStatus",
            table: "Plugins",
            columns: new[] { "Category", "HealthStatus" });
        migrationBuilder.CreateIndex(
            name: "IX_PluginHealthChecks_PluginId_CheckedAt",
            table: "PluginHealthChecks",
            columns: new[] { "PluginId", "CheckedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_PluginHealthChecks_Status_CheckedAt",
            table: "PluginHealthChecks",
            columns: new[] { "Status", "CheckedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PluginHealthChecks");
        migrationBuilder.DropTable(name: "Plugins");
    }
}

