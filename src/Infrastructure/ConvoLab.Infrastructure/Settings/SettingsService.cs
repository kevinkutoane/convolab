using System.Text.Json;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Settings;
using ConvoLab.Domain.Settings;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.Settings;

public sealed class SettingsService : ISettingsService
{
    private readonly ApplicationDbContext _db;
    private readonly IEffectiveConfigurationResolver _resolver;

    public SettingsService(ApplicationDbContext db, IEffectiveConfigurationResolver resolver)
    {
        _db = db; _resolver = resolver;
    }

    // ─── Workspace settings ───────────────────────────────────────────────────

    public async Task<IReadOnlyList<SettingValueDto>> ListWorkspaceSettingsAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var defs = await _db.SettingDefinitions.AsNoTracking().ToDictionaryAsync(d => d.Key, ct);
        var rows = await _db.SettingValues.AsNoTracking()
            .Where(sv => sv.WorkspaceId == workspaceId && sv.Scope == "Workspace")
            .ToListAsync(ct);
        return rows.Select(r => ToDto(r, defs.GetValueOrDefault(r.DefinitionKey))).ToList();
    }

    public async Task<IReadOnlyList<EffectiveSettingDto>> GetEffectiveWorkspaceSettingsAsync(Guid workspaceId, Guid? environmentId, CancellationToken ct = default)
    {
        var ws = await _db.Workspaces.AsNoTracking().SingleOrDefaultAsync(w => w.Id == workspaceId, ct)
            ?? throw NotFound("workspace", workspaceId);
        var results = await _resolver.ResolveAsync(ws.OrganisationId, workspaceId, environmentId, ct);
        return results.Select(ToEffectiveDto).ToList();
    }

    public async Task<SettingValueDto> UpsertWorkspaceSettingAsync(Guid workspaceId, string settingKey, UpsertSettingRequest request, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default)
    {
        var ws = await _db.Workspaces.AsNoTracking().SingleOrDefaultAsync(w => w.Id == workspaceId, ct)
            ?? throw NotFound("workspace", workspaceId);
        return await UpsertAsync("Workspace", ws.OrganisationId, workspaceId, null, settingKey, request, actorId, actorDisplay, correlationId, ct);
    }

    public async Task DeleteWorkspaceSettingAsync(Guid workspaceId, string settingKey, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default)
    {
        var ws = await _db.Workspaces.AsNoTracking().SingleOrDefaultAsync(w => w.Id == workspaceId, ct)
            ?? throw NotFound("workspace", workspaceId);
        await DeleteAsync("Workspace", ws.OrganisationId, workspaceId, null, settingKey, actorId, actorDisplay, correlationId, ct);
    }

    // ─── Environment settings ─────────────────────────────────────────────────

    public async Task<IReadOnlyList<SettingValueDto>> ListEnvironmentSettingsAsync(Guid workspaceId, Guid environmentId, CancellationToken ct = default)
    {
        var defs = await _db.SettingDefinitions.AsNoTracking().ToDictionaryAsync(d => d.Key, ct);
        var rows = await _db.SettingValues.AsNoTracking()
            .Where(sv => sv.EnvironmentId == environmentId && sv.Scope == "Environment")
            .ToListAsync(ct);
        return rows.Select(r => ToDto(r, defs.GetValueOrDefault(r.DefinitionKey))).ToList();
    }

    public async Task<IReadOnlyList<EffectiveSettingDto>> GetEffectiveEnvironmentSettingsAsync(Guid workspaceId, Guid environmentId, CancellationToken ct = default)
    {
        var ws = await _db.Workspaces.AsNoTracking().SingleOrDefaultAsync(w => w.Id == workspaceId, ct)
            ?? throw NotFound("workspace", workspaceId);
        return (await _resolver.ResolveAsync(ws.OrganisationId, workspaceId, environmentId, ct))
            .Select(ToEffectiveDto).ToList();
    }

    public async Task<SettingValueDto> UpsertEnvironmentSettingAsync(Guid workspaceId, Guid environmentId, string settingKey, UpsertSettingRequest request, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default)
    {
        var ws = await _db.Workspaces.AsNoTracking().SingleOrDefaultAsync(w => w.Id == workspaceId, ct)
            ?? throw NotFound("workspace", workspaceId);
        var env = await _db.RuntimeEnvironments.AsNoTracking().SingleOrDefaultAsync(e => e.Id == environmentId && e.WorkspaceId == workspaceId, ct)
            ?? throw NotFound("environment", environmentId);
        if (env.Status == "Archived") throw new ResourceConflictException("environment.archived", "Archived environments are immutable.");
        return await UpsertAsync("Environment", ws.OrganisationId, workspaceId, environmentId, settingKey, request, actorId, actorDisplay, correlationId, ct);
    }

    public async Task DeleteEnvironmentSettingAsync(Guid workspaceId, Guid environmentId, string settingKey, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default)
    {
        var ws = await _db.Workspaces.AsNoTracking().SingleOrDefaultAsync(w => w.Id == workspaceId, ct)
            ?? throw NotFound("workspace", workspaceId);
        await DeleteAsync("Environment", ws.OrganisationId, workspaceId, environmentId, settingKey, actorId, actorDisplay, correlationId, ct);
    }

    // ─── Organisation settings ────────────────────────────────────────────────

    public async Task<IReadOnlyList<SettingValueDto>> ListOrganisationSettingsAsync(Guid organisationId, CancellationToken ct = default)
    {
        var defs = await _db.SettingDefinitions.AsNoTracking().ToDictionaryAsync(d => d.Key, ct);
        var rows = await _db.SettingValues.AsNoTracking()
            .Where(sv => sv.OrganisationId == organisationId && sv.Scope == "Organisation")
            .ToListAsync(ct);
        return rows.Select(r => ToDto(r, defs.GetValueOrDefault(r.DefinitionKey))).ToList();
    }

    public async Task<SettingValueDto> UpsertOrganisationSettingAsync(Guid organisationId, string settingKey, UpsertSettingRequest request, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default)
        => await UpsertAsync("Organisation", organisationId, null, null, settingKey, request, actorId, actorDisplay, correlationId, ct);

    public async Task DeleteOrganisationSettingAsync(Guid organisationId, string settingKey, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default)
        => await DeleteAsync("Organisation", organisationId, null, null, settingKey, actorId, actorDisplay, correlationId, ct);

    // ─── Change history ───────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ConfigurationChangeDto>> GetChangeHistoryAsync(Guid workspaceId, Guid? environmentId, int take = 100, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 500);
        var query = _db.ConfigurationChanges.AsNoTracking().Where(c => c.WorkspaceId == workspaceId);
        if (environmentId.HasValue) query = query.Where(c => c.EnvironmentId == environmentId);
        // SQLite cannot ORDER BY DateTimeOffset columns; fetch the workspace-scoped
        // subset (append-only, bounded per workspace) and order client-side.
        var fetched = await query.ToListAsync(ct);
        var rows = fetched.OrderByDescending(c => c.ChangedAt).Take(take).ToList();
        var envNames = await _db.RuntimeEnvironments.AsNoTracking()
            .Where(e => rows.Select(r => r.EnvironmentId).Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);
        return rows.Select(r => new ConfigurationChangeDto(r.Id, r.SettingKey, r.PreviousValueSummary,
            r.NewValueSummary, r.ChangedByDisplay, r.ChangedAt, r.Reason, r.CorrelationId, r.Outcome,
            r.EnvironmentId.HasValue ? envNames.GetValueOrDefault(r.EnvironmentId.Value) : null)).ToList();
    }

    // ─── Export / Import ──────────────────────────────────────────────────────

    public async Task<ConfigurationExportDto> ExportAsync(Guid workspaceId, Guid environmentId, CancellationToken ct = default)
    {
        var ws = await _db.Workspaces.AsNoTracking().SingleOrDefaultAsync(w => w.Id == workspaceId, ct)
            ?? throw NotFound("workspace", workspaceId);
        var env = await _db.RuntimeEnvironments.AsNoTracking().SingleOrDefaultAsync(e => e.Id == environmentId && e.WorkspaceId == workspaceId, ct)
            ?? throw NotFound("environment", environmentId);
        var org = await _db.Organisations.AsNoTracking().SingleOrDefaultAsync(o => o.Id == ws.OrganisationId, ct);

        var effective = await _resolver.ResolveAsync(ws.OrganisationId, workspaceId, environmentId, ct);
        var exportSettings = effective
            .Where(s => !s.IsSecret)
            .Select(s => new ExportedSettingDto(s.Key, s.Category, s.DisplayName, s.EffectiveValue))
            .ToList();
        var flags = effective
            .Where(s => s.Category == "Feature Flags")
            .Select(s => new ExportedFeatureFlagDto(s.Key, s.EffectiveValue))
            .ToList();
        var providerMeta = new ExportedProviderMetadataDto(
            effective.FirstOrDefault(s => s.Key == SettingKeys.AiProvider)?.EffectiveValue?.Trim('"'),
            effective.FirstOrDefault(s => s.Key == SettingKeys.AiModel)?.EffectiveValue?.Trim('"'),
            bool.TryParse(effective.FirstOrDefault(s => s.Key == SettingKeys.AiProviderEnabled)?.EffectiveValue?.Trim('"'), out var enabled) && enabled);

        return new ConfigurationExportDto("1.0", org?.Name ?? ws.OrganisationId.ToString(), ws.Name, env.Name,
            DateTimeOffset.UtcNow, exportSettings, flags, providerMeta);
    }

    public async Task<IReadOnlyList<ConfigurationChangeDto>> ImportAsync(Guid workspaceId, Guid environmentId, ImportConfigurationRequest request, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default)
    {
        ConfigurationExportDto? export;
        try { export = JsonSerializer.Deserialize<ConfigurationExportDto>(request.SettingsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch { throw new RequestValidationException("import.invalid_json", "The import payload is not valid JSON."); }

        if (export?.Settings is null)
            throw new RequestValidationException("import.invalid_format", "The import payload does not match the expected format.");
        if (export.SchemaVersion != "1.0")
            throw new RequestValidationException("import.unsupported_schema", $"Unsupported export schema version '{export.SchemaVersion}'. Expected '1.0'.");

        var ws = await _db.Workspaces.AsNoTracking().SingleOrDefaultAsync(w => w.Id == workspaceId, ct)
            ?? throw NotFound("workspace", workspaceId);
        var env = await _db.RuntimeEnvironments.AsNoTracking().SingleOrDefaultAsync(e => e.Id == environmentId && e.WorkspaceId == workspaceId, ct)
            ?? throw NotFound("environment", environmentId);
        if (env.Status == "Archived")
            throw new ResourceConflictException("environment.archived", "Archived environments are immutable.");

        var defs = await _db.SettingDefinitions.AsNoTracking().ToDictionaryAsync(d => d.Key, ct);
        var currentRows = await _db.SettingValues.AsNoTracking()
            .Where(sv => sv.Scope == "Environment" && sv.EnvironmentId == environmentId)
            .ToDictionaryAsync(sv => sv.DefinitionKey, ct);

        // ─── Stage 1: validate every entry and build the preview ────────────
        var now = DateTimeOffset.UtcNow;
        var preview = new List<(ExportedSettingDto Setting, SettingDefinitionRecord? Def, string Outcome, string? Message)>();
        foreach (var setting in export.Settings)
        {
            if (string.IsNullOrWhiteSpace(setting.Key) || setting.Value is null)
            { preview.Add((setting, null, "Skipped", "Missing key or value.")); continue; }

            if (!defs.TryGetValue(setting.Key, out var def))
            { preview.Add((setting, null, "Skipped", "Unknown setting key.")); continue; }

            if (def.IsSecret)
            { preview.Add((setting, def, "Skipped", "Secret values are never imported.")); continue; }

            if (!def.AllowsEnvironmentOverride)
            { preview.Add((setting, def, "Skipped", "This setting cannot be overridden at environment scope.")); continue; }

            var validation = SettingValueValidator.Validate(ToDomainDefinition(def), setting.Value);
            if (!validation.IsValid)
            { preview.Add((setting, def, "Invalid", validation.Message)); continue; }

            var isChange = !currentRows.TryGetValue(setting.Key, out var current) || current.ValueJson != setting.Value;
            preview.Add((setting, def, isChange ? "Apply" : "Unchanged", null));
        }

        if (preview.Any(p => p.Outcome == "Invalid"))
        {
            var firstInvalid = preview.First(p => p.Outcome == "Invalid");
            throw new RequestValidationException("import.invalid_values",
                $"Import rejected: '{firstInvalid.Setting.Key}' — {firstInvalid.Message} No settings were changed.");
        }

        ConfigurationChangeDto ToPreviewDto((ExportedSettingDto Setting, SettingDefinitionRecord? Def, string Outcome, string? Message) p) =>
            new(Guid.NewGuid(), p.Setting.Key,
                currentRows.TryGetValue(p.Setting.Key, out var cur) ? SummariseSafe(p.Def?.IsSecret ?? false, cur.ValueJson) : "(inherited)",
                p.Setting.Value is null ? "" : SummariseSafe(p.Def?.IsSecret ?? false, p.Setting.Value),
                actorDisplay, now, p.Message ?? request.Reason, correlationId,
                request.ValidateOnly ? $"Preview:{p.Outcome}" : p.Outcome, env.Name);

        if (request.ValidateOnly)
            return preview.Select(ToPreviewDto).ToList();

        // ─── Stage 2: apply atomically ──────────────────────────────────
        var changes = new List<ConfigurationChangeDto>();
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            foreach (var entry in preview.Where(p => p.Outcome == "Apply"))
            {
                var upsertRequest = new UpsertSettingRequest(entry.Setting.Value!, request.Reason ?? "Configuration import", null);
                await UpsertAsync("Environment", ws.OrganisationId, workspaceId, environmentId, entry.Setting.Key, upsertRequest, actorId, actorDisplay, correlationId, ct);
            }
            await transaction.CommitAsync(ct);
        });

        changes.AddRange(preview.Select(ToPreviewDto));
        return changes;
    }

    public async Task<SettingsValidationResultDto> ValidateEnvironmentSettingsAsync(Guid workspaceId, Guid environmentId, CancellationToken ct = default)
    {
        var ws = await _db.Workspaces.AsNoTracking().SingleOrDefaultAsync(w => w.Id == workspaceId, ct)
            ?? throw NotFound("workspace", workspaceId);
        _ = await _db.RuntimeEnvironments.AsNoTracking().SingleOrDefaultAsync(e => e.Id == environmentId && e.WorkspaceId == workspaceId, ct)
            ?? throw NotFound("environment", environmentId);

        var defs = await _db.SettingDefinitions.AsNoTracking().ToDictionaryAsync(d => d.Key, ct);
        var effective = await _resolver.ResolveAsync(ws.OrganisationId, workspaceId, environmentId, ct);

        var entries = new List<SettingValidationEntryDto>();
        foreach (var result in effective)
        {
            if (!defs.TryGetValue(result.Key, out var def)) continue;
            if (result.EffectiveValue is null)
            {
                entries.Add(new SettingValidationEntryDto(result.Key, result.DisplayName, result.Category,
                    def.IsRequired ? "Invalid" : "Valid",
                    def.IsRequired ? "A required setting has no value at any scope." : null,
                    result.SourceScope.ToString()));
                continue;
            }
            var validation = SettingValueValidator.Validate(ToDomainDefinition(def), result.EffectiveValue);
            entries.Add(new SettingValidationEntryDto(result.Key, result.DisplayName, result.Category,
                validation.Status, validation.Message, result.SourceScope.ToString()));
        }

        // Cross-field rule: budget warning threshold must sit below the hard stop.
        var warn = GetDecimalValue(effective, SettingKeys.BudgetWarningThreshold);
        var hard = GetDecimalValue(effective, SettingKeys.BudgetHardStopThreshold);
        if (warn.HasValue && hard.HasValue && warn.Value >= hard.Value)
        {
            entries.Add(new SettingValidationEntryDto(SettingKeys.BudgetWarningThreshold,
                "Budget Warning Threshold", "Budget", "Invalid",
                "The warning threshold must be lower than the hard-stop threshold.", "Environment"));
        }

        var invalid = entries.Count(e => e.Status == "Invalid");
        var warnings = entries.Count(e => e.Status == "Warning");
        return new SettingsValidationResultDto(invalid == 0, entries.Count, invalid, warnings, entries, DateTimeOffset.UtcNow);
    }

    private static decimal? GetDecimalValue(IReadOnlyList<EffectiveSettingResult> results, string key)
    {
        var raw = results.FirstOrDefault(r => r.Key == key)?.EffectiveValue?.Trim('"');
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static SettingDefinition ToDomainDefinition(SettingDefinitionRecord r) =>
        new(r.Key, r.Category, r.DisplayName, r.Description,
            Enum.Parse<SettingValueType>(r.ValueType), r.DefaultValue,
            r.IsSecret, r.IsRequired,
            r.AllowsOrganisationOverride, r.AllowsWorkspaceOverride, r.AllowsEnvironmentOverride,
            r.ValidationRules, r.RequiresRestart, r.AllowedValues);

    // ─── Internal helpers ─────────────────────────────────────────────────────

    private async Task<SettingValueDto> UpsertAsync(
        string scope, Guid orgId, Guid? wsId, Guid? envId,
        string settingKey, UpsertSettingRequest request,
        Guid actorId, string actorDisplay, string correlationId, CancellationToken ct)
    {
        var def = await _db.SettingDefinitions.AsNoTracking().SingleOrDefaultAsync(d => d.Key == settingKey, ct)
            ?? throw new ResourceNotFoundException("setting_definition.not_found", $"Setting '{settingKey}' is not defined.");

        if (string.IsNullOrWhiteSpace(request.ValueJson))
            throw new RequestValidationException("setting.value_required", "A value is required.");

        // ─── Scope override rules from the definition ──────────────────────
        var overrideAllowed = scope switch
        {
            "Organisation" => def.AllowsOrganisationOverride,
            "Workspace" => def.AllowsWorkspaceOverride,
            "Environment" => def.AllowsEnvironmentOverride,
            _ => false
        };
        if (!overrideAllowed)
            throw new RequestValidationException("setting.scope_not_allowed",
                $"Setting '{settingKey}' cannot be overridden at {scope} scope.");

        // ─── Typed validation against the definition ───────────────────────
        var validation = SettingValueValidator.Validate(ToDomainDefinition(def), request.ValueJson);
        if (!validation.IsValid)
            throw new RequestValidationException("setting.invalid_value", validation.Message ?? "The value is invalid for this setting.");

        // ─── Production safeguards ─────────────────────────────────────────
        if (scope == "Environment" && envId.HasValue)
        {
            var env = await _db.RuntimeEnvironments.AsNoTracking().SingleOrDefaultAsync(e => e.Id == envId.Value, ct);
            if (env?.EnvironmentType == "Production")
            {
                if (string.IsNullOrWhiteSpace(request.Reason))
                    throw new RequestValidationException("setting.reason_required",
                        "Changes to Production environments require a reason.");

                var disablesEnforcement = ProtectedSettingKeys.IsEnforcementKey(settingKey) &&
                    request.ValueJson.Trim().Trim('"').Equals("false", StringComparison.OrdinalIgnoreCase);
                if (disablesEnforcement && !request.ConfirmProtectedChange)
                    throw new RequestValidationException("setting.confirmation_required",
                        "Disabling policy enforcement in Production requires explicit confirmation. Set confirmProtectedChange to true.");
            }
        }

        // Scope identifiers are stored sparsely: only the id matching the scope is set.
        // The query below must mirror the insert exactly, or updates create duplicates.
        Guid? scopedOrg = scope == "Organisation" ? orgId : null;
        Guid? scopedWs = scope == "Workspace" ? wsId : null;
        Guid? scopedEnv = scope == "Environment" ? envId : null;

        var existing = await _db.SettingValues.SingleOrDefaultAsync(sv =>
            sv.DefinitionKey == settingKey && sv.Scope == scope &&
            sv.OrganisationId == scopedOrg && sv.WorkspaceId == scopedWs && sv.EnvironmentId == scopedEnv, ct);

        var now = DateTimeOffset.UtcNow;
        string? previous = existing?.ValueJson;

        if (existing is not null)
        {
            if (request.ExpectedRevision.HasValue && existing.Revision != request.ExpectedRevision.Value)
                throw new ResourceConflictException("revision.conflict", "The resource changed. Refresh and retry.");
            existing.ValueJson = request.ValueJson;
            existing.UpdatedBy = actorId;
            existing.UpdatedAt = now;
            existing.Revision++;
        }
        else
        {
            existing = new SettingValueRecord
            {
                Id = Guid.NewGuid(), DefinitionKey = settingKey, Scope = scope,
                OrganisationId = scopedOrg,
                WorkspaceId = scopedWs,
                EnvironmentId = scopedEnv,
                ValueJson = request.ValueJson, CreatedAt = now, CreatedBy = actorId,
                UpdatedAt = now, UpdatedBy = actorId, Revision = 1
            };
            _db.SettingValues.Add(existing);
        }

        var summary = SummariseSafe(def.IsSecret, request.ValueJson);
        _db.ConfigurationChanges.Add(new ConfigurationChangeRecord
        {
            Id = Guid.NewGuid(), OrganisationId = orgId, WorkspaceId = wsId, EnvironmentId = envId,
            SettingKey = settingKey,
            PreviousValueSummary = previous is null ? null : SummariseSafe(def.IsSecret, previous),
            NewValueSummary = summary,
            ChangedBy = actorId, ChangedByDisplay = actorDisplay, ChangedAt = now,
            Reason = request.Reason, CorrelationId = correlationId, Outcome = "Succeeded", Revision = 1
        });

        await _db.SaveChangesAsync(ct);
        return ToDto(existing, def);
    }

    private async Task DeleteAsync(string scope, Guid orgId, Guid? wsId, Guid? envId, string settingKey, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct)
    {
        Guid? scopedOrg = scope == "Organisation" ? orgId : null;
        Guid? scopedWs = scope == "Workspace" ? wsId : null;
        Guid? scopedEnv = scope == "Environment" ? envId : null;

        var existing = await _db.SettingValues.SingleOrDefaultAsync(sv =>
            sv.DefinitionKey == settingKey && sv.Scope == scope &&
            sv.OrganisationId == scopedOrg && sv.WorkspaceId == scopedWs && sv.EnvironmentId == scopedEnv, ct);
        if (existing is null) return;

        var def = await _db.SettingDefinitions.AsNoTracking().SingleOrDefaultAsync(d => d.Key == settingKey, ct);
        _db.SettingValues.Remove(existing);
        _db.ConfigurationChanges.Add(new ConfigurationChangeRecord
        {
            Id = Guid.NewGuid(), OrganisationId = orgId, WorkspaceId = wsId, EnvironmentId = envId,
            SettingKey = settingKey,
            PreviousValueSummary = SummariseSafe(def?.IsSecret ?? false, existing.ValueJson),
            NewValueSummary = "(inherited)",
            ChangedBy = actorId, ChangedByDisplay = actorDisplay, ChangedAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId, Outcome = "Succeeded", Revision = 1
        });
        await _db.SaveChangesAsync(ct);
    }

    private static string SummariseSafe(bool isSecret, string valueJson)
    {
        if (isSecret) return "***";
        var trimmed = valueJson.Trim('"');
        return trimmed.Length > 100 ? trimmed[..97] + "..." : trimmed;
    }

    private static SettingValueDto ToDto(SettingValueRecord r, SettingDefinitionRecord? def) =>
        new(r.Id, r.DefinitionKey, def?.DisplayName ?? r.DefinitionKey, def?.Category ?? "",
            r.Scope, r.OrganisationId?.ToString(), r.WorkspaceId?.ToString(), r.EnvironmentId?.ToString(),
            def?.IsSecret == true ? "***" : r.ValueJson,
            def?.IsSecret ?? false, def?.ValueType ?? "String", r.UpdatedAt, r.Revision);

    private static EffectiveSettingDto ToEffectiveDto(EffectiveSettingResult r) =>
        new(r.Key, r.IsSecret ? null : r.EffectiveValue, r.ValueType.ToString(),
            r.SourceScope.ToString(), r.SourceId?.ToString(),
            r.IsInherited, r.IsSecret, r.ValidationStatus,
            r.RequiresRestart, r.DisplayName, r.Category, r.InheritedFromDisplay);

    private static ResourceNotFoundException NotFound(string resource, Guid id) =>
        new($"{resource}.not_found", $"{resource} '{id}' was not found.");
}
