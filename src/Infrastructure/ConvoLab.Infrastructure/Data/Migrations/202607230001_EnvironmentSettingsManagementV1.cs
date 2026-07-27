using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConvoLab.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202607230001_EnvironmentSettingsManagementV1")]
public partial class EnvironmentSettingsManagementV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ─── RuntimeEnvironments ──────────────────────────────────────────
        migrationBuilder.CreateTable("RuntimeEnvironments", table => new
        {
            Id = table.Column<Guid>(nullable: false),
            OrganisationId = table.Column<Guid>(nullable: false),
            WorkspaceId = table.Column<Guid>(nullable: false),
            Name = table.Column<string>(maxLength: 200, nullable: false),
            Slug = table.Column<string>(maxLength: 100, nullable: false),
            EnvironmentType = table.Column<string>(maxLength: 30, nullable: false),
            Description = table.Column<string>(maxLength: 2000, nullable: false),
            Status = table.Column<string>(maxLength: 30, nullable: false),
            IsDefault = table.Column<bool>(nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(nullable: false),
            CreatedBy = table.Column<Guid>(nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(nullable: false),
            UpdatedBy = table.Column<Guid>(nullable: true),
            Revision = table.Column<long>(nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_RuntimeEnvironments", item => item.Id);
            table.ForeignKey("FK_RuntimeEnvironments_Workspaces_WorkspaceId", item => item.WorkspaceId, "Workspaces", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_RuntimeEnvironments_WorkspaceId_Slug", "RuntimeEnvironments", new[] { "WorkspaceId", "Slug" }, unique: true);
        migrationBuilder.CreateIndex("IX_RuntimeEnvironments_WorkspaceId_IsDefault", "RuntimeEnvironments", new[] { "WorkspaceId", "IsDefault" });
        migrationBuilder.CreateIndex("IX_RuntimeEnvironments_OrganisationId", "RuntimeEnvironments", "OrganisationId");

        // ─── SettingDefinitions ───────────────────────────────────────────
        migrationBuilder.CreateTable("SettingDefinitions", table => new
        {
            Key = table.Column<string>(maxLength: 120, nullable: false),
            Category = table.Column<string>(maxLength: 80, nullable: false),
            DisplayName = table.Column<string>(maxLength: 200, nullable: false),
            Description = table.Column<string>(maxLength: 1000, nullable: false),
            ValueType = table.Column<string>(maxLength: 30, nullable: false),
            DefaultValue = table.Column<string>(maxLength: 2000, nullable: true),
            IsSecret = table.Column<bool>(nullable: false),
            IsRequired = table.Column<bool>(nullable: false),
            AllowsOrganisationOverride = table.Column<bool>(nullable: false),
            AllowsWorkspaceOverride = table.Column<bool>(nullable: false),
            AllowsEnvironmentOverride = table.Column<bool>(nullable: false),
            ValidationRules = table.Column<string>(maxLength: 1000, nullable: true),
            RequiresRestart = table.Column<bool>(nullable: false),
            AllowedValues = table.Column<string>(maxLength: 2000, nullable: true),
            UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK_SettingDefinitions", item => item.Key));
        migrationBuilder.CreateIndex("IX_SettingDefinitions_Category", "SettingDefinitions", "Category");

        // ─── SettingValues ────────────────────────────────────────────────
        migrationBuilder.CreateTable("SettingValues", table => new
        {
            Id = table.Column<Guid>(nullable: false),
            DefinitionKey = table.Column<string>(maxLength: 120, nullable: false),
            Scope = table.Column<string>(maxLength: 30, nullable: false),
            OrganisationId = table.Column<Guid>(nullable: true),
            WorkspaceId = table.Column<Guid>(nullable: true),
            EnvironmentId = table.Column<Guid>(nullable: true),
            ValueJson = table.Column<string>(nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(nullable: false),
            CreatedBy = table.Column<Guid>(nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(nullable: false),
            UpdatedBy = table.Column<Guid>(nullable: false),
            Revision = table.Column<long>(nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_SettingValues", item => item.Id);
            table.ForeignKey("FK_SettingValues_SettingDefinitions_DefinitionKey", item => item.DefinitionKey, "SettingDefinitions", "Key", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateIndex("IX_SettingValues_Scope", "SettingValues",
            new[] { "DefinitionKey", "Scope", "OrganisationId", "WorkspaceId", "EnvironmentId" }, unique: true);
        migrationBuilder.CreateIndex("IX_SettingValues_WorkspaceId_Scope", "SettingValues", new[] { "WorkspaceId", "Scope" });
        migrationBuilder.CreateIndex("IX_SettingValues_EnvironmentId_Scope", "SettingValues", new[] { "EnvironmentId", "Scope" });

        // ─── SecretReferences ─────────────────────────────────────────────
        migrationBuilder.CreateTable("SecretReferences", table => new
        {
            Id = table.Column<Guid>(nullable: false),
            WorkspaceId = table.Column<Guid>(nullable: false),
            DisplayName = table.Column<string>(maxLength: 200, nullable: false),
            Reference = table.Column<string>(maxLength: 500, nullable: false),
            Provider = table.Column<string>(maxLength: 80, nullable: false),
            Status = table.Column<string>(maxLength: 30, nullable: false),
            LastValidatedAt = table.Column<DateTimeOffset>(nullable: true),
            LastValidationOutcome = table.Column<string>(maxLength: 500, nullable: true),
            IsDisabled = table.Column<bool>(nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(nullable: false),
            CreatedBy = table.Column<Guid>(nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>(nullable: false),
            UpdatedBy = table.Column<Guid>(nullable: true),
            Revision = table.Column<long>(nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_SecretReferences", item => item.Id);
            table.ForeignKey("FK_SecretReferences_Workspaces_WorkspaceId", item => item.WorkspaceId, "Workspaces", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_SecretReferences_WorkspaceId", "SecretReferences", "WorkspaceId");
        migrationBuilder.CreateIndex("IX_SecretReferences_WorkspaceId_Reference", "SecretReferences", new[] { "WorkspaceId", "Reference" });

        // ─── ConfigurationChanges (append-only) ───────────────────────────
        migrationBuilder.CreateTable("ConfigurationChanges", table => new
        {
            Id = table.Column<Guid>(nullable: false),
            OrganisationId = table.Column<Guid>(nullable: false),
            WorkspaceId = table.Column<Guid>(nullable: true),
            EnvironmentId = table.Column<Guid>(nullable: true),
            SettingKey = table.Column<string>(maxLength: 120, nullable: false),
            PreviousValueSummary = table.Column<string>(maxLength: 500, nullable: true),
            NewValueSummary = table.Column<string>(maxLength: 500, nullable: false),
            ChangedBy = table.Column<Guid>(nullable: false),
            ChangedByDisplay = table.Column<string>(maxLength: 320, nullable: false),
            ChangedAt = table.Column<DateTimeOffset>(nullable: false),
            Reason = table.Column<string>(maxLength: 1000, nullable: true),
            CorrelationId = table.Column<string>(maxLength: 100, nullable: false),
            Outcome = table.Column<string>(maxLength: 30, nullable: false),
            Revision = table.Column<long>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK_ConfigurationChanges", item => item.Id));
        migrationBuilder.CreateIndex("IX_ConfigurationChanges_WorkspaceId_ChangedAt", "ConfigurationChanges", new[] { "WorkspaceId", "ChangedAt" });
        migrationBuilder.CreateIndex("IX_ConfigurationChanges_EnvironmentId_ChangedAt", "ConfigurationChanges", new[] { "EnvironmentId", "ChangedAt" });
        migrationBuilder.CreateIndex("IX_ConfigurationChanges_OrganisationId_ChangedAt", "ConfigurationChanges", new[] { "OrganisationId", "ChangedAt" });

        // ─── Backfill: seed SettingDefinitions ────────────────────────────
        SeedSettingDefinitions(migrationBuilder);

        // ─── Backfill: create Development environment per workspace ───────
        BackfillEnvironments(migrationBuilder);
    }

    private static void SeedSettingDefinitions(MigrationBuilder m)
    {
        var now = "2026-07-23 00:00:00+00";
        if (!ActiveProvider.Contains("Npgsql", StringComparison.Ordinal))
            now = "2026-07-23 00:00:00+00:00";

        var defs = new[]
        {
            // General
            ("general.locale","General","Default Locale","Default locale for the workspace.","String","\"en-ZA\"",false,false,true,true,true,null,false,null),
            ("general.timezone","General","Default Timezone","Default timezone for the workspace.","String","\"Africa/Johannesburg\"",false,false,true,true,true,null,false,null),
            ("general.currency","General","Default Currency","Default currency for the workspace.","String","\"ZAR\"",false,false,true,true,true,null,false,null),
            // AI Provider
            ("ai.provider","AI Provider","Provider","Active AI provider.","String","\"Gemini\"",false,false,true,true,true,null,false,null),
            ("ai.model","AI Provider","Model","Active AI model.","String","\"gemini-2.5-flash\"",false,false,true,true,true,null,false,null),
            ("ai.secret_reference","AI Provider","API Key Reference","Reference to the provider API key secret.","SecretReference",null,true,false,true,true,true,null,false,null),
            ("ai.request_timeout_seconds","AI Provider","Request Timeout (s)","Provider request timeout in seconds.","Integer","\"30\"",false,false,true,true,true,"{\"min\":5,\"max\":300}",false,null),
            ("ai.max_retry_count","AI Provider","Max Retry Count","Maximum retry attempts for provider calls.","Integer","\"3\"",false,false,true,true,true,"{\"min\":0,\"max\":10}",false,null),
            ("ai.temperature","AI Provider","Temperature","Model temperature (0.0–2.0).","Decimal","\"0.7\"",false,false,true,true,true,"{\"min\":0,\"max\":2}",false,null),
            ("ai.max_output_tokens","AI Provider","Max Output Tokens","Maximum output tokens per request.","Integer","\"8192\"",false,false,true,true,true,"{\"min\":1,\"max\":65536}",false,null),
            ("ai.provider_enabled","AI Provider","Provider Enabled","Whether AI provider execution is enabled.","Boolean","\"true\"",false,false,true,true,true,null,false,null),
            // Budget
            ("budget.monthly_zar","Budget","Monthly Budget (ZAR)","Monthly AI spend limit in ZAR.","Currency","\"500\"",false,false,true,true,true,"{\"min\":0}",false,null),
            ("budget.warning_threshold","Budget","Warning Threshold","Budget utilisation warning level (0.0–1.0).","Percentage","\"0.8\"",false,false,true,true,true,"{\"min\":0,\"max\":1}",false,null),
            ("budget.hard_stop_threshold","Budget","Hard-Stop Threshold","Budget utilisation hard-stop level (0.0–1.0).","Percentage","\"1.0\"",false,false,true,true,true,"{\"min\":0,\"max\":1}",false,null),
            ("budget.input_price_zar_per_1k","Budget","Input Price (ZAR/1K tokens)","Provider input token price in ZAR per 1,000 tokens.","Decimal",null,false,false,true,true,true,"{\"min\":0}",false,null),
            ("budget.output_price_zar_per_1k","Budget","Output Price (ZAR/1K tokens)","Provider output token price in ZAR per 1,000 tokens.","Decimal",null,false,false,true,true,true,"{\"min\":0}",false,null),
            ("budget.allow_unknown_pricing","Budget","Allow Unknown Pricing","Allow execution when provider pricing is not configured.","Boolean","\"true\"",false,false,true,true,true,null,false,null),
            // Evaluation
            ("evaluation.min_groundedness","Evaluation","Min Groundedness","Minimum acceptable groundedness score (0.0–1.0).","Percentage","\"0.80\"",false,false,true,true,true,"{\"min\":0,\"max\":1}",false,null),
            ("evaluation.min_relevance","Evaluation","Min Relevance","Minimum acceptable relevance score (0.0–1.0).","Percentage","\"0.80\"",false,false,true,true,true,"{\"min\":0,\"max\":1}",false,null),
            ("evaluation.min_safety","Evaluation","Min Safety","Minimum acceptable safety score (0.0–1.0).","Percentage","\"0.95\"",false,false,true,true,true,"{\"min\":0,\"max\":1}",false,null),
            ("evaluation.min_overall","Evaluation","Min Overall Score","Minimum acceptable overall evaluation score (0.0–1.0).","Percentage","\"0.82\"",false,false,true,true,true,"{\"min\":0,\"max\":1}",false,null),
            ("evaluation.failure_action","Evaluation","Failure Action","Action to take when evaluation fails.",  "Enum","\"Review\"",false,false,true,true,true,null,false,"\"Allow,Warn,Review,Block\""),
            // Retention
            ("retention.trace_days","Retention","Trace Retention (days)","Number of days to retain trace records.","Integer","\"90\"",false,false,true,true,true,"{\"min\":0}",false,null),
            ("retention.evaluation_days","Retention","Evaluation Retention (days)","Number of days to retain evaluation records.","Integer","\"180\"",false,false,true,true,true,"{\"min\":0}",false,null),
            ("retention.replay_days","Retention","Replay Retention (days)","Number of days to retain replay records.","Integer","\"90\"",false,false,true,true,true,"{\"min\":0}",false,null),
            ("retention.store_provider_payloads","Retention","Store Provider Payloads","Whether to store provider request payloads.","Boolean","\"false\"",false,false,true,true,true,null,false,null),
            ("retention.store_provider_responses","Retention","Store Provider Responses","Whether to store provider response payloads.","Boolean","\"false\"",false,false,true,true,true,null,false,null),
            ("retention.redaction_level","Retention","Default Redaction Level","Default redaction level for sensitive data.","Enum","\"Standard\"",false,false,true,true,true,null,false,"\"None,Standard,Strict\""),
            ("retention.allow_sensitive_reveal","Retention","Allow Sensitive Reveal","Allow authorised users to reveal sensitive trace artifacts.","Boolean","\"false\"",false,false,true,true,false,null,false,null),
            // Feature flags
            ("feature.provider_execution","Feature Flags","Provider Execution","Enable AI provider execution.","Boolean","\"true\"",false,false,false,true,true,null,false,null),
            ("feature.replay_execution","Feature Flags","Replay Execution","Enable replay experiment execution.","Boolean","\"true\"",false,false,false,true,true,null,false,null),
            ("feature.plugin_activation","Feature Flags","Plugin Activation","Allow plugin activation.","Boolean","\"true\"",false,false,false,true,true,null,false,null),
            ("feature.policy_enforcement","Feature Flags","Policy Enforcement","Enable policy enforcement.","Boolean","\"true\"",false,false,false,true,true,null,false,null),
            ("feature.experimental","Feature Flags","Experimental Features","Enable experimental features.","Boolean","\"false\"",false,false,false,true,true,null,false,null),
            ("feature.sensitive_trace_reveal","Feature Flags","Sensitive Trace Reveal","Allow sensitive trace artifact reveal.","Boolean","\"false\"",false,false,false,true,false,null,false,null),
            // Plugin
            ("plugin.allow_workspace_registration","Plugin","Allow Workspace Registration","Allow workspace-scoped plugin registration.","Boolean","\"true\"",false,false,true,true,true,null,false,null),
            ("plugin.allow_manifest_url","Plugin","Allow Manifest URL","Allow plugins to be registered by manifest URL.","Boolean","\"true\"",false,false,true,true,true,null,false,null),
            ("plugin.require_healthy","Plugin","Require Healthy Plugin","Require plugins to pass health check before activation.","Boolean","\"true\"",false,false,true,true,true,null,false,null),
            ("plugin.require_compatibility","Plugin","Require Compatibility","Require compatibility validation before plugin activation.","Boolean","\"true\"",false,false,true,true,true,null,false,null),
            ("plugin.allow_platform","Plugin","Allow Platform Plugins","Allow platform-scoped plugins.","Boolean","\"true\"",false,false,false,false,false,null,false,null),
            // Policy
            ("policy.enforcement_enabled","Policy","Policy Enforcement Enabled","Whether policy enforcement is active.","Boolean","\"true\"",false,false,true,true,true,null,false,null),
            ("policy.default_denial_behaviour","Policy","Default Denial Behaviour","Behaviour when no matching policy rule exists.","Enum","\"Allow\"",false,false,true,true,true,null,false,"\"Allow,Block\""),
            ("policy.require_before_provider","Policy","Require Policy Before Provider","Require policy evaluation before provider invocation.","Boolean","\"true\"",false,false,true,true,true,null,false,null),
            ("policy.audit_all","Policy","Audit All Policy Decisions","Audit every policy decision, including allows.","Boolean","\"true\"",false,false,true,true,true,null,false,null),
        };

        foreach (var (key, cat, display, desc, vtype, defVal, isSecret, isReq, allowOrg, allowWs, allowEnv, rules, restart, allowed) in defs)
        {
            var defValSql = defVal is null ? "NULL" : $"'{EscapeSql(defVal)}'";
            var rulesSql = rules is null ? "NULL" : $"'{EscapeSql(rules)}'";
            var allowedSql = allowed is null ? "NULL" : $"'{EscapeSql(allowed)}'";
            var isSecretSql = isSecret ? "true" : "false";
            var isReqSql = isReq ? "true" : "false";
            var allowOrgSql = allowOrg ? "true" : "false";
            var allowWsSql = allowWs ? "true" : "false";
            var allowEnvSql = allowEnv ? "true" : "false";
            var restartSql = restart ? "true" : "false";
            var nowSql = ActiveProvider.Contains("Npgsql", StringComparison.Ordinal)
                ? $"TIMESTAMPTZ '{now}'"
                : $"'{now}'";

            if (ActiveProvider.Contains("Npgsql", StringComparison.Ordinal))
            {
                m.Sql($"INSERT INTO \"SettingDefinitions\" (\"Key\",\"Category\",\"DisplayName\",\"Description\",\"ValueType\",\"DefaultValue\",\"IsSecret\",\"IsRequired\",\"AllowsOrganisationOverride\",\"AllowsWorkspaceOverride\",\"AllowsEnvironmentOverride\",\"ValidationRules\",\"RequiresRestart\",\"AllowedValues\",\"UpdatedAt\") " +
                      $"VALUES ('{key}','{cat}','{display}','{EscapeSql(desc)}','{vtype}',{defValSql},{isSecretSql},{isReqSql},{allowOrgSql},{allowWsSql},{allowEnvSql},{rulesSql},{restartSql},{allowedSql},{nowSql}) " +
                      $"ON CONFLICT (\"Key\") DO NOTHING;");
            }
            else
            {
                m.Sql($"INSERT OR IGNORE INTO \"SettingDefinitions\" (\"Key\",\"Category\",\"DisplayName\",\"Description\",\"ValueType\",\"DefaultValue\",\"IsSecret\",\"IsRequired\",\"AllowsOrganisationOverride\",\"AllowsWorkspaceOverride\",\"AllowsEnvironmentOverride\",\"ValidationRules\",\"RequiresRestart\",\"AllowedValues\",\"UpdatedAt\") " +
                      $"VALUES ('{key}','{cat}','{display}','{EscapeSql(desc)}','{vtype}',{defValSql},{isSecretSql},{isReqSql},{allowOrgSql},{allowWsSql},{allowEnvSql},{rulesSql},{restartSql},{allowedSql},'{now}');");
            }
        }
    }

    private static void BackfillEnvironments(MigrationBuilder m)
    {
        var bootstrapActor = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var now = "2026-07-23 00:00:00+00";

        if (ActiveProvider.Contains("Npgsql", StringComparison.Ordinal))
        {
            m.Sql($@"
INSERT INTO ""RuntimeEnvironments"" (""Id"", ""OrganisationId"", ""WorkspaceId"", ""Name"", ""Slug"", ""EnvironmentType"", ""Description"", ""Status"", ""IsDefault"", ""CreatedAt"", ""CreatedBy"", ""UpdatedAt"", ""Revision"")
SELECT
    gen_random_uuid(),
    w.""OrganisationId"",
    w.""Id"",
    'Development',
    'development',
    'Development',
    'Default development environment created during platform upgrade.',
    'Active',
    true,
    TIMESTAMPTZ '{now}',
    '{bootstrapActor}',
    TIMESTAMPTZ '{now}',
    1
FROM ""Workspaces"" w
WHERE NOT EXISTS (
    SELECT 1 FROM ""RuntimeEnvironments"" e WHERE e.""WorkspaceId"" = w.""Id""
);");
        }
        else
        {
            // SQLite: select all workspaces and insert for each that has no environment
            m.Sql($@"
INSERT INTO ""RuntimeEnvironments"" (""Id"", ""OrganisationId"", ""WorkspaceId"", ""Name"", ""Slug"", ""EnvironmentType"", ""Description"", ""Status"", ""IsDefault"", ""CreatedAt"", ""CreatedBy"", ""UpdatedAt"", ""Revision"")
SELECT
    lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' || substr(lower(hex(randomblob(2))),2) || '-' || substr('89ab',abs(random()) % 4 + 1, 1) || substr(lower(hex(randomblob(2))),2) || '-' || lower(hex(randomblob(6))),
    w.""OrganisationId"",
    w.""Id"",
    'Development',
    'development',
    'Development',
    'Default development environment created during platform upgrade.',
    'Active',
    1,
    '{now}',
    '{bootstrapActor}',
    '{now}',
    1
FROM ""Workspaces"" w
WHERE NOT EXISTS (
    SELECT 1 FROM ""RuntimeEnvironments"" e WHERE e.""WorkspaceId"" = w.""Id""
);");
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ConfigurationChanges");
        migrationBuilder.DropTable("SecretReferences");
        migrationBuilder.DropTable("SettingValues");
        migrationBuilder.DropTable("SettingDefinitions");
        migrationBuilder.DropTable("RuntimeEnvironments");
    }

    private static string EscapeSql(string value) => value.Replace("'", "''");
}
