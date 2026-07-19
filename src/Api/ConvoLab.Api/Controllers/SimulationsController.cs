using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Simulation;
using Microsoft.AspNetCore.Mvc;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/simulations")]
public sealed class SimulationsController : ControllerBase
{
    private readonly IConversationSimulationService _simulations;

    public SimulationsController(IConversationSimulationService simulations)
    {
        _simulations = simulations;
    }

    [HttpGet("options")]
    [ProducesResponseType<SimulationOptions>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SimulationOptions>> GetOptions(CancellationToken cancellationToken)
        => Ok(await _simulations.GetOptionsAsync(cancellationToken));

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SimulationSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SimulationSummary>>> List(CancellationToken cancellationToken)
        => Ok(await _simulations.ListAsync(cancellationToken));

    [HttpGet("{simulationId:guid}")]
    [ProducesResponseType<SimulationConversation>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationConversation>> Get(
        Guid simulationId,
        CancellationToken cancellationToken)
    {
        var simulation = await _simulations.GetAsync(simulationId, cancellationToken);
        return Ok(simulation ?? throw new ResourceNotFoundException("simulation.not_found", $"Simulation '{simulationId}' was not found."));
    }

    [HttpPost]
    [ProducesResponseType<SimulationConversation>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SimulationConversation>> Create(
        [FromBody] CreateSimulationCommand command,
        CancellationToken cancellationToken)
    {
        var simulation = await _simulations.CreateAsync(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { simulationId = simulation.Id }, simulation);
    }

    [HttpPost("{simulationId:guid}/messages")]
    [ProducesResponseType<SimulationConversation>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationConversation>> SendMessage(
        Guid simulationId,
        [FromBody] SendSimulationMessageCommand command,
        CancellationToken cancellationToken)
    {
        var simulation = await _simulations.SendMessageAsync(simulationId, command, cancellationToken);
        return Ok(simulation ?? throw new ResourceNotFoundException("simulation.not_found", $"Simulation '{simulationId}' was not found."));
    }

    [HttpPost("{simulationId:guid}/replay")]
    [ProducesResponseType<SimulationConversation>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationConversation>> Replay(
        Guid simulationId,
        [FromBody] ReplaySimulationCommand command,
        CancellationToken cancellationToken)
    {
        var simulation = await _simulations.ReplayAsync(simulationId, command, cancellationToken);
        return Ok(simulation ?? throw new ResourceNotFoundException("simulation.not_found", $"Simulation '{simulationId}' was not found."));
    }
}
