using System.Security.Claims;
using ConvoLab.Application.Analytics;
using ConvoLab.Domain.WorkspaceIdentity;
using ConvoLab.Infrastructure.Analytics;
using ConvoLab.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Authorize(Policy = WorkspacePermissions.ViewWorkspaceAnalytics)]
[Route("api/workspaces/{workspaceId:guid}/analytics")]
public sealed class AnalyticsController(IAnalyticsService analytics, ApplicationDbContext db) : ControllerBase
{
    [Authorize(Policy = WorkspacePermissions.ViewEnvironmentAnalytics)]
    [HttpGet("filter-options")]
    public async Task<ActionResult<AnalyticsFilterOptionsDto>> FilterOptions(
        Guid workspaceId,
        [FromQuery] AnalyticsFilter filter,
        CancellationToken ct)
    {
        if (filter.ActorId.HasValue && !CanViewActors()) return Forbid();
        return Ok(await analytics.FilterOptionsAsync(
            ToQuery(workspaceId, filter),
            ct));
    }

    [HttpGet("overview")]
    public Task<ActionResult<AnalyticsDashboardDto>> Overview(Guid workspaceId, [FromQuery] AnalyticsFilter filter, CancellationToken ct) =>
        Dashboard("overview", workspaceId, filter, ct);

    [Authorize(Policy = WorkspacePermissions.ViewEnvironmentAnalytics)]
    [HttpGet("usage")]
    public Task<ActionResult<AnalyticsDashboardDto>> Usage(Guid workspaceId, [FromQuery] AnalyticsFilter filter, CancellationToken ct) =>
        Dashboard("usage", workspaceId, filter, ct);

    [Authorize(Policy = WorkspacePermissions.ViewCostAnalytics)]
    [HttpGet("cost")]
    public async Task<ActionResult<AnalyticsDashboardDto>> Cost(Guid workspaceId, [FromQuery] AnalyticsFilter filter, CancellationToken ct)
    {
        if (!await CanViewCostAsync(workspaceId, filter.EnvironmentId, ct)) return Forbid();
        return await Dashboard("cost", workspaceId, filter, ct);
    }

    [Authorize(Policy = WorkspacePermissions.ViewCostAnalytics)]
    [HttpGet("budget")]
    public async Task<ActionResult<AnalyticsDashboardDto>> Budget(Guid workspaceId, [FromQuery] AnalyticsFilter filter, CancellationToken ct)
    {
        if (!await CanViewCostAsync(workspaceId, filter.EnvironmentId, ct)) return Forbid();
        return await Dashboard("budget", workspaceId, filter, ct);
    }

    [Authorize(Policy = WorkspacePermissions.ViewQualityAnalytics)]
    [HttpGet("quality")]
    public Task<ActionResult<AnalyticsDashboardDto>> Quality(Guid workspaceId, [FromQuery] AnalyticsFilter filter, CancellationToken ct) =>
        Dashboard("quality", workspaceId, filter, ct);

    [Authorize(Policy = WorkspacePermissions.ViewGovernanceAnalytics)]
    [HttpGet("governance")]
    public Task<ActionResult<AnalyticsDashboardDto>> Governance(Guid workspaceId, [FromQuery] AnalyticsFilter filter, CancellationToken ct) =>
        Dashboard("governance", workspaceId, filter, ct);

    [Authorize(Policy = WorkspacePermissions.ViewEnvironmentAnalytics)]
    [HttpGet("performance")]
    public Task<ActionResult<AnalyticsDashboardDto>> Performance(Guid workspaceId, [FromQuery] AnalyticsFilter filter, CancellationToken ct) =>
        Dashboard("performance", workspaceId, filter, ct);

    [Authorize(Policy = WorkspacePermissions.ViewAdoptionAnalytics)]
    [HttpGet("adoption")]
    public Task<ActionResult<AnalyticsDashboardDto>> Adoption(Guid workspaceId, [FromQuery] AnalyticsFilter filter, CancellationToken ct) =>
        Dashboard("adoption", workspaceId, filter, ct);

