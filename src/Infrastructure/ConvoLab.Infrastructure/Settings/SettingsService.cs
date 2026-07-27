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
        var rows = await query.OrderByDescending(c => c.ChangedAt).Take(take).ToListAsync(ct);
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
        ConfigurationExportDto export;
        try { export = JsonSerializer.Deserialize<ConfigurationExportDto>(request.SettingsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!; }
        catch { throw new RequestValidationException("import.invalid_json", "The import payload is not valid JSON."); }

        if (export is null || export.Settings is null)
            throw new RequestValidationException("import.invalid_format", "The import payload does not match the expected format.");

        var changes = new List<ConfigurationChangeDto>();
        if (request.ValidateOnly) return changes;

        var ws = await _db.Workspaces.AsNoTracking().SingleOrDefaultAsync(w => w.Id == workspaceId, ct)
            ?? throw NotFound("workspace", workspaceId);

        foreach (var setting in export.Settings)
        {
            if (string.IsNullOrWhiteSpace(setting.Key) || setting.Value is null) continue;
            var def = await _db.SettingDefinitions.AsNoTracking().SingleOrDefaultAsync(d => d.Key == setting.Key, ct);
            if (def is null || def.IsSecret) continue;

            var upsertRequest = new UpsertSettingRequest(setting.Value, request.Reason, null);
            var dto = await UpsertAsync("Environment", ws.OrganisationId, workspaceId, environmentId, setting.Key, upsertRequest, actorId, actorDisplay, correlationId, ct);
            changes.Add(new ConfigurationChangeDto(Guid.NewGuid(), setting.Key, null, setting.Value ?? "", actorDisplay, DateTimeOffset.UtcNow, request.Reason, correlationId, "Succeeded", null));
        }

        return changes;
    }

    // ─── Internal helpers ─────────────────────────────────────────────────────

    private async Task<SettingValueDto> UpsertAsync(
        string scope, Guid orgId, Guid? wsId, Guid? envId,
        string settingKey, UpsertSettingRequest request,
        Guid actorId, string actorDisplay, string correlationId, CancellationToken ct)
    {
        var def = await _db.SettingDefinitions.AsNoTracking().SingleOrDefaultAsync(d => d.Key == settingKey, ct)
            ?? throw NotFound("setting_definition", Guid.Empty);

        if (string.IsNullOrWhiteSpace(request.ValueJson))
            throw new RequestValidationException("setting.value_required", "A value is required.");

        var existing = await _db.SettingValues.SingleOrDefaultAsync(sv =>
            sv.DefinitionKey == settingKey && sv.Scope == scope &&
            sv.OrganisationId == orgId && sv.WorkspaceId == wsId && sv.EnvironmentId == envId, ct);

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
                OrganisationId = scope == "Organisation" ? orgId : null,
                WorkspaceId = scope == "Workspace" ? wsId : null,
                EnvironmentId = scope == "Environment" ? envId : null,
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
        var existing = await _db.SettingValues.SingleOrDefaultAsync(sv =>
            sv.DefinitionKey == settingKey && sv.Scope == scope &&
            sv.OrganisationId == orgId && sv.WorkspaceId == wsId && sv.EnvironmentId == envId, ct);
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
