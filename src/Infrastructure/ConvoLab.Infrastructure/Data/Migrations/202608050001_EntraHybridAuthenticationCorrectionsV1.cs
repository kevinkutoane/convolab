using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ConvoLab.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608050001_EntraHybridAuthenticationCorrectionsV1")]
public sealed class EntraHybridAuthenticationCorrectionsV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("BreakGlassFailedAttempts", "LocalCredentials", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<DateTimeOffset>("BreakGlassLockedUntil", "LocalCredentials", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("BreakGlassLastFailedAt", "LocalCredentials", nullable: true);
        migrationBuilder.AddColumn<long>("BreakGlassRevision", "LocalCredentials", nullable: false, defaultValue: 1L);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("BreakGlassFailedAttempts", "LocalCredentials");
        migrationBuilder.DropColumn("BreakGlassLockedUntil", "LocalCredentials");
        migrationBuilder.DropColumn("BreakGlassLastFailedAt", "LocalCredentials");
        migrationBuilder.DropColumn("BreakGlassRevision", "LocalCredentials");
    }
}
