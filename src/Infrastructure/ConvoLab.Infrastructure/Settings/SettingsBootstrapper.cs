using System.Globalization;
using System.Text.Json;
using ConvoLab.Domain.Settings;
using ConvoLab.Domain.WorkspaceIdentity;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConvoLab.Infrastructure.Settings;

/// <summary>
/// Seeds the default Development environment for any workspace that lacks one,
/// migrates configured environment variables into persisted SettingValues,
/// and creates the GEMINI_API_KEY SecretReference.
/// All operations are idempotent.
/// </summary>
public sealed class SettingsBootstrapper
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<SettingsBootstrapper> _logger;
    private static readonly Guid SystemActor = WorkspaceIdentityDefaults.BootstrapUserId;

    public SettingsBootstrapper(ApplicationDbContext db, IConfiguration config, ILogger<SettingsBootstrapper> logger)
    {
        _db = db; _config = config; _logger = logger;
    }

    public async Task ApplyAsync(CancellationToken ct = default)
    {
        await EnsureStudioDefinitionMetadataAsync(ct);
        await EnsureAnalyticsDefinitionsAsync(ct);
        var workspaces = await _db.Workspaces.AsNoTracking()
            .Where(w => w.Status == "Active")
            .ToListAsync(ct);

        foreach (var workspace in workspaces)
        {
            await EnsureDevelopmentEnvironmentAsync(workspace.Id, workspace.OrganisationId, ct);
            await MigrateEnvironmentVariablesAsync(workspace.Id, workspace.OrganisationId, ct);
            await EnsureGeminiSecretReferenceAsync(workspace.Id, ct);
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("SettingsBootstrapper: processed {Count} workspace(s).", workspaces.Count);
    }

    private async Task EnsureStudioDefinitionMetadataAsync(CancellationToken ct)
    {
        var provider = await _db.SettingDefinitions.SingleOrDefaultAsync(
            item => item.Key == SettingKeys.AiProvider, ct);
        if (provider is null) return;

        provider.ValueType = "Enum";
        provider.Description = "Select the execution provider. ConvoLab Deterministic is the local repeatable test provider; Gemini performs live external inference when its secret is configured.";
        provider.AllowedValues = JsonSerializer.Serialize(new[] { "Deterministic", "Gemini" });
        provider.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task EnsureAnalyticsDefinitionsAsync(CancellationToken ct)
    {
        var definitions = new[]
        {
            (SettingKeys.AnalyticsEventRetentionDays, "Analytics event retention", "Days to retain safe raw analytics events.", "90", "{\"min\":1,\"max\":730}"),
            (SettingKeys.AnalyticsHourlyRetentionDays, "Hourly analytics retention", "Days to retain hourly analytics aggregates.", "90", "{\"min\":1,\"max\":730}"),
            (SettingKeys.AnalyticsDailyRetentionDays, "Daily analytics retention", "Days to retain daily analytics aggregates.", "730", "{\"min\":30,\"max\":3650}"),
            (SettingKeys.AnalyticsExportRetentionDays, "Analytics export retention", "Days before generated analytics exports expire.", "7", "{\"min\":1,\"max\":30}")
        };
        var keys = definitions.Select(item => item.Item1).ToArray();
        var existing = await _db.SettingDefinitions.Where(item => keys.Contains(item.Key)).Select(item => item.Key).ToListAsync(ct);
        foreach (var (key, name, description, defaultValue, validation) in definitions.Where(item => !existing.Contains(item.Item1)))
        {
            _db.SettingDefinitions.Add(new SettingDefinitionRecord
            {
                Key = key, Category = "Retention", DisplayName = name, Description = description,
                ValueType = "Integer", DefaultValue = defaultValue, IsSecret = false, IsRequired = false,
                AllowsOrganisationOverride = true, AllowsWorkspaceOverride = true, AllowsEnvironmentOverride = true,
                ValidationRules = validation, RequiresRestart = false, UpdatedAt = DateTimeOffset.UtcNow
            });
        }
    }

    private async Task EnsureDevelopmentEnvironmentAsync(Guid workspaceId, Guid organisationId, CancellationToken ct)
    {
        var exists = await _db.RuntimeEnvironments.AnyAsync(e => e.WorkspaceId == workspaceId, ct);
        if (exists) return;

        var now = DateTimeOffset.UtcNow;
        _db.RuntimeEnvironments.Add(new RuntimeEnvironmentRecord
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            WorkspaceId = workspaceId,
            Name = "Development",
            Slug = "development",
            EnvironmentType = "Development",
            Description = "Default development environment.",
            Status = "Active",
            IsDefault = true,
            CreatedAt = now,
            CreatedBy = SystemActor,
            UpdatedAt = now,
            Revision = 1
        });

        _logger.LogInformation("SettingsBootstrapper: created Development environment for workspace {WorkspaceId}.", workspaceId);
    }

    private async Task MigrateEnvironmentVariablesAsync(Guid workspaceId, Guid organisationId, CancellationToken ct)
    {
        var migrations = BuildEnvVarMigrations();
        var existingKeys = await _db.SettingValues
            .Where(sv => sv.WorkspaceId == workspaceId && sv.Scope == "Workspace")
            .Select(sv => sv.DefinitionKey)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;

        foreach (var (envVar, settingKey) in migrations)
        {
            if (existingKeys.Contains(settingKey)) continue;

            var rawValue = Environment.GetEnvironmentVariable(envVar)
                           ?? _config[envVar.Replace("_", ":")];

            if (string.IsNullOrWhiteSpace(rawValue)) continue;

            var valueJson = FormatValueJson(settingKey, rawValue);
            if (valueJson is null)
            {
                _logger.LogWarning(
                    "SettingsBootstrapper: invalid external setting value for {SettingKey}. Skipping.",
                    settingKey);
                continue;
            }

            _db.SettingValues.Add(new SettingValueRecord
            {
                Id = Guid.NewGuid(),
                DefinitionKey = settingKey,
                Scope = "Workspace",
                OrganisationId = organisationId,
                WorkspaceId = workspaceId,
                ValueJson = valueJson,
                CreatedAt = now,
                CreatedBy = SystemActor,
                UpdatedAt = now,
                UpdatedBy = SystemActor,
                Revision = 1
            });

            _logger.LogInformation(
                "SettingsBootstrapper: migrated an external setting for {SettingKey} in workspace {WorkspaceId}.",
                settingKey,
                workspaceId);
        }
    }

    private async Task EnsureGeminiSecretReferenceAsync(Guid workspaceId, CancellationToken ct)
    {
        // Keep the reference as a captured value so EF parameterizes it. A literal
        // in the expression can be emitted in query-compilation and command logs.
        var defaultReference = "env:GEMINI_API_KEY";
        var exists = await _db.SecretReferences
            .AnyAsync(r => r.WorkspaceId == workspaceId && r.Reference == defaultReference, ct);
        if (exists) return;

        var now = DateTimeOffset.UtcNow;
        var keyPresent = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GEMINI_API_KEY"));

        _db.SecretReferences.Add(new SecretReferenceRecord
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            DisplayName = "Gemini API Key",
            Reference = defaultReference,
            Provider = "env",
            Status = keyPresent ? "NotValidated" : "Missing",
            CreatedAt = now,
            CreatedBy = SystemActor,
            UpdatedAt = now,
            Revision = 1
        });

        _logger.LogInformation("SettingsBootstrapper: created Gemini API Key secret reference for workspace {WorkspaceId}.", workspaceId);
    }

    private static IReadOnlyList<(string EnvVar, string SettingKey)> BuildEnvVarMigrations() =>
    [
        ("GEMINI_MODEL", SettingKeys.AiModel),
        ("CONVOLAB_MONTHLY_AI_BUDGET_ZAR", SettingKeys.MonthlyBudgetZar),
        ("GEMINI_INPUT_PRICE_ZAR_PER_1K", SettingKeys.AiInputPriceZarPer1K),
        ("GEMINI_OUTPUT_PRICE_ZAR_PER_1K", SettingKeys.AiOutputPriceZarPer1K),
        ("CONVOLAB_EVALUATION_MIN_GROUNDEDNESS", SettingKeys.EvalMinGroundedness),
        ("CONVOLAB_EVALUATION_MIN_RELEVANCE", SettingKeys.EvalMinRelevance),
        ("CONVOLAB_EVALUATION_MIN_SAFETY", SettingKeys.EvalMinSafety),
        ("CONVOLAB_EVALUATION_MIN_OVERALL", SettingKeys.EvalMinOverall),
        ("CONVOLAB_EVALUATION_FAILURE_ACTION", SettingKeys.EvalFailureAction),
    ];

    private static string? FormatValueJson(string key, string raw)
    {
        // For decimal/percentage/currency settings, ensure parseable number
        var numericKeys = new HashSet<string>
        {
            SettingKeys.MonthlyBudgetZar, SettingKeys.AiInputPriceZarPer1K, SettingKeys.AiOutputPriceZarPer1K,
            SettingKeys.EvalMinGroundedness, SettingKeys.EvalMinRelevance,
            SettingKeys.EvalMinSafety, SettingKeys.EvalMinOverall
        };

        if (numericKeys.Contains(key))
        {
            if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) || d < 0m)
                return null;
            return d.ToString(CultureInfo.InvariantCulture);
        }

        // Enum: EvalFailureAction
        if (key == SettingKeys.EvalFailureAction)
        {
            var normalised = raw.Trim();
            if (!Enum.TryParse<EvaluationFailureAction>(normalised, ignoreCase: true, out _))
                return null;
            return $"\"{normalised}\"";
        }

        // String (model name, etc.)
        return $"\"{raw.Trim().Replace("\"", "\\\"")}\"";
    }
}
