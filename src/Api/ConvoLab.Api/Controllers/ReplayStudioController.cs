using ConvoLab.Application.ReplayStudio;
using Microsoft.AspNetCore.Mvc;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/replay")]
public sealed class ReplayStudioController(IReplayStudioService replay) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType<ReplayOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReplayOverviewDto>> GetOverview(CancellationToken cancellationToken)
        => Ok(await replay.GetOverviewAsync(cancellationToken));

    [HttpGet("sources")]
    [ProducesResponseType<IReadOnlyList<ReplaySourceDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReplaySourceDto>>> ListSources(CancellationToken cancellationToken)
        => Ok(await replay.ListSourcesAsync(cancellationToken));

    [HttpGet("experiments")]
    [ProducesResponseType<IReadOnlyList<ReplayExperimentSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReplayExperimentSummaryDto>>> ListExperiments(CancellationToken cancellationToken)
        => Ok(await replay.ListExperimentsAsync(cancellationToken));

    [HttpGet("experiments/{experimentId:guid}")]
    [ProducesResponseType<ReplayExperimentDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReplayExperimentDetailDto>> GetExperiment(Guid experimentId, CancellationToken cancellationToken)
        => Ok(await replay.GetExperimentAsync(experimentId, cancellationToken));

    [HttpPost("experiments")]
    [ProducesResponseType<ReplayExperimentDetailDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ReplayExperimentDetailDto>> CreateExperiment(
        [FromBody] CreateReplayExperimentCommand command,
        CancellationToken cancellationToken)
    {
        var created = await replay.CreateExperimentAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetExperiment), new { experimentId = created.Summary.Id }, created);
    }

    [HttpPost("experiments/{experimentId:guid}/candidates")]
    [ProducesResponseType<ReplayExperimentDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReplayExperimentDetailDto>> AddCandidate(
        Guid experimentId,
        [FromBody] AddReplayCandidateCommand command,
        CancellationToken cancellationToken)
        => Ok(await replay.AddCandidateAsync(experimentId, command, cancellationToken));

    [HttpPost("experiments/{experimentId:guid}/complete")]
    [ProducesResponseType<ReplayExperimentDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReplayExperimentDetailDto>> Complete(Guid experimentId, CancellationToken cancellationToken)
        => Ok(await replay.CompleteAsync(experimentId, cancellationToken));

    [HttpPost("experiments/{experimentId:guid}/archive")]
    [ProducesResponseType<ReplayExperimentDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReplayExperimentDetailDto>> Archive(Guid experimentId, CancellationToken cancellationToken)
        => Ok(await replay.ArchiveAsync(experimentId, cancellationToken));
}
