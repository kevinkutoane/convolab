using ConvoLab.Application.TraceStudio;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ConvoLab.Domain.WorkspaceIdentity;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.WorkspaceIdentity;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/traces")]
public sealed class TraceStudioController(ITraceStudioService traces, ApplicationDbContext db, WorkspaceRequestContext workspace) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType<TraceOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TraceOverviewDto>> GetOverview(CancellationToken cancellationToken)
        => Ok(await traces.GetOverviewAsync(cancellationToken));

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TraceSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TraceSummaryDto>>> List(
        [FromQuery] string? query = null,
        [FromQuery] string? status = null,
        [FromQuery] string? capability = null,
        [FromQuery] string? provider = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int limit = 250,
        CancellationToken cancellationToken = default)
        => Ok(await traces.ListAsync(new TraceSearchQuery(query, status, capability, provider, from, to, limit), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TraceDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TraceDetailDto>> Get(
        Guid id,
        [FromQuery] bool includeSensitive = false,
        CancellationToken cancellationToken = default)
    {
        if (includeSensitive && !User.HasClaim("permission", WorkspacePermissions.InspectSensitiveTrace))
            return Forbid();
        var result = await traces.GetAsync(id, includeSensitive, cancellationToken);
        if (includeSensitive)
        {
            db.WorkspaceAuditEvents.Add(AuthController.Audit("Workspace", workspace.OrganisationId, workspace.WorkspaceId, workspace.ActorType, workspace.UserId, User.Identity?.Name ?? "Authenticated actor", "Trace.SensitiveContentRevealed", "Trace", id.ToString(), "Succeeded", HttpContext.TraceIdentifier));
            await db.SaveChangesAsync(cancellationToken);
        }
        return Ok(result);
    }
}
