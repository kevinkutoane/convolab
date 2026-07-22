using ConvoLab.Application.EvaluationStudio;
using Microsoft.AspNetCore.Mvc;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/evaluations")]
public sealed class ExpandedEvaluationStudioController(IEvaluationStudioService evaluations) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType<EvaluationOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EvaluationOverviewDto>> GetOverview(CancellationToken cancellationToken)
        => Ok(await evaluations.GetOverviewAsync(cancellationToken));

    [HttpGet("scorecards")]
    [ProducesResponseType<IReadOnlyList<EvaluationScorecardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EvaluationScorecardDto>>> ListScorecards(CancellationToken cancellationToken)
        => Ok(await evaluations.ListScorecardsAsync(cancellationToken));

    [HttpGet("scorecards/{id:guid}")]
    [ProducesResponseType<EvaluationScorecardDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EvaluationScorecardDto>> GetScorecard(Guid id, CancellationToken cancellationToken)
        => Ok(await evaluations.GetScorecardAsync(id, cancellationToken));

    [HttpPost("scorecards")]
    [ProducesResponseType<EvaluationScorecardDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EvaluationScorecardDto>> CreateScorecard(
        [FromBody] CreateEvaluationScorecardCommand command,
        CancellationToken cancellationToken)
    {
        var scorecard = await evaluations.CreateScorecardAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetScorecard), new { id = scorecard.Id }, scorecard);
    }

    [HttpPost("scorecards/{id:guid}/publish")]
    [ProducesResponseType<EvaluationScorecardDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EvaluationScorecardDto>> PublishScorecard(
        Guid id,
        [FromQuery] long revision,
        CancellationToken cancellationToken)
        => Ok(await evaluations.PublishScorecardAsync(id, revision, cancellationToken));

    [HttpGet("runs")]
    [ProducesResponseType<IReadOnlyList<EvaluationRunDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EvaluationRunDto>>> ListRuns(
        [FromQuery] int limit = 250,
        CancellationToken cancellationToken = default)
        => Ok(await evaluations.ListRunsAsync(limit, cancellationToken));

    [HttpPost("runs/evaluate")]
    [ProducesResponseType<EvaluationRunDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EvaluationRunDto>> EvaluateRun(
        [FromBody] EvaluateSimulationRunCommand command,
        CancellationToken cancellationToken)
        => Ok(await evaluations.EvaluateRunAsync(command, cancellationToken));

    [HttpPost("runs/{id:guid}/review")]
    [ProducesResponseType<EvaluationRunDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EvaluationRunDto>> ReviewRun(
        Guid id,
        [FromBody] ReviewEvaluationRunCommand command,
        CancellationToken cancellationToken)
        => Ok(await evaluations.ReviewRunAsync(id, command, cancellationToken));

    [HttpGet("compare")]
    [ProducesResponseType<EvaluationComparisonDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EvaluationComparisonDto>> CompareRuns(
        [FromQuery] Guid baselineId,
        [FromQuery] Guid candidateId,
        CancellationToken cancellationToken)
        => Ok(await evaluations.CompareRunsAsync(baselineId, candidateId, cancellationToken));

    [HttpGet("test-cases")]
    [ProducesResponseType<IReadOnlyList<EvaluationTestCaseDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EvaluationTestCaseDto>>> ListTestCases(CancellationToken cancellationToken)
        => Ok(await evaluations.ListTestCasesAsync(cancellationToken));

    [HttpPost("test-cases")]
    [ProducesResponseType<EvaluationTestCaseDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EvaluationTestCaseDto>> CreateTestCase(
        [FromBody] CreateEvaluationTestCaseCommand command,
        CancellationToken cancellationToken)
    {
        var testCase = await evaluations.CreateTestCaseAsync(command, cancellationToken);
        return Created($"/api/evaluations/test-cases/{testCase.Id}", testCase);
    }

    [HttpPost("batches")]
    [ProducesResponseType<EvaluationBatchDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EvaluationBatchDto>> RunBatch(
        [FromBody] RunEvaluationBatchCommand command,
        CancellationToken cancellationToken)
        => Ok(await evaluations.RunBatchAsync(command, cancellationToken));
}