    [Authorize(Policy = WorkspacePermissions.ViewQualityAnalytics)]
    [HttpGet("events")]
    public async Task<ActionResult<AnalyticsEventPageDto>> Events(
        Guid workspaceId,
        [FromQuery] AnalyticsFilter filter,
        [FromQuery] int take = 50,
        [FromQuery] string? cursor = null,
        CancellationToken ct = default)
    {
        if (filter.ActorId.HasValue && !CanViewActors()) return Forbid();
        return Ok(await analytics.EventsAsync(
            ToQuery(workspaceId, filter),
            take,
            cursor,
            await VisibilityAsync(workspaceId, filter.EnvironmentId, ct),
            ct));
    }

    [Authorize(Policy = WorkspacePermissions.ViewQualityAnalytics)]
    [HttpGet("events/{eventId:guid}")]
    public async Task<ActionResult<AnalyticsEventDto>> Event(Guid workspaceId, Guid eventId, CancellationToken ct)
    {
        var environmentId = await db.AnalyticsEvents.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.Id == eventId)
            .Select(item => (Guid?)item.EnvironmentId)
            .SingleOrDefaultAsync(ct);
        return Ok(await analytics.EventAsync(
            workspaceId,
            eventId,
            await VisibilityAsync(workspaceId, environmentId, ct),
            ct));
    }

    [Authorize(Policy = WorkspacePermissions.ViewQualityAnalytics)]
    [HttpGet("correlations/{correlationId}")]
    public async Task<ActionResult<IReadOnlyList<AnalyticsEventDto>>> Correlation(
        Guid workspaceId,
        string correlationId,
        CancellationToken ct)
    {
        var environmentId = await db.AnalyticsEvents.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.CorrelationId == correlationId)
            .Select(item => (Guid?)item.EnvironmentId)
            .FirstOrDefaultAsync(ct);
        return Ok(await analytics.CorrelationAsync(
            workspaceId,
            correlationId,
            await VisibilityAsync(
                workspaceId,
                User.IsInRole(nameof(WorkspaceRole.Engineer)) ? null : environmentId,
                ct),
            ct));
    }

    [Authorize(Policy = WorkspacePermissions.ExportAnalytics)]
    [HttpPost("exports")]
    public async Task<ActionResult<AnalyticsExportDto>> CreateExport(
        Guid workspaceId,
        CreateAnalyticsExportRequest request,
        CancellationToken ct)
    {
        var visibility = await VisibilityAsync(workspaceId, request.EnvironmentId, ct);
        var result = await analytics.CreateExportAsync(
            workspaceId,
            ActorId(),
            request,
            visibility,
            ct);
        return AcceptedAtAction(nameof(GetExport), new { workspaceId, exportId = result.Id }, result);
    }

    [Authorize(Policy = WorkspacePermissions.ExportAnalytics)]
    [HttpGet("exports")]
    public async Task<ActionResult<IReadOnlyList<AnalyticsExportDto>>> Exports(Guid workspaceId, CancellationToken ct) =>
        Ok(await analytics.ExportsAsync(workspaceId, ct));

    [Authorize(Policy = WorkspacePermissions.ExportAnalytics)]
    [HttpGet("exports/{exportId:guid}")]
    public async Task<ActionResult<AnalyticsExportDto>> GetExport(Guid workspaceId, Guid exportId, CancellationToken ct) =>
        Ok(await analytics.ExportAsync(workspaceId, exportId, ct));

    [Authorize(Policy = WorkspacePermissions.ExportAnalytics)]
    [HttpGet("exports/{exportId:guid}/download")]
    public async Task<IActionResult> Download(Guid workspaceId, Guid exportId, CancellationToken ct)
    {
        var file = await analytics.DownloadExportAsync(workspaceId, exportId, ct);
        var organisationId = await db.Workspaces.AsNoTracking()
            .Where(item => item.Id == workspaceId)
            .Select(item => (Guid?)item.OrganisationId)
            .SingleOrDefaultAsync(ct);
        var audit = AuthController.Audit(
            "Workspace",
            organisationId,
            workspaceId,
            User.FindFirstValue("actor_type") ?? "User",
            ActorId(),
            User.Identity?.Name ?? "Authenticated actor",
            "Analytics.ExportDownloaded",
            "AnalyticsExport",
            exportId.ToString(),
            "Succeeded",
            HttpContext.TraceIdentifier);
        db.WorkspaceAuditEvents.Add(audit);
        await AnalyticsOutboxFactory.EnqueueAuditAsync(db, audit, cancellationToken: ct);
        await db.SaveChangesAsync(ct);
        return File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }

    private async Task<ActionResult<AnalyticsDashboardDto>> Dashboard(
        string category,
        Guid workspaceId,
        AnalyticsFilter filter,
        CancellationToken ct)
    {
        if (filter.ActorId.HasValue && !CanViewActors()) return Forbid();
        return Ok(await analytics.DashboardAsync(
            category,
            ToQuery(workspaceId, filter),
            await VisibilityAsync(workspaceId, filter.EnvironmentId, ct),
            ct));
    }

    private AnalyticsQuery ToQuery(Guid workspaceId, AnalyticsFilter filter)
    {
        var to = filter.To ?? DateTimeOffset.UtcNow;
        return new AnalyticsQuery(
            workspaceId,
            filter.EnvironmentId,
            filter.From ?? to.AddDays(-30),
            to,
            filter.Granularity ?? "day",
            filter.Provider,
            filter.Model,
            filter.Capability,
            filter.Outcome,
            filter.ConfigurationRevision,
            filter.Prompt,
            filter.Workflow,
            filter.KnowledgeCollection,
            filter.ActorId,
            filter.EventType,
            filter.CostType);
    }

    private Guid ActorId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private bool CanViewActors() =>
        User.HasClaim("permission", WorkspacePermissions.ViewActorAnalytics);

    private async Task<AnalyticsFieldVisibility> VisibilityAsync(
        Guid workspaceId,
        Guid? environmentId,
        CancellationToken ct)
    {
        var canViewActors = User.HasClaim("permission", WorkspacePermissions.ViewActorAnalytics);
        var canViewEnvironment = User.HasClaim(
            "permission",
            WorkspacePermissions.ViewEnvironmentAnalytics);
        var canViewCostPermission = User.HasClaim(
            "permission",
            WorkspacePermissions.ViewCostAnalytics);
        var canViewCost = canViewCostPermission
            && await CanViewCostAsync(workspaceId, environmentId, ct);

        return new AnalyticsFieldVisibility(
            IncludeActor: canViewActors,
            IncludeCost: canViewCost,
            IncludeTokenUsage: canViewEnvironment && canViewCost,
            IncludeProviderDetails: canViewEnvironment,
            IncludeSensitiveSource: canViewActors);
    }

    private async Task<bool> CanViewCostAsync(Guid workspaceId, Guid? environmentId, CancellationToken ct)
    {
        if (!User.IsInRole(nameof(WorkspaceRole.Engineer))) return true;
        if (!environmentId.HasValue) return false;
        var type = await db.RuntimeEnvironments.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.Id == environmentId)
            .Select(item => item.EnvironmentType)
            .SingleOrDefaultAsync(ct);
        return type is "Development" or "Test";
    }
}

public sealed record AnalyticsFilter(
    Guid? EnvironmentId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Granularity,
    string? Provider,
    string? Model,
    string? Capability,
    string? Outcome,
    string? ConfigurationRevision,
    string? Prompt,
    string? Workflow,
    string? KnowledgeCollection,
    Guid? ActorId,
    string? EventType,
    string? CostType);
