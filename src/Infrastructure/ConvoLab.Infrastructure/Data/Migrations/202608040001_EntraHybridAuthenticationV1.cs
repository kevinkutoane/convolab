using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConvoLab.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608040001_EntraHybridAuthenticationV1")]
public sealed class EntraHybridAuthenticationV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>("AbsoluteExpiresAt", "AuthenticationSessions", nullable: false,
            defaultValue: new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.Zero));
        migrationBuilder.AddColumn<string>("AuthenticationProvider", "AuthenticationSessions", maxLength: 30,
            nullable: false, defaultValue: "Local");
        migrationBuilder.AddColumn<Guid>("ExternalIdentityId", "AuthenticationSessions", nullable: true);
        migrationBuilder.AddColumn<string>("RevocationReason", "AuthenticationSessions", maxLength: 160, nullable: true);
        migrationBuilder.AddColumn<Guid>("RevokedBy", "AuthenticationSessions", nullable: true);
        migrationBuilder.AddColumn<Guid>("SessionFamilyId", "AuthenticationSessions", nullable: false, defaultValue: Guid.Empty);

        migrationBuilder.CreateTable(
            "ExternalIdentities",
            table => new
            {
                Id = table.Column<Guid>(nullable: false),
                UserId = table.Column<Guid>(nullable: false),
                Provider = table.Column<string>(maxLength: 40, nullable: false),
                Issuer = table.Column<string>(maxLength: 500, nullable: false),
                Subject = table.Column<string>(maxLength: 255, nullable: false),
                TenantId = table.Column<string>(maxLength: 80, nullable: false),
                EmailAtLastLogin = table.Column<string>(maxLength: 320, nullable: true),
                DisplayNameAtLastLogin = table.Column<string>(maxLength: 200, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                LastLoginAt = table.Column<DateTimeOffset>(nullable: false),
                IsActive = table.Column<bool>(nullable: false),
                DisabledAt = table.Column<DateTimeOffset>(nullable: true),
                DisabledBy = table.Column<Guid>(nullable: true),
                Revision = table.Column<long>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExternalIdentities", item => item.Id);
                table.ForeignKey("FK_ExternalIdentities_IdentityUsers_UserId", item => item.UserId,
                    "IdentityUsers", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            "ExternalIdentityInvitations",
            table => new
            {
                Id = table.Column<Guid>(nullable: false),
                UserId = table.Column<Guid>(nullable: false),
                InvitedEmail = table.Column<string>(maxLength: 320, nullable: false),
                NormalizedEmail = table.Column<string>(maxLength: 320, nullable: false),
                ExpectedTenant = table.Column<string>(maxLength: 80, nullable: false),
                ExpectedProvider = table.Column<string>(maxLength: 40, nullable: false),
                TokenHash = table.Column<string>(maxLength: 128, nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(nullable: false),
                CreatedBy = table.Column<Guid>(nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                ConsumedAt = table.Column<DateTimeOffset>(nullable: true),
                ConsumedByExternalIdentityId = table.Column<Guid>(nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(nullable: true),
                Status = table.Column<string>(maxLength: 30, nullable: false),
                Revision = table.Column<long>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExternalIdentityInvitations", item => item.Id);
                table.ForeignKey("FK_ExternalIdentityInvitations_IdentityUsers_UserId", item => item.UserId,
                    "IdentityUsers", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ExternalIdentityInvitations_ExternalIdentities_ConsumedByExternalIdentityId",
                    item => item.ConsumedByExternalIdentityId, "ExternalIdentities", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_ExternalIdentities_Provider_Issuer_Subject", "ExternalIdentities",
            new[] { "Provider", "Issuer", "Subject" }, unique: true);
        migrationBuilder.CreateIndex("IX_ExternalIdentities_UserId_Provider", "ExternalIdentities",
            new[] { "UserId", "Provider" });
        migrationBuilder.CreateIndex("IX_ExternalIdentityInvitations_TokenHash", "ExternalIdentityInvitations", "TokenHash", unique: true);
        migrationBuilder.CreateIndex("IX_ExternalIdentityInvitations_UserId_Status_ExpiresAt", "ExternalIdentityInvitations",
            new[] { "UserId", "Status", "ExpiresAt" });
        migrationBuilder.CreateIndex("IX_ExternalIdentityInvitations_ConsumedByExternalIdentityId", "ExternalIdentityInvitations",
            "ConsumedByExternalIdentityId");
        migrationBuilder.CreateIndex("IX_AuthenticationSessions_ExternalIdentityId_ExpiresAt", "AuthenticationSessions",
            new[] { "ExternalIdentityId", "ExpiresAt" });
        if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            migrationBuilder.AddForeignKey("FK_AuthenticationSessions_ExternalIdentities_ExternalIdentityId",
                "AuthenticationSessions", "ExternalIdentityId", "ExternalIdentities", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

        migrationBuilder.Sql("""
            UPDATE "AuthenticationSessions"
            SET "SessionFamilyId" = "Id", "AbsoluteExpiresAt" = "ExpiresAt"
            WHERE "SessionFamilyId" = '00000000-0000-0000-0000-000000000000';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            migrationBuilder.DropForeignKey("FK_AuthenticationSessions_ExternalIdentities_ExternalIdentityId", "AuthenticationSessions");
        migrationBuilder.DropIndex("IX_AuthenticationSessions_ExternalIdentityId_ExpiresAt", "AuthenticationSessions");
        migrationBuilder.DropTable("ExternalIdentityInvitations");
        migrationBuilder.DropTable("ExternalIdentities");
        migrationBuilder.DropColumn("AbsoluteExpiresAt", "AuthenticationSessions");
        migrationBuilder.DropColumn("AuthenticationProvider", "AuthenticationSessions");
        migrationBuilder.DropColumn("ExternalIdentityId", "AuthenticationSessions");
        migrationBuilder.DropColumn("RevocationReason", "AuthenticationSessions");
        migrationBuilder.DropColumn("RevokedBy", "AuthenticationSessions");
        migrationBuilder.DropColumn("SessionFamilyId", "AuthenticationSessions");
    }
}
