using ConvoLab.Application.IntelligenceStudio;
using Microsoft.AspNetCore.Mvc;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/intelligence")]
public sealed class IntelligenceCenterController : ControllerBase
{
    private readonly IIntelligenceStudioService _intelligence;

    public IntelligenceCenterController(IIntelligenceStudioService intelligence)
    {
        _intelligence = intelligence;
    }

    [HttpGet("overview")]
    [ProducesResponseType<IntelligenceOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IntelligenceOverviewDto>> GetOverview(CancellationToken cancellationToken)
        => Ok(await _intelligence.GetOverviewAsync(cancellationToken));

    [HttpGet("executions")]
    [ProducesResponseType<IReadOnlyList<IntelligenceExecutionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<IntelligenceExecutionDto>>> ListExecutions(
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
        => Ok(await _intelligence.ListExecutionsAsync(limit, cancellationToken));

    [HttpPost("plan-preview")]
    [ProducesResponseType<ExecutionPlanPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExecutionPlanPreviewDto>> PreviewPlan(
        [FromBody] ExecutionPlanPreviewCommand command,
        CancellationToken cancellationToken)
        => Ok(await _intelligence.PreviewPlanAsync(command, cancellationToken));
}
