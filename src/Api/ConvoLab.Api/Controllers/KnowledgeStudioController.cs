using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.KnowledgeStudio;
using ConvoLab.Domain.Knowledge.Enums;
using Microsoft.AspNetCore.Mvc;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/knowledge")]
public sealed class KnowledgeStudioController(IKnowledgeStudioService service) : ControllerBase
{
    [HttpGet("collections")]
    public Task<IReadOnlyList<KnowledgeCollectionDto>> List(CancellationToken ct)
        => service.ListCollectionsAsync(ct);

    [HttpPost("collections")]
    public async Task<ActionResult<KnowledgeCollectionDto>> Create(
        CreateKnowledgeCollectionCommand command,
        CancellationToken ct)
    {
        var result = await service.CreateCollectionAsync(command, ct);
        return CreatedAtAction(nameof(GetCollection), new { id = result.Id }, result);
    }

    [HttpGet("collections/{id:guid}")]
    public async Task<ActionResult<KnowledgeCollectionDto>> GetCollection(Guid id, CancellationToken ct)
        => Ok(await service.GetCollectionAsync(id, ct)
            ?? throw new ResourceNotFoundException(
                "knowledge.collection.not_found",
                $"Knowledge collection '{id}' was not found."));

    [HttpPatch("collections/{id:guid}")]
    public async Task<ActionResult<KnowledgeCollectionDto>> Update(
        Guid id,
        UpdateKnowledgeCollectionCommand command,
        CancellationToken ct)
        => Ok(await service.UpdateCollectionAsync(id, command, ct)
            ?? throw new ResourceNotFoundException(
                "knowledge.collection.not_found",
                $"Knowledge collection '{id}' was not found."));

    [HttpPost("collections/{id:guid}/archive")]
    public async Task<ActionResult<KnowledgeCollectionDto>> Archive(Guid id, CancellationToken ct)
        => Ok(await service.ArchiveCollectionAsync(id, ct)
            ?? throw new ResourceNotFoundException(
                "knowledge.collection.not_found",
                $"Knowledge collection '{id}' was not found."));

    [HttpPost("collections/{id:guid}/restore")]
    public async Task<ActionResult<KnowledgeCollectionDto>> Restore(Guid id, CancellationToken ct)
        => Ok(await service.RestoreCollectionAsync(id, ct)
            ?? throw new ResourceNotFoundException(
                "knowledge.collection.not_found",
                $"Knowledge collection '{id}' was not found."));

    [HttpGet("collections/{id:guid}/documents")]
    public Task<IReadOnlyList<KnowledgeDocumentDto>> Documents(Guid id, CancellationToken ct)
        => service.ListDocumentsAsync(id, ct);

    [HttpPost("collections/{id:guid}/documents")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<KnowledgeDocumentDto>> Upload(
        Guid id,
        IFormFile file,
        [FromForm] string owner = "Unassigned",
        [FromForm] KnowledgeClassification classification = KnowledgeClassification.Internal,
        CancellationToken ct = default)
    {
        if (file.Length == 0)
            throw new RequestValidationException(
                "knowledge.upload.empty",
                "A non-empty file is required.",
                new Dictionary<string, string[]> { ["file"] = ["Choose a non-empty file."] });

        await using var stream = file.OpenReadStream();
        var result = await service.UploadAsync(
            new KnowledgeUpload(id, file.FileName, file.ContentType, file.Length, stream, owner, classification),
            ct);
        return Created($"/api/knowledge/documents/{result.Id}", result);
    }

    [HttpGet("documents/{id:guid}")]
    public async Task<ActionResult<KnowledgeDocumentDto>> Document(Guid id, CancellationToken ct)
        => Ok(await service.GetDocumentAsync(id, ct)
            ?? throw new ResourceNotFoundException(
                "knowledge.document.not_found",
                $"Knowledge document '{id}' was not found."));

    [HttpPatch("documents/{id:guid}")]
    public async Task<ActionResult<KnowledgeDocumentDto>> UpdateDocument(
        Guid id,
        UpdateKnowledgeDocumentCommand command,
        CancellationToken ct)
        => Ok(await service.UpdateDocumentAsync(id, command, ct)
            ?? throw new ResourceNotFoundException(
                "knowledge.document.not_found",
                $"Knowledge document '{id}' was not found."));

    [HttpDelete("documents/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await service.DeleteDocumentAsync(id, ct))
            throw new ResourceNotFoundException(
                "knowledge.document.not_found",
                $"Knowledge document '{id}' was not found.");
        return NoContent();
    }

    [HttpPost("documents/{id:guid}/process")]
    public async Task<ActionResult<KnowledgeDocumentDto>> Process(Guid id, CancellationToken ct)
        => Ok(await service.ProcessAsync(id, ct)
            ?? throw new ResourceNotFoundException(
                "knowledge.document.not_found",
                $"Knowledge document '{id}' was not found."));

    [HttpPost("documents/{id:guid}/retry")]
    public async Task<ActionResult<KnowledgeDocumentDto>> Retry(Guid id, CancellationToken ct)
        => Ok(await service.RetryAsync(id, ct)
            ?? throw new ResourceNotFoundException(
                "knowledge.document.not_found",
                $"Knowledge document '{id}' was not found."));

    [HttpPost("documents/{id:guid}/{lifecycleAction:regex(^(submit|approve|reject|publish|deprecate|archive|restore)$)}")]
    public async Task<ActionResult<KnowledgeDocumentDto>> Transition(
        Guid id,
        string lifecycleAction,
        KnowledgeLifecycleCommand command,
        CancellationToken ct)
        => Ok(await service.TransitionAsync(id, lifecycleAction, command, ct)
            ?? throw new ResourceNotFoundException(
                "knowledge.document.not_found",
                $"Knowledge document '{id}' was not found."));

    [HttpGet("documents/{id:guid}/chunks")]
    public Task<IReadOnlyList<KnowledgeChunkDto>> Chunks(Guid id, CancellationToken ct)
        => service.GetChunksAsync(id, ct);

    [HttpPost("collections/{id:guid}/query")]
    public Task<KnowledgeQueryResponse> Query(Guid id, KnowledgeQueryCommand command, CancellationToken ct)
        => service.QueryAsync(id, command, ct);

    [HttpGet("collections/{id:guid}/health")]
    public async Task<ActionResult<KnowledgeCollectionHealthDto>> Health(Guid id, CancellationToken ct)
        => Ok(await service.GetHealthAsync(id, ct)
            ?? throw new ResourceNotFoundException(
                "knowledge.collection.not_found",
                $"Knowledge collection '{id}' was not found."));
}
