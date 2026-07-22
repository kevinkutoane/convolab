using ConvoLab.Application.TraceStudio;
using Microsoft.AspNetCore.Mvc;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/traces")]
public sealed class TraceStudioController(ITraceStudioService traces) : ControllerBase
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
        => Ok(await traces.GetAsync(id, includeSensitive, cancellationToken));
}
