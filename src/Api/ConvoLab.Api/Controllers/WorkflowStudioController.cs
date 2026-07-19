using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.WorkflowStudio;
using Microsoft.AspNetCore.Mvc;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/workflows")]
public sealed class WorkflowStudioController(IWorkflowStudioService workflows) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkflowSummaryDto>>> List(CancellationToken ct)
        => Ok(await workflows.ListAsync(ct));

    [HttpGet("published")]
    public async Task<ActionResult<IReadOnlyList<RuntimeWorkflowTemplate>>> Published(CancellationToken ct)
        => Ok(await workflows.ListPublishedAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkflowDetailDto>> Get(Guid id, CancellationToken ct)
        => Ok(await workflows.GetAsync(id, ct)
            ?? throw new ResourceNotFoundException("workflow.not_found", $"Workflow '{id}' was not found."));

    [HttpPost]
    public async Task<ActionResult<WorkflowDetailDto>> Create(CreateWorkflowCommand command, CancellationToken ct)
    {
        var item = await workflows.CreateAsync(command, ct);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<WorkflowDetailDto>> Update(Guid id, UpdateWorkflowCommand command, CancellationToken ct)
        => Ok(await workflows.UpdateAsync(id, command, ct)
            ?? throw new ResourceNotFoundException("workflow.not_found", $"Workflow '{id}' was not found."));

    [HttpPost("{id:guid}/versions")]
    public async Task<ActionResult<WorkflowVersionDto>> CreateVersion(Guid id, CreateWorkflowVersionCommand command, CancellationToken ct)
        => Ok(await workflows.CreateVersionAsync(id, command, ct));

    [HttpPut("versions/{versionId:guid}/graph")]
    public async Task<ActionResult<WorkflowVersionDto>> UpdateGraph(Guid versionId, UpdateWorkflowGraphCommand command, CancellationToken ct)
        => Ok(await workflows.UpdateGraphAsync(versionId, command, ct)
            ?? throw new ResourceNotFoundException("workflow.version.not_found", $"Workflow version '{versionId}' was not found."));

    [HttpGet("versions/{versionId:guid}/validate")]
    public async Task<ActionResult<WorkflowVersionDto>> Validate(Guid versionId, CancellationToken ct)
        => Ok(await workflows.ValidateAsync(versionId, ct)
            ?? throw new ResourceNotFoundException("workflow.version.not_found", $"Workflow version '{versionId}' was not found."));

    [HttpPost("versions/{versionId:guid}/{lifecycleAction:regex(^(submit|approve|reject|publish|deprecate|archive|restore)$)}")]
    public async Task<ActionResult<WorkflowVersionDto>> Transition(Guid versionId, string lifecycleAction, WorkflowLifecycleCommand command, CancellationToken ct)
        => Ok(await workflows.TransitionAsync(versionId, lifecycleAction, command, ct)
            ?? throw new ResourceNotFoundException("workflow.version.not_found", $"Workflow version '{versionId}' was not found."));
}
