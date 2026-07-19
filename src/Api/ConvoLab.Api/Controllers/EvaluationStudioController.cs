using ConvoLab.Application.EvaluationStudio;
using Microsoft.AspNetCore.Mvc;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/evaluation")]
public sealed class EvaluationStudioController : ControllerBase
{
    private readonly IEvaluationStudioService _evaluation;

    public EvaluationStudioController(IEvaluationStudioService evaluation)
    {
        _evaluation = evaluation;
    }

    [HttpGet("overview")]
    [ProducesResponseType<EvaluationOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EvaluationOverviewDto>> GetOverview(CancellationToken cancellationToken)
        => Ok(await _evaluation.GetOverviewAsync(cancellationToken));

    [HttpGet("runs")]
    [ProducesResponseType<IReadOnlyList<EvaluationRunDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EvaluationRunDto>>> ListRuns(
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
        => Ok(await _evaluation.ListRunsAsync(limit, cancellationToken));

    [HttpGet("scorecards")]
    [ProducesResponseType<IReadOnlyList<EvaluationScorecardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EvaluationScorecardDto>>> ListScorecards(
        CancellationToken cancellationToken)
        => Ok(await _evaluation.ListScorecardsAsync(cancellationToken));

    [HttpPost("scorecards")]
    [ProducesResponseType<EvaluationScorecardDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EvaluationScorecardDto>> CreateScorecard(
        [FromBody] CreateEvaluationScorecardCommand command,
        CancellationToken cancellationToken)
    {
        var created = await _evaluation.CreateScorecardAsync(command, cancellationToken);
        return Created($"/api/evaluation/scorecards/{created.Id}", created);
    }

    [HttpPost("preview")]
    [ProducesResponseType<EvaluationPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EvaluationPreviewDto>> Preview(
        [FromBody] EvaluationPreviewCommand command,
        CancellationToken cancellationToken)
        => Ok(await _evaluation.PreviewAsync(command, cancellationToken));
}
