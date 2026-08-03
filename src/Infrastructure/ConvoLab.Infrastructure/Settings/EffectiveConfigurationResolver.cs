using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Settings;
using ConvoLab.Domain.Settings;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ConvoLab.Application.Operations;

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
        using var activity = ConvoLabTelemetry.ActivitySource.StartActivity("configuration.resolve");
        await RequireScopeAsync(organisationId, workspaceId, environmentId, ct);
        var definitions = await _db.SettingDefinitions.AsNoTracking().ToListAsync(ct);
        var scopedValues = await LoadScopedValuesAsync(organisationId, workspaceId, environmentId, ct);

        return definitions
            .Select(def => Resolve(def, organisationId, workspaceId, environmentId, scopedValues))
            .ToList();
    }

    public async Task<EffectiveSettingResult?> ResolveOneAsync(
        Guid organisationId, Guid workspaceId, Guid? environmentId, string key, CancellationToken ct = default)
    {
        await RequireScopeAsync(organisationId, workspaceId, environmentId, ct);
        var def = await _db.SettingDefinitions.AsNoTracking().SingleOrDefaultAsync(d => d.Key == key, ct);
        if (def is null) return null;

        var scopedValues = await LoadScopedValuesAsync(organisationId, workspaceId, environmentId, ct);
        return Resolve(def, organisationId, workspaceId, environmentId, scopedValues);
    }

    public async Task<ConfigurationSnapshot> CreateSnapshotAsync(
        Guid organisationId,
        Guid workspaceId,
        Guid environmentId,
        CancellationToken ct = default,
        IReadOnlyDictionary<string, string?>? executionOverrides = null)
    {
        var results = await ResolveAsync(organisationId, workspaceId, environmentId, ct);
        var env = await _db.RuntimeEnvironments.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == environmentId && e.WorkspaceId == workspaceId, ct)
            ?? throw NotFound("environment", environmentId);

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

        static bool SnapshotSafe(EffectiveSettingResult result) =>
            !result.IsSecret || result.Key == SettingKeys.AiSecretReference;

        var validatedOverrides = (executionOverrides
                ?? new Dictionary<string, string?>())
            .Where(item => item.Key is "provider" or "model" or "temperature" or "maximumOutputTokens")
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(
                item => $"execution.override.{item.Key}",
                item => item.Value,
                StringComparer.Ordinal);
        var revisionLines = results
            .Where(SnapshotSafe)
            .OrderBy(result => result.Key, StringComparer.Ordinal)
            .Select(result => string.Join('\u001f',
                result.Key,
                result.EffectiveValue ?? "null"))
            .Concat(validatedOverrides.Select(item =>
                string.Join('\u001f', item.Key, item.Value ?? "null")));
        var revisionPayload = string.Join('\n', revisionLines);
        var revision = $"sha256:{Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(revisionPayload))).ToLowerInvariant()}";

        var snapshot = new ConfigurationSnapshot(
            revision, environmentId,
            env?.Name ?? environmentId.ToString(),
            validatedOverrides.GetValueOrDefault("execution.override.provider")
                ?? Get(SettingKeys.AiProvider),
            validatedOverrides.GetValueOrDefault("execution.override.model")
                ?? Get(SettingKeys.AiModel),
            GetDecimal(SettingKeys.MonthlyBudgetZar),
            GetDecimal(SettingKeys.EvalMinGroundedness), GetDecimal(SettingKeys.EvalMinRelevance),
            GetDecimal(SettingKeys.EvalMinSafety), GetDecimal(SettingKeys.EvalMinOverall),
            Get(SettingKeys.EvalFailureAction),
            GetBool(SettingKeys.PolicyEnforcementEnabled),
            GetBool(SettingKeys.FeatureProviderExecution),
            flags, DateTimeOffset.UtcNow);
        if (!await _db.ConfigurationSnapshots.AnyAsync(item =>
            item.OrganisationId == organisationId && item.WorkspaceId == workspaceId
            && item.EnvironmentId == environmentId && item.Revision == revision, ct))
        {
            var values = results
                .Where(SnapshotSafe)
                .OrderBy(item => item.Key)
                .ToDictionary(item => item.Key, item => item.EffectiveValue);
            foreach (var item in validatedOverrides) values[item.Key] = item.Value;
            var provenance = results
                .Where(SnapshotSafe)
                .OrderBy(item => item.Key)
                .ToDictionary(
                    item => item.Key,
                    item => (object)new
                    {
                        scope = item.SourceScope.ToString(),
                        sourceId = item.SourceId
                    });
            foreach (var item in validatedOverrides)
                provenance[item.Key] = new { scope = "ExecutionOverride" };

            _db.ConfigurationSnapshots.Add(new ConfigurationSnapshotRecord
            {
                Id = Guid.NewGuid(), OrganisationId = organisationId, WorkspaceId = workspaceId,
                EnvironmentId = environmentId, Revision = revision,
                ValuesJson = JsonSerializer.Serialize(values),
                ProvenanceJson = JsonSerializer.Serialize(provenance),
                CreatedAt = snapshot.CreatedAt
            });
            await _db.SaveChangesAsync(ct);
        }
        return snapshot;
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

        // Environment variables are migrated to governed setting values by the
        // bootstrapper. Runtime resolution never lets a process variable silently
        // override a persisted scope.
        string? effectiveValue = winner?.ValueJson;
        if (effectiveValue is null)
        {
            effectiveValue = def.DefaultValue;
            isInherited = true;
        }
        effectiveValue = CanonicalizeValue(def, effectiveValue);

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
            def.DisplayName, def.Category, inheritedDisplay,
            def.Description, def.IsRequired,
            ParseAllowedValues(def.AllowedValues),
            def.AllowsEnvironmentOverride);
    }

    private static IReadOnlyList<string> ParseAllowedValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.Array => document.RootElement.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString()!)
                    .ToArray(),
                JsonValueKind.String => document.RootElement.GetString()?
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
                _ => []
            };
        }
        catch (JsonException)
        {
            return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }

    private async Task RequireScopeAsync(
        Guid organisationId, Guid workspaceId, Guid? environmentId, CancellationToken ct)
    {
        if (!await _db.Workspaces.AsNoTracking()
                .AnyAsync(workspace => workspace.Id == workspaceId && workspace.OrganisationId == organisationId, ct))
            throw NotFound("workspace", workspaceId);

        if (environmentId.HasValue
            && !await _db.RuntimeEnvironments.AsNoTracking()
                .AnyAsync(environment => environment.Id == environmentId
                    && environment.WorkspaceId == workspaceId
                    && environment.OrganisationId == organisationId, ct))
            throw NotFound("environment", environmentId.Value);
    }

    private static string? CanonicalizeValue(SettingDefinitionRecord definition, string? value)
    {
        if (value is null) return null;
        var scalar = ReadScalar(value);

        return Enum.Parse<SettingValueType>(definition.ValueType) switch
        {
            SettingValueType.Integer or SettingValueType.Duration
                when long.TryParse(scalar, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
                => integer.ToString(CultureInfo.InvariantCulture),
            SettingValueType.Decimal or SettingValueType.Percentage or SettingValueType.Currency
                when decimal.TryParse(scalar, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                => number.ToString(CultureInfo.InvariantCulture),
            SettingValueType.Boolean when bool.TryParse(scalar, out var boolean)
                => boolean ? "true" : "false",
            SettingValueType.String or SettingValueType.Enum or SettingValueType.SecretReference
                => JsonSerializer.Serialize(scalar),
            _ => value
        };
    }

    private static string ReadScalar(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.String
                ? document.RootElement.GetString() ?? string.Empty
                : document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            return value.Trim();
        }
    }

    private static ResourceNotFoundException NotFound(string resource, Guid id) =>
        new($"{resource}.not_found", $"{resource} '{id}' was not found.");
}
