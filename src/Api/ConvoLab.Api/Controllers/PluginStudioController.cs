using ConvoLab.Application.PluginStudio;
using Microsoft.AspNetCore.Mvc;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/plugins")]
public sealed class PluginStudioController(IPluginStudioService plugins) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType<PluginOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PluginOverviewDto>> GetOverview(CancellationToken cancellationToken)
        => Ok(await plugins.GetOverviewAsync(cancellationToken));

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PluginSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PluginSummaryDto>>> List(CancellationToken cancellationToken)
        => Ok(await plugins.ListPluginsAsync(cancellationToken));

    [HttpGet("{pluginId:guid}")]
    [ProducesResponseType<PluginDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PluginDetailDto>> Get(Guid pluginId, CancellationToken cancellationToken)
        => Ok(await plugins.GetPluginAsync(pluginId, cancellationToken));

    [HttpPost]
    [ProducesResponseType<PluginDetailDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PluginDetailDto>> Register(
        [FromBody] RegisterPluginCommand command,
        CancellationToken cancellationToken)
    {
        var created = await plugins.RegisterAsync(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { pluginId = created.Summary.Id }, created);
    }

    [HttpPut("{pluginId:guid}")]
    [ProducesResponseType<PluginDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PluginDetailDto>> Update(
        Guid pluginId,
        [FromBody] UpdatePluginCommand command,
        CancellationToken cancellationToken)
        => Ok(await plugins.UpdateAsync(pluginId, command, cancellationToken));

    [HttpPost("{pluginId:guid}/versions")]
    [ProducesResponseType<PluginDetailDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PluginDetailDto>> UpdateVersion(
        Guid pluginId,
        [FromBody] UpdatePluginVersionCommand command,
        CancellationToken cancellationToken)
    {
        var created = await plugins.UpdateVersionAsync(pluginId, command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { pluginId = created.Summary.Id }, created);
    }

    [HttpPost("{pluginId:guid}/health")]
    [ProducesResponseType<PluginHealthCheckDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PluginHealthCheckDto>> CheckHealth(
        Guid pluginId,
        CancellationToken cancellationToken)
        => Ok(await plugins.CheckHealthAsync(pluginId, cancellationToken));

    [HttpPost("{pluginId:guid}/{lifecycleAction:regex(^(activate|deactivate|disable|deprecate)$)}")]
    [ProducesResponseType<PluginDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PluginDetailDto>> Transition(
        Guid pluginId,
        string lifecycleAction,
        CancellationToken cancellationToken)
        => Ok(await plugins.TransitionAsync(pluginId, lifecycleAction, cancellationToken));
}
