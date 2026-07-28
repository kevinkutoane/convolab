using System.Security.Claims;
using ConvoLab.Application.Analytics;
using ConvoLab.Domain.WorkspaceIdentity;
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
        CancellationToken ct = default) =>
        Ok(await analytics.EventsAsync(ToQuery(workspaceId, filter), take, cursor, CanViewActors(), ct));

    [Authorize(Policy = WorkspacePermissions.ViewQualityAnalytics)]
    [HttpGet("events/{eventId:guid}")]
    public async Task<ActionResult<AnalyticsEventDto>> Event(Guid workspaceId, Guid eventId, CancellationToken ct) =>
        Ok(await analytics.EventAsync(workspaceId, eventId, CanViewActors(), ct));

    [Authorize(Policy = WorkspacePermissions.ViewQualityAnalytics)]
    [HttpGet("correlations/{correlationId}")]
    public async Task<ActionResult<IReadOnlyList<AnalyticsEventDto>>> Correlation(
        Guid workspaceId,
        string correlationId,
        CancellationToken ct) =>
        Ok(await analytics.CorrelationAsync(workspaceId, correlationId, CanViewActors(), ct));

    [Authorize(Policy = WorkspacePermissions.ExportAnalytics)]
    [HttpPost("exports")]
    public async Task<ActionResult<AnalyticsExportDto>> CreateExport(
        Guid workspaceId,
        CreateAnalyticsExportRequest request,
        CancellationToken ct)
    {
        var result = await analytics.CreateExportAsync(workspaceId, ActorId(), request, ct);
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
        return File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }

    private async Task<ActionResult<AnalyticsDashboardDto>> Dashboard(
        string category,
        Guid workspaceId,
        AnalyticsFilter filter,
        CancellationToken ct) =>
        Ok(await analytics.DashboardAsync(category, ToQuery(workspaceId, filter), ct));

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
            filter.Workflow);
    }

    private bool CanViewActors() => User.HasClaim("permission", WorkspacePermissions.ViewActorAnalytics);

    private Guid ActorId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

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
    string? Workflow);
