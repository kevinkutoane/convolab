using System.Globalization;
using System.Text.Json;
using ConvoLab.Application.Settings;
using ConvoLab.Domain.Settings;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConvoLab.Infrastructure.Settings;

/// <summary>
/// Resolves the effective configuration for a given scope by walking the hierarchy:
/// Platform default → Organisation override → Workspace override → Environment override.
/// </summary>
public sealed class EffectiveConfigurationResolver : IEffectiveConfigurationResolver
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EffectiveConfigurationResolver> _logger;

    public EffectiveConfigurationResolver(
        ApplicationDbContext db, IConfiguration configuration,
        ILogger<EffectiveConfigurationResolver> logger)
    {
        _db = db; _configuration = configuration; _logger = logger;
    }

    public async Task<IReadOnlyList<EffectiveSettingResult>> ResolveAsync(
        Guid organisationId, Guid workspaceId, Guid? environmentId, CancellationToken ct = default)
    {
        var definitions = await _db.SettingDefinitions.AsNoTracking().ToListAsync(ct);
        var scopedValues = await LoadScopedValuesAsync(organisationId, workspaceId, environmentId, ct);

        return definitions
            .Select(def => Resolve(def, organisationId, workspaceId, environmentId, scopedValues))
            .ToList();
    }

    public async Task<EffectiveSettingResult?> ResolveOneAsync(
        Guid organisationId, Guid workspaceId, Guid? environmentId, string key, CancellationToken ct = default)
    {
        var def = await _db.SettingDefinitions.AsNoTracking().SingleOrDefaultAsync(d => d.Key == key, ct);
        if (def is null) return null;

        var scopedValues = await LoadScopedValuesAsync(organisationId, workspaceId, environmentId, ct);
        return Resolve(def, organisationId, workspaceId, environmentId, scopedValues);
    }

    public async Task<ConfigurationSnapshot> CreateSnapshotAsync(
        Guid organisationId, Guid workspaceId, Guid environmentId, CancellationToken ct = default)
    {
        var results = await ResolveAsync(organisationId, workspaceId, environmentId, ct);
        var env = await _db.RuntimeEnvironments.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == environmentId, ct);

        string? Get(string key) => results.FirstOrDefault(r => r.Key == key)?.EffectiveValue?.Trim('"');
        bool GetBool(string key, bool fallback = true) =>
            bool.TryParse(Get(key)?.Trim('"'), out var v) ? v : fallback;
        decimal? GetDecimal(string key) =>
            decimal.TryParse(Get(key)?.Trim('"'), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;

        var featureKeys = new[]
        {
            SettingKeys.FeatureProviderExecution, SettingKeys.FeatureReplayExecution,
            SettingKeys.FeaturePluginActivation, SettingKeys.FeaturePolicyEnforcement,
            SettingKeys.FeatureExperimental, SettingKeys.FeatureSensitiveTraceReveal
        };
        var flags = featureKeys.ToDictionary(k => k, k => Get(k));

        var revision = $"{workspaceId:N}-{environmentId:N}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        return new ConfigurationSnapshot(
            revision, environmentId,
            env?.Name ?? environmentId.ToString(),
            Get(SettingKeys.AiProvider), Get(SettingKeys.AiModel),
            GetDecimal(SettingKeys.MonthlyBudgetZar),
            GetDecimal(SettingKeys.EvalMinGroundedness), GetDecimal(SettingKeys.EvalMinRelevance),
            GetDecimal(SettingKeys.EvalMinSafety), GetDecimal(SettingKeys.EvalMinOverall),
            Get(SettingKeys.EvalFailureAction),
            GetBool(SettingKeys.PolicyEnforcementEnabled),
            GetBool(SettingKeys.FeatureProviderExecution),
            flags, DateTimeOffset.UtcNow);
    }

    // ──────────────────────────────────────────────────────────────────────────

    private async Task<Dictionary<(string key, string scope, Guid? orgId, Guid? wsId, Guid? envId), SettingValueRecord>>
        LoadScopedValuesAsync(Guid organisationId, Guid workspaceId, Guid? environmentId, CancellationToken ct)
    {
        var query = _db.SettingValues.AsNoTracking()
            .Where(sv =>
                (sv.Scope == "Organisation" && sv.OrganisationId == organisationId) ||
                (sv.Scope == "Workspace" && sv.WorkspaceId == workspaceId) ||
                (environmentId.HasValue && sv.Scope == "Environment" && sv.EnvironmentId == environmentId));

        var rows = await query.ToListAsync(ct);
        return rows.ToDictionary(r => (r.DefinitionKey, r.Scope,
            r.OrganisationId, r.WorkspaceId, r.EnvironmentId));
    }

    private EffectiveSettingResult Resolve(
        SettingDefinitionRecord def,
        Guid organisationId, Guid workspaceId, Guid? environmentId,
        Dictionary<(string key, string scope, Guid? orgId, Guid? wsId, Guid? envId), SettingValueRecord> scopedValues)
    {
        // Try environment scope first (highest priority)
        SettingValueRecord? winner = null;
        SettingScope scope = SettingScope.Platform;
        Guid? sourceId = null;
        bool isInherited = true;

        if (environmentId.HasValue && def.AllowsEnvironmentOverride)
        {
            scopedValues.TryGetValue((def.Key, "Environment", null, null, environmentId), out winner);
            if (winner is not null) { scope = SettingScope.Environment; sourceId = environmentId; isInherited = false; }
        }

        if (winner is null && def.AllowsWorkspaceOverride)
        {
            scopedValues.TryGetValue((def.Key, "Workspace", null, workspaceId, null), out winner);
            if (winner is not null) { scope = SettingScope.Workspace; sourceId = workspaceId; isInherited = false; }
        }

        if (winner is null && def.AllowsOrganisationOverride)
        {
            scopedValues.TryGetValue((def.Key, "Organisation", organisationId, null, null), out winner);
            if (winner is not null) { scope = SettingScope.Organisation; sourceId = organisationId; isInherited = true; }
        }

        // Fall back to environment-variable (bootstrap compat) or platform default
        string? effectiveValue = winner?.ValueJson;
        if (effectiveValue is null)
        {
            effectiveValue = GetEnvVarFallback(def.Key) ?? def.DefaultValue;
            isInherited = true;
        }

        var inheritedDisplay = scope switch
        {
            SettingScope.Organisation => "Organisation default",
            SettingScope.Platform => "Platform default",
            _ => null
        };

        return new EffectiveSettingResult(
            def.Key, effectiveValue,
            Enum.Parse<SettingValueType>(def.ValueType),
            scope, sourceId,
            isInherited, def.IsSecret,
            "Valid", def.RequiresRestart,
            def.DisplayName, def.Category, inheritedDisplay);
    }

    private static string? GetEnvVarFallback(string key) => key switch
    {
        SettingKeys.AiModel => WrapString(Environment.GetEnvironmentVariable("GEMINI_MODEL")),
        SettingKeys.MonthlyBudgetZar => WrapString(Environment.GetEnvironmentVariable("CONVOLAB_MONTHLY_AI_BUDGET_ZAR")),
        SettingKeys.AiInputPriceZarPer1K => WrapString(Environment.GetEnvironmentVariable("GEMINI_INPUT_PRICE_ZAR_PER_1K")),
        SettingKeys.AiOutputPriceZarPer1K => WrapString(Environment.GetEnvironmentVariable("GEMINI_OUTPUT_PRICE_ZAR_PER_1K")),
        SettingKeys.EvalMinGroundedness => WrapString(Environment.GetEnvironmentVariable("CONVOLAB_EVALUATION_MIN_GROUNDEDNESS")),
        SettingKeys.EvalMinRelevance => WrapString(Environment.GetEnvironmentVariable("CONVOLAB_EVALUATION_MIN_RELEVANCE")),
        SettingKeys.EvalMinSafety => WrapString(Environment.GetEnvironmentVariable("CONVOLAB_EVALUATION_MIN_SAFETY")),
        SettingKeys.EvalMinOverall => WrapString(Environment.GetEnvironmentVariable("CONVOLAB_EVALUATION_MIN_OVERALL")),
        SettingKeys.EvalFailureAction => WrapString(Environment.GetEnvironmentVariable("CONVOLAB_EVALUATION_FAILURE_ACTION")),
        _ => null
    };

    private static string? WrapString(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : $"\"{value.Trim()}\"";
}
