using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.PromptStudio;
using Microsoft.AspNetCore.Mvc;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/prompts")]
public sealed class PromptStudioController(IPromptStudioService prompts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PromptSummaryDto>>> List(CancellationToken ct)
        => Ok(await prompts.ListAsync(ct));

    [HttpGet("published")]
    public async Task<ActionResult<IReadOnlyList<RuntimePromptTemplate>>> Published(CancellationToken ct)
        => Ok(await prompts.ListPublishedAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PromptDetailDto>> Get(Guid id, CancellationToken ct)
        => Ok(await prompts.GetAsync(id, ct)
            ?? throw new ResourceNotFoundException("prompt.not_found", $"Prompt '{id}' was not found."));

    [HttpPost]
    public async Task<ActionResult<PromptDetailDto>> Create(CreatePromptCommand command, CancellationToken ct)
    {
        var item = await prompts.CreateAsync(command, ct);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<PromptDetailDto>> Update(Guid id, UpdatePromptCommand command, CancellationToken ct)
        => Ok(await prompts.UpdateAsync(id, command, ct)
            ?? throw new ResourceNotFoundException("prompt.not_found", $"Prompt '{id}' was not found."));

    [HttpPost("{id:guid}/versions")]
    public async Task<ActionResult<PromptVersionDto>> CreateVersion(
        Guid id,
        CreatePromptVersionCommand command,
        CancellationToken ct)
        => Ok(await prompts.CreateVersionAsync(id, command, ct));

    [HttpPost("versions/{versionId:guid}/{lifecycleAction:regex(^(submit|approve|reject|publish|deprecate|archive|restore)$)}")]
    public async Task<ActionResult<PromptVersionDto>> Transition(
        Guid versionId,
        string lifecycleAction,
        PromptLifecycleCommand command,
        CancellationToken ct)
        => Ok(await prompts.TransitionAsync(versionId, lifecycleAction, command, ct)
            ?? throw new ResourceNotFoundException(
                "prompt.version.not_found",
                $"Prompt version '{versionId}' was not found."));

    [HttpPost("render")]
    public async Task<ActionResult<RenderedPromptDto>> Render(RenderPromptCommand command, CancellationToken ct)
        => Ok(await prompts.RenderAsync(command, ct));

    [HttpGet("compare")]
    public async Task<ActionResult<PromptComparisonDto>> Compare(
        [FromQuery] Guid left,
        [FromQuery] Guid right,
        CancellationToken ct)
        => Ok(await prompts.CompareAsync(left, right, ct));
}
