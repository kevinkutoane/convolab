using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Api.Controllers;

/// <summary>
/// Provides paginated, filtered read-only access to the platform audit trail.
/// All endpoints require the PlatformAdministrator policy and never expose raw
/// tokens, subjects, email claims, authorization codes, or secret references.
/// </summary>
[ApiController]
[Authorize(Policy = "PlatformAdministrator")]
[Route("api/audit")]
public sealed class AuditController(
    ApplicationDbContext db,
    IConfiguration configuration,
    IHostEnvironment environment) : ControllerBase
{
    private const int MaxPageSize = 500;
    private const int DefaultPageSize = 50;

    /// <summary>
    /// Returns a paginated list of audit events, optionally filtered by workspace,
    /// action, resource type, actor type, and date range.
    /// </summary>
    [HttpGet("events")]
    public async Task<ActionResult<AuditEventPageResponse>> GetEvents(
        [FromQuery] Guid? workspaceId,
        [FromQuery] string? action,
        [FromQuery] string? resourceType,
        [FromQuery] string? actorType,
        [FromQuery] string? outcome,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var skip = (page - 1) * pageSize;

        var query = db.WorkspaceAuditEvents.AsNoTracking();

        if (workspaceId.HasValue)
            query = query.Where(e => e.WorkspaceId == workspaceId.Value);
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(e => e.Action == action);
        if (!string.IsNullOrWhiteSpace(resourceType))
            query = query.Where(e => e.ResourceType == resourceType);
        if (!string.IsNullOrWhiteSpace(actorType))
            query = query.Where(e => e.ActorType == actorType);
        if (!string.IsNullOrWhiteSpace(outcome))
            query = query.Where(e => e.Outcome == outcome);

        List<AuditEventRecord> allRecords;
        if (db.Database.IsSqlite())
        {
            var raw = await query.ToListAsync(ct);
            if (from.HasValue)
                raw = raw.Where(e => e.OccurredAt >= from.Value).ToList();
            if (to.HasValue)
                raw = raw.Where(e => e.OccurredAt <= to.Value).ToList();
            allRecords = raw;
        }
        else
        {
            if (from.HasValue)
                query = query.Where(e => e.OccurredAt >= from.Value);
            if (to.HasValue)
                query = query.Where(e => e.OccurredAt <= to.Value);
            allRecords = await query.ToListAsync(ct);
        }

        var total = allRecords.Count;
        var events = allRecords
            .OrderByDescending(e => e.OccurredAt)
            .Skip(skip)
            .Take(pageSize)
            .Select(e => new AuditEventSummary(
                e.Id,
                e.Scope,
                e.OrganisationId,
                e.WorkspaceId,
                e.ActorType,
                e.Action,
                e.ResourceType,
                e.ResourceId,
                e.Outcome,
                e.CorrelationId,
                e.OccurredAt))
            .ToList();

        return Ok(new AuditEventPageResponse(events, total, page, pageSize));
    }

    /// <summary>
    /// Returns a single audit event by ID. No sensitive actor or detail fields are returned.
    /// </summary>
    [HttpGet("events/{id:guid}")]
    public async Task<ActionResult<AuditEventSummary>> GetEvent(Guid id, CancellationToken ct)
    {
        var record = await db.WorkspaceAuditEvents.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == id, ct);

        if (record is null) return NotFound();
        return Ok(new AuditEventSummary(
            record.Id,
            record.Scope,
            record.OrganisationId,
            record.WorkspaceId,
            record.ActorType,
            record.Action,
            record.ResourceType,
            record.ResourceId,
            record.Outcome,
            record.CorrelationId,
            record.OccurredAt));
    }

    /// <summary>
    /// Returns aggregate counts grouped by action and resource type for the
    /// specified time window (default: last 7 days).
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<AuditSummaryResponse>> GetSummary(
        [FromQuery] Guid? workspaceId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct = default)
    {
        var effectiveFrom = from ?? DateTimeOffset.UtcNow.AddDays(-7);
        var effectiveTo = to ?? DateTimeOffset.UtcNow;

        var query = db.WorkspaceAuditEvents.AsNoTracking();
        if (workspaceId.HasValue)
            query = query.Where(e => e.WorkspaceId == workspaceId.Value);

        var rawList = await query.ToListAsync(ct);
        var filtered = rawList.Where(e => e.OccurredAt >= effectiveFrom && e.OccurredAt <= effectiveTo);

        var breakdown = filtered
            .GroupBy(e => new { e.Action, e.ResourceType, e.Outcome })
            .Select(g => new AuditActionBreakdown(g.Key.Action, g.Key.ResourceType, g.Key.Outcome, g.Count()))
            .OrderByDescending(b => b.Count)
            .ToList();

        var total = breakdown.Sum(b => b.Count);
        return Ok(new AuditSummaryResponse(total, effectiveFrom, effectiveTo, breakdown));
    }

    /// <summary>
    /// Exports all audit events in the given date range as a structured JSON array.
    /// Blocked when SafeMode:BlockAuditExports is true.
    /// </summary>
    [HttpGet("export")]
    public async Task<ActionResult> Export(
        [FromQuery] Guid? workspaceId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct = default)
    {
        var blockExports = configuration.GetValue<bool?>("SafeMode:BlockAuditExports");
        if (blockExports == true)
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { code = "safe_mode.audit_export_blocked", message = "Audit exports are blocked by SafeMode configuration." });

        var query = db.WorkspaceAuditEvents.AsNoTracking();
        if (workspaceId.HasValue)
            query = query.Where(e => e.WorkspaceId == workspaceId.Value);

        var raw = await query.ToListAsync(ct);
        if (from.HasValue)
            raw = raw.Where(e => e.OccurredAt >= from.Value).ToList();
        if (to.HasValue)
            raw = raw.Where(e => e.OccurredAt <= to.Value).ToList();

        var events = raw
            .OrderBy(e => e.OccurredAt)
            .Select(e => new AuditEventSummary(
                e.Id,
                e.Scope,
                e.OrganisationId,
                e.WorkspaceId,
                e.ActorType,
                e.Action,
                e.ResourceType,
                e.ResourceId,
                e.Outcome,
                e.CorrelationId,
                e.OccurredAt))
            .ToList();

        return Ok(new { exportedAt = DateTimeOffset.UtcNow, environment = environment.EnvironmentName, count = events.Count, events });
    }
}

// DTO records — no raw tokens, subjects, email, or secret fields.
public sealed record AuditEventSummary(
    Guid Id,
    string Scope,
    Guid? OrganisationId,
    Guid? WorkspaceId,
    string ActorType,
    string Action,
    string ResourceType,
    string? ResourceId,
    string Outcome,
    string CorrelationId,
    DateTimeOffset OccurredAt);

public sealed record AuditEventPageResponse(
    IReadOnlyList<AuditEventSummary> Events,
    int Total,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)Total / PageSize) : 0;
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

public sealed record AuditActionBreakdown(
    string Action,
    string ResourceType,
    string Outcome,
    int Count);

public sealed record AuditSummaryResponse(
    int Total,
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<AuditActionBreakdown> Breakdown);
