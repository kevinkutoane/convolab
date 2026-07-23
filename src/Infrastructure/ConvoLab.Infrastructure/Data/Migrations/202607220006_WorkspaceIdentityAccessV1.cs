using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConvoLab.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202607220006_WorkspaceIdentityAccessV1")]
public partial class WorkspaceIdentityAccessV1 : Migration
{
    private static readonly Guid DefaultOrganisation = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid DefaultWorkspace = Guid.Parse("20000000-0000-0000-0000-000000000001");

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("Organisations", table => new
        {
            Id = table.Column<Guid>(nullable: false), Name = table.Column<string>(maxLength: 200, nullable: false),
            Slug = table.Column<string>(maxLength: 100, nullable: false), Status = table.Column<string>(maxLength: 30, nullable: false),
            Revision = table.Column<long>(nullable: false), CreatedAt = table.Column<DateTimeOffset>(nullable: false), UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK_Organisations", item => item.Id));
        migrationBuilder.CreateIndex("IX_Organisations_Slug", "Organisations", "Slug", unique: true);

        migrationBuilder.CreateTable("Workspaces", table => new
        {
            Id = table.Column<Guid>(nullable: false), OrganisationId = table.Column<Guid>(nullable: false),
            Name = table.Column<string>(maxLength: 200, nullable: false), Slug = table.Column<string>(maxLength: 100, nullable: false),
            Description = table.Column<string>(maxLength: 2000, nullable: false), Status = table.Column<string>(maxLength: 30, nullable: false),
            Revision = table.Column<long>(nullable: false), CreatedAt = table.Column<DateTimeOffset>(nullable: false), UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_Workspaces", item => item.Id);
            table.ForeignKey("FK_Workspaces_Organisations_OrganisationId", item => item.OrganisationId, "Organisations", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_Workspaces_OrganisationId_Slug", "Workspaces", new[] { "OrganisationId", "Slug" }, unique: true);

        if (ActiveProvider.Contains("Npgsql", StringComparison.Ordinal))
        {
            migrationBuilder.Sql("INSERT INTO \"Organisations\" (\"Id\", \"Name\", \"Slug\", \"Status\", \"Revision\", \"CreatedAt\", \"UpdatedAt\") VALUES ('10000000-0000-0000-0000-000000000001', 'ConvoLab', 'convolab', 'Active', 1, TIMESTAMPTZ '2026-07-22 00:00:00+00', TIMESTAMPTZ '2026-07-22 00:00:00+00');");
            migrationBuilder.Sql("INSERT INTO \"Workspaces\" (\"Id\", \"OrganisationId\", \"Name\", \"Slug\", \"Description\", \"Status\", \"Revision\", \"CreatedAt\", \"UpdatedAt\") VALUES ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'Default Workspace', 'default', 'The deterministic bootstrap workspace for upgraded resources.', 'Active', 1, TIMESTAMPTZ '2026-07-22 00:00:00+00', TIMESTAMPTZ '2026-07-22 00:00:00+00');");
        }
        else
        {
            migrationBuilder.Sql("INSERT INTO \"Organisations\" (\"Id\", \"Name\", \"Slug\", \"Status\", \"Revision\", \"CreatedAt\", \"UpdatedAt\") VALUES ('10000000-0000-0000-0000-000000000001', 'ConvoLab', 'convolab', 'Active', 1, '2026-07-22 00:00:00+00:00', '2026-07-22 00:00:00+00:00');");
            migrationBuilder.Sql("INSERT INTO \"Workspaces\" (\"Id\", \"OrganisationId\", \"Name\", \"Slug\", \"Description\", \"Status\", \"Revision\", \"CreatedAt\", \"UpdatedAt\") VALUES ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'Default Workspace', 'default', 'The deterministic bootstrap workspace for upgraded resources.', 'Active', 1, '2026-07-22 00:00:00+00:00', '2026-07-22 00:00:00+00:00');");
        }

        migrationBuilder.CreateTable("IdentityUsers", table => new
        {
            Id = table.Column<Guid>(nullable: false), Email = table.Column<string>(maxLength: 320, nullable: false), NormalizedEmail = table.Column<string>(maxLength: 320, nullable: false),
            DisplayName = table.Column<string>(maxLength: 200, nullable: false), Status = table.Column<string>(maxLength: 30, nullable: false), IsPlatformAdministrator = table.Column<bool>(nullable: false),
            Revision = table.Column<long>(nullable: false), CreatedAt = table.Column<DateTimeOffset>(nullable: false), UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK_IdentityUsers", item => item.Id));
        migrationBuilder.CreateIndex("IX_IdentityUsers_NormalizedEmail", "IdentityUsers", "NormalizedEmail", unique: true);

        migrationBuilder.CreateTable("WorkspaceMemberships", table => new
        {
            Id = table.Column<Guid>(nullable: false), WorkspaceId = table.Column<Guid>(nullable: false), UserId = table.Column<Guid>(nullable: false),
            Role = table.Column<string>(maxLength: 50, nullable: false), Status = table.Column<string>(maxLength: 30, nullable: false), InvitationTokenHash = table.Column<string>(maxLength: 128, nullable: true),
            InvitationExpiresAt = table.Column<DateTimeOffset>(nullable: true), Revision = table.Column<long>(nullable: false), CreatedAt = table.Column<DateTimeOffset>(nullable: false), UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_WorkspaceMemberships", item => item.Id);
            table.ForeignKey("FK_WorkspaceMemberships_Workspaces_WorkspaceId", item => item.WorkspaceId, "Workspaces", "Id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_WorkspaceMemberships_IdentityUsers_UserId", item => item.UserId, "IdentityUsers", "Id", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateIndex("IX_WorkspaceMemberships_WorkspaceId_UserId", "WorkspaceMemberships", new[] { "WorkspaceId", "UserId" }, unique: true);
        migrationBuilder.CreateIndex("IX_WorkspaceMemberships_UserId", "WorkspaceMemberships", "UserId");
        migrationBuilder.CreateIndex("IX_WorkspaceMemberships_InvitationTokenHash", "WorkspaceMemberships", "InvitationTokenHash", unique: true);

        migrationBuilder.CreateTable("LocalCredentials", table => new
        {
            UserId = table.Column<Guid>(nullable: false), PasswordHash = table.Column<string>(maxLength: 1000, nullable: false), FailedAttempts = table.Column<int>(nullable: false),
            LockedUntil = table.Column<DateTimeOffset>(nullable: true), UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
        }, constraints: table => { table.PrimaryKey("PK_LocalCredentials", item => item.UserId); table.ForeignKey("FK_LocalCredentials_IdentityUsers_UserId", item => item.UserId, "IdentityUsers", "Id", onDelete: ReferentialAction.Cascade); });

        migrationBuilder.CreateTable("AuthenticationSessions", table => new
        {
            Id = table.Column<Guid>(nullable: false), UserId = table.Column<Guid>(nullable: false), ActiveWorkspaceId = table.Column<Guid>(nullable: true), TokenHash = table.Column<string>(maxLength: 128, nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(nullable: false), LastSeenAt = table.Column<DateTimeOffset>(nullable: false), ExpiresAt = table.Column<DateTimeOffset>(nullable: false), RevokedAt = table.Column<DateTimeOffset>(nullable: true),
            ReplacedByTokenHash = table.Column<string>(maxLength: 128, nullable: true), IpAddress = table.Column<string>(maxLength: 64, nullable: true), UserAgent = table.Column<string>(maxLength: 500, nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_AuthenticationSessions", item => item.Id);
            table.ForeignKey("FK_AuthenticationSessions_IdentityUsers_UserId", item => item.UserId, "IdentityUsers", "Id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_AuthenticationSessions_Workspaces_ActiveWorkspaceId", item => item.ActiveWorkspaceId, "Workspaces", "Id", onDelete: ReferentialAction.SetNull);
        });
        migrationBuilder.CreateIndex("IX_AuthenticationSessions_TokenHash", "AuthenticationSessions", "TokenHash", unique: true);
        migrationBuilder.CreateIndex("IX_AuthenticationSessions_UserId_ExpiresAt", "AuthenticationSessions", new[] { "UserId", "ExpiresAt" });
        migrationBuilder.CreateIndex("IX_AuthenticationSessions_ActiveWorkspaceId", "AuthenticationSessions", "ActiveWorkspaceId");

        migrationBuilder.CreateTable("ServiceAccounts", table => new
        {
            Id = table.Column<Guid>(nullable: false), WorkspaceId = table.Column<Guid>(nullable: false), Name = table.Column<string>(maxLength: 200, nullable: false), SecretHash = table.Column<string>(maxLength: 128, nullable: false),
            ScopesJson = table.Column<string>(nullable: false), Status = table.Column<string>(maxLength: 30, nullable: false), ExpiresAt = table.Column<DateTimeOffset>(nullable: true), LastUsedAt = table.Column<DateTimeOffset>(nullable: true),
            Revision = table.Column<long>(nullable: false), CreatedAt = table.Column<DateTimeOffset>(nullable: false), UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
        }, constraints: table => { table.PrimaryKey("PK_ServiceAccounts", item => item.Id); table.ForeignKey("FK_ServiceAccounts_Workspaces_WorkspaceId", item => item.WorkspaceId, "Workspaces", "Id", onDelete: ReferentialAction.Cascade); });
        migrationBuilder.CreateIndex("IX_ServiceAccounts_WorkspaceId_Name", "ServiceAccounts", new[] { "WorkspaceId", "Name" }, unique: true);

        migrationBuilder.CreateTable("WorkspaceAuditEvents", table => new
        {
            Id = table.Column<Guid>(nullable: false), Scope = table.Column<string>(maxLength: 30, nullable: false), OrganisationId = table.Column<Guid>(nullable: true), WorkspaceId = table.Column<Guid>(nullable: true),
            ActorType = table.Column<string>(maxLength: 30, nullable: false), ActorId = table.Column<Guid>(nullable: true), ActorDisplay = table.Column<string>(maxLength: 320, nullable: false), Action = table.Column<string>(maxLength: 120, nullable: false),
            ResourceType = table.Column<string>(maxLength: 100, nullable: false), ResourceId = table.Column<string>(maxLength: 100, nullable: true), Outcome = table.Column<string>(maxLength: 30, nullable: false), DetailJson = table.Column<string>(nullable: false),
            CorrelationId = table.Column<string>(maxLength: 100, nullable: false), OccurredAt = table.Column<DateTimeOffset>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK_WorkspaceAuditEvents", item => item.Id));
        migrationBuilder.CreateIndex("IX_WorkspaceAuditEvents_WorkspaceId_OccurredAt", "WorkspaceAuditEvents", new[] { "WorkspaceId", "OccurredAt" });
        migrationBuilder.CreateIndex("IX_WorkspaceAuditEvents_Scope_OccurredAt", "WorkspaceAuditEvents", new[] { "Scope", "OccurredAt" });

        var supportsAlterForeignKeys = ActiveProvider.Contains("Npgsql", StringComparison.Ordinal);
        AddWorkspace(migrationBuilder, "KnowledgeCollections", supportsAlterForeignKeys); AddWorkspace(migrationBuilder, "Prompts", supportsAlterForeignKeys); AddWorkspace(migrationBuilder, "Workflows", supportsAlterForeignKeys); AddWorkspace(migrationBuilder, "ConversationSimulations", supportsAlterForeignKeys);
        AddWorkspace(migrationBuilder, "EvaluationScorecards", supportsAlterForeignKeys); AddWorkspace(migrationBuilder, "EvaluationRuns", supportsAlterForeignKeys); AddWorkspace(migrationBuilder, "EvaluationTestCases", supportsAlterForeignKeys); AddWorkspace(migrationBuilder, "EvaluationBatches", supportsAlterForeignKeys);
        AddWorkspace(migrationBuilder, "Traces", supportsAlterForeignKeys); AddWorkspace(migrationBuilder, "ReplayExperiments", supportsAlterForeignKeys); AddWorkspace(migrationBuilder, "PolicyDefinitions", supportsAlterForeignKeys); AddWorkspace(migrationBuilder, "PolicyDecisions", supportsAlterForeignKeys);

        migrationBuilder.AddColumn<string>("OwnershipScope", "Plugins", maxLength: 50, nullable: false, defaultValue: "Workspace");
        migrationBuilder.AddColumn<Guid>("WorkspaceId", "Plugins", nullable: true);
        migrationBuilder.Sql("UPDATE \"Plugins\" SET \"OwnershipScope\" = 'Platform', \"WorkspaceId\" = NULL WHERE \"ManifestUrl\" LIKE 'builtin://%' OR \"MetadataJson\" LIKE '%built-in%';");
        migrationBuilder.Sql($"UPDATE \"Plugins\" SET \"WorkspaceId\" = '{DefaultWorkspace}' WHERE \"OwnershipScope\" = 'Workspace';");
        migrationBuilder.CreateIndex("IX_Plugins_WorkspaceId_Key", "Plugins", new[] { "WorkspaceId", "Key" });
        if (supportsAlterForeignKeys) migrationBuilder.AddForeignKey("FK_Plugins_Workspaces_WorkspaceId", "Plugins", "WorkspaceId", "Workspaces", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

        migrationBuilder.DropIndex("IX_EvaluationScorecards_Name_Version", "EvaluationScorecards");
        migrationBuilder.CreateIndex("IX_EvaluationScorecards_WorkspaceId_Name_Version", "EvaluationScorecards", new[] { "WorkspaceId", "Name", "Version" }, unique: true);
    }

    private static void AddWorkspace(MigrationBuilder migrationBuilder, string table, bool addForeignKey)
    {
        migrationBuilder.AddColumn<Guid>("WorkspaceId", table, nullable: false, defaultValue: DefaultWorkspace);
        migrationBuilder.CreateIndex($"IX_{table}_WorkspaceId", table, "WorkspaceId");
        if (addForeignKey) migrationBuilder.AddForeignKey($"FK_{table}_Workspaces_WorkspaceId", table, "WorkspaceId", "Workspaces", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_EvaluationScorecards_WorkspaceId_Name_Version", "EvaluationScorecards");
        migrationBuilder.CreateIndex("IX_EvaluationScorecards_Name_Version", "EvaluationScorecards", new[] { "Name", "Version" }, unique: true);
        var supportsAlterForeignKeys = ActiveProvider.Contains("Npgsql", StringComparison.Ordinal);
        if (supportsAlterForeignKeys) migrationBuilder.DropForeignKey("FK_Plugins_Workspaces_WorkspaceId", "Plugins");
        migrationBuilder.DropIndex("IX_Plugins_WorkspaceId_Key", "Plugins"); migrationBuilder.DropColumn("WorkspaceId", "Plugins"); migrationBuilder.DropColumn("OwnershipScope", "Plugins");
        foreach (var table in new[] { "KnowledgeCollections", "Prompts", "Workflows", "ConversationSimulations", "EvaluationScorecards", "EvaluationRuns", "EvaluationTestCases", "EvaluationBatches", "Traces", "ReplayExperiments", "PolicyDefinitions", "PolicyDecisions" })
        {
            if (supportsAlterForeignKeys) migrationBuilder.DropForeignKey($"FK_{table}_Workspaces_WorkspaceId", table);
            migrationBuilder.DropIndex($"IX_{table}_WorkspaceId", table); migrationBuilder.DropColumn("WorkspaceId", table);
        }
        migrationBuilder.DropTable("AuthenticationSessions"); migrationBuilder.DropTable("LocalCredentials"); migrationBuilder.DropTable("ServiceAccounts"); migrationBuilder.DropTable("WorkspaceAuditEvents"); migrationBuilder.DropTable("WorkspaceMemberships"); migrationBuilder.DropTable("IdentityUsers"); migrationBuilder.DropTable("Workspaces"); migrationBuilder.DropTable("Organisations");
    }
}
