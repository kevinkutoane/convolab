using System.Text.Json;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Settings;
using ConvoLab.Application.Operations;
using ConvoLab.Domain.Settings;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.Analytics;
using ConvoLab.Domain.Analytics;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.Settings;

public sealed class EnvironmentService : IEnvironmentService
{
    private readonly ApplicationDbContext _db;

    public EnvironmentService(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<EnvironmentDto>> ListAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var rows = await _db.RuntimeEnvironments.AsNoTracking()
            .Where(e => e.WorkspaceId == workspaceId)
            .OrderBy(e => e.Name)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<EnvironmentDto> GetAsync(Guid workspaceId, Guid environmentId, CancellationToken ct = default)
    {
        var row = await _db.RuntimeEnvironments.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == environmentId && e.WorkspaceId == workspaceId, ct)
            ?? throw NotFound("environment", environmentId);
        return ToDto(row);
    }

    public async Task<EnvironmentDto> CreateAsync(Guid workspaceId, CreateEnvironmentRequest request, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Slug))
            throw Invalid("environment.invalid", "Name and slug are required.");
        if (!Enum.TryParse<EnvironmentType>(request.EnvironmentType, true, out var envType))
            throw Invalid("environment.type_invalid", $"Environment type '{request.EnvironmentType}' is not valid.");

        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await _db.RuntimeEnvironments.AnyAsync(e => e.WorkspaceId == workspaceId && e.Slug == slug, ct))
            throw new ResourceConflictException("environment.slug_exists", $"An environment with slug '{slug}' already exists in this workspace.");

        var now = DateTimeOffset.UtcNow;

        // If this is the new default, clear existing default
        if (request.IsDefault)
            await ClearDefaultAsync(workspaceId, ct);

        // First environment is always default
        var isDefault = request.IsDefault || !await _db.RuntimeEnvironments.AnyAsync(e => e.WorkspaceId == workspaceId, ct);

        var record = new RuntimeEnvironmentRecord
        {
            Id = Guid.NewGuid(),
            OrganisationId = (await _db.Workspaces.AsNoTracking().Select(w => new { w.Id, w.OrganisationId }).SingleAsync(w => w.Id == workspaceId, ct)).OrganisationId,
            WorkspaceId = workspaceId,
            Name = request.Name.Trim(),
            Slug = slug,
            EnvironmentType = envType.ToString(),
            Description = request.Description?.Trim() ?? "",
            Status = "Active",
            IsDefault = isDefault,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            Revision = 1
        };
        _db.RuntimeEnvironments.Add(record);
        await AddChangeAsync(record.OrganisationId, workspaceId, record.Id, "environment.created", null, record.Name, actorId, actorDisplay, correlationId, ct);
        await _db.SaveChangesAsync(ct);
        return ToDto(record);
    }

    public async Task<EnvironmentDto> UpdateAsync(Guid workspaceId, Guid environmentId, UpdateEnvironmentRequest request, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default)
    {
        var record = await _db.RuntimeEnvironments.SingleOrDefaultAsync(e => e.Id == environmentId && e.WorkspaceId == workspaceId, ct)
            ?? throw NotFound("environment", environmentId);

        if (record.Status == "Archived") throw new ResourceConflictException("environment.archived", "Archived environments are immutable.");
        if (record.Revision != request.ExpectedRevision) throw new ResourceConflictException("revision.conflict", "The resource changed. Refresh and retry.");
        if (!Enum.TryParse<EnvironmentType>(request.EnvironmentType, true, out _))
            throw Invalid("environment.type_invalid", $"Environment type '{request.EnvironmentType}' is not valid.");

        var prev = record.Name;
        record.Name = request.Name.Trim();
        record.Description = request.Description?.Trim() ?? "";
        record.EnvironmentType = request.EnvironmentType;
        record.UpdatedAt = DateTimeOffset.UtcNow;
        record.UpdatedBy = actorId;
        record.Revision++;

        await AddChangeAsync(record.OrganisationId, workspaceId, environmentId, "environment.updated", prev, record.Name, actorId, actorDisplay, correlationId, ct);
        await _db.SaveChangesAsync(ct);
        return ToDto(record);
    }

    public async Task ActivateAsync(Guid workspaceId, Guid environmentId, long expectedRevision, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default)
    {
        var record = await FindAsync(workspaceId, environmentId, expectedRevision, ct);
        if (record.Status == "Archived") throw new ResourceConflictException("environment.archived", "Archived environments cannot be activated.");
        record.Status = "Active"; record.UpdatedAt = DateTimeOffset.UtcNow; record.UpdatedBy = actorId; record.Revision++;
        await AddChangeAsync(record.OrganisationId, workspaceId, environmentId, "environment.activated", null, "Active", actorId, actorDisplay, correlationId, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SuspendAsync(Guid workspaceId, Guid environmentId, long expectedRevision, bool isAdmin, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default)
    {
        var record = await FindAsync(workspaceId, environmentId, expectedRevision, ct);
        if (record.Status == "Archived") throw new ResourceConflictException("environment.archived", "Archived environments are immutable.");
        if (record.IsDefault) throw new ResourceConflictException("environment.default", "Change the default environment before suspending this one.");
        if (record.EnvironmentType == "Production" && !isAdmin)
        {
            var otherActive = await _db.RuntimeEnvironments.CountAsync(e => e.WorkspaceId == workspaceId && e.Status == "Active" && e.Id != environmentId && e.EnvironmentType == "Production", ct);
            if (otherActive == 0) throw new ResourceConflictException("environment.last_production", "The final active Production environment cannot be suspended without Administrator access.");
        }
        record.Status = "Suspended"; record.UpdatedAt = DateTimeOffset.UtcNow; record.UpdatedBy = actorId; record.Revision++;
        await AddChangeAsync(record.OrganisationId, workspaceId, environmentId, "environment.suspended", null, "Suspended", actorId, actorDisplay, correlationId, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ArchiveAsync(Guid workspaceId, Guid environmentId, long expectedRevision, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default)
    {
        var record = await FindAsync(workspaceId, environmentId, expectedRevision, ct);
        if (record.Status == "Archived") return;
        if (record.IsDefault) throw new ResourceConflictException("environment.default", "Change the default environment before archiving this one.");
        record.Status = "Archived"; record.UpdatedAt = DateTimeOffset.UtcNow; record.UpdatedBy = actorId; record.Revision++;
        await AddChangeAsync(record.OrganisationId, workspaceId, environmentId, "environment.archived", null, "Archived", actorId, actorDisplay, correlationId, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MakeDefaultAsync(Guid workspaceId, Guid environmentId, long expectedRevision, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default)
    {
        var record = await FindAsync(workspaceId, environmentId, expectedRevision, ct);
        if (record.Status != "Active") throw new ResourceConflictException("environment.not_active", "Only active environments can be set as default.");
        await ClearDefaultAsync(workspaceId, ct);
        record.IsDefault = true; record.UpdatedAt = DateTimeOffset.UtcNow; record.UpdatedBy = actorId; record.Revision++;
        await AddChangeAsync(record.OrganisationId, workspaceId, environmentId, "environment.default_changed", null, record.Name, actorId, actorDisplay, correlationId, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<EnvironmentDto> SelectAsync(
        Guid workspaceId,
        Guid environmentId,
        Guid actorId,
        string actorType,
        string? actorRole,
        string correlationId,
        CancellationToken ct = default)
    {
        using var activity = ConvoLabTelemetry.ActivitySource.StartActivity("environment.selection");
        var record = await _db.RuntimeEnvironments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == environmentId && item.WorkspaceId == workspaceId, ct)
            ?? throw NotFound("environment", environmentId);
        if (record.Status != "Active")
            throw new ResourceConflictException("environment.inactive", $"Environment '{environmentId}' is not active.");

        var eventKey = AnalyticsKeys.Event("EnvironmentSelection", Guid.NewGuid(), "EnvironmentSelected");
        var analyticsEvent = new AnalyticsEventRecord
        {
            Id = Guid.NewGuid(), EventKey = eventKey, OrganisationId = record.OrganisationId,
            WorkspaceId = workspaceId, EnvironmentId = environmentId, ActorId = actorId,
            ActorType = actorType, ActorRole = actorRole, Capability = "Environment",
            EventType = "EnvironmentSelected", Outcome = "Succeeded", CostType = "Unavailable",
            SourceType = "RuntimeEnvironment", SourceId = environmentId,
            ConfigurationRevision = "selection:no-execution", CorrelationId = correlationId,
            OccurredAt = DateTimeOffset.UtcNow
        };
        _db.AnalyticsOutbox.Add(new AnalyticsOutboxRecord
        {
            Id = Guid.NewGuid(), EventKey = eventKey,
            PayloadJson = JsonSerializer.Serialize(analyticsEvent),
            Status = "Pending", AvailableAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return ToDto(record);
    }

    private async Task ClearDefaultAsync(Guid workspaceId, CancellationToken ct)
    {
        var current = await _db.RuntimeEnvironments.Where(e => e.WorkspaceId == workspaceId && e.IsDefault).ToListAsync(ct);
        foreach (var e in current) { e.IsDefault = false; e.UpdatedAt = DateTimeOffset.UtcNow; }
    }

    private async Task<RuntimeEnvironmentRecord> FindAsync(Guid workspaceId, Guid environmentId, long expectedRevision, CancellationToken ct)
    {
        var record = await _db.RuntimeEnvironments.SingleOrDefaultAsync(e => e.Id == environmentId && e.WorkspaceId == workspaceId, ct)
            ?? throw NotFound("environment", environmentId);
        if (record.Revision != expectedRevision) throw new ResourceConflictException("revision.conflict", "The resource changed. Refresh and retry.");
        return record;
    }

    private async Task AddChangeAsync(
        Guid orgId,
        Guid? wsId,
        Guid? envId,
        string key,
        string? prev,
        string next,
        Guid actor,
        string display,
        string correlation,
        CancellationToken ct)
    {
        var change = new ConfigurationChangeRecord
        {
            Id = Guid.NewGuid(), OrganisationId = orgId, WorkspaceId = wsId, EnvironmentId = envId,
            SettingKey = key, PreviousValueSummary = prev, NewValueSummary = next,
            ChangedBy = actor, ChangedByDisplay = display, ChangedAt = DateTimeOffset.UtcNow,
            CorrelationId = correlation, Outcome = "Succeeded", Revision = 1
        };
        _db.ConfigurationChanges.Add(change);
        await AnalyticsOutboxFactory.EnqueueConfigurationChangeAsync(_db, change, ct);
    }

    private static EnvironmentDto ToDto(RuntimeEnvironmentRecord r) =>
        new(r.Id, r.OrganisationId, r.WorkspaceId, r.Name, r.Slug,
            r.EnvironmentType, r.Description, r.Status, r.IsDefault,
            r.CreatedAt, r.UpdatedAt, r.Revision);

    private static ResourceNotFoundException NotFound(string resource, Guid id) =>
        new($"{resource}.not_found", $"{resource} '{id}' was not found.");

    private static RequestValidationException Invalid(string code, string detail) =>
        new(code, detail);
}
