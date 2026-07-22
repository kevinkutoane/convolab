using ConvoLab.Application.EvaluationStudio;
using Microsoft.AspNetCore.Mvc;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/evaluation")]
public sealed class LegacyEvaluationController : ControllerBase
{
    private readonly ILegacyEvaluationStudioService _evaluation;

    public LegacyEvaluationController(ILegacyEvaluationStudioService evaluation)
    {
        _evaluation = evaluation;
    }

    [HttpGet("overview")]
    [ProducesResponseType<LegacyEvaluationOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<LegacyEvaluationOverviewDto>> GetOverview(CancellationToken cancellationToken)
        => Ok(await _evaluation.GetOverviewAsync(cancellationToken));

    [HttpGet("runs")]
    [ProducesResponseType<IReadOnlyList<LegacyEvaluationRunDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LegacyEvaluationRunDto>>> ListRuns(
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
        => Ok(await _evaluation.ListRunsAsync(limit, cancellationToken));

    [HttpGet("scorecards")]
    [ProducesResponseType<IReadOnlyList<LegacyEvaluationScorecardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LegacyEvaluationScorecardDto>>> ListScorecards(
        CancellationToken cancellationToken)
        => Ok(await _evaluation.ListScorecardsAsync(cancellationToken));

    [HttpPost("scorecards")]
    [ProducesResponseType<LegacyEvaluationScorecardDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LegacyEvaluationScorecardDto>> CreateScorecard(
        [FromBody] CreateLegacyEvaluationScorecardCommand command,
        CancellationToken cancellationToken)
    {
        var created = await _evaluation.CreateScorecardAsync(command, cancellationToken);
        return Created($"/api/evaluation/scorecards/{created.Id}", created);
    }

    [HttpPost("preview")]
    [ProducesResponseType<LegacyEvaluationPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LegacyEvaluationPreviewDto>> Preview(
        [FromBody] LegacyEvaluationPreviewCommand command,
        CancellationToken cancellationToken)
        => Ok(await _evaluation.PreviewAsync(command, cancellationToken));
}
