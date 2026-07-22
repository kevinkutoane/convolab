using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Common.Persistence;
using ConvoLab.Domain.Knowledge.Enums;
using ConvoLab.Domain.Knowledge.Policies;

namespace ConvoLab.Application.KnowledgeStudio;

public sealed class KnowledgeStudioService(
    IKnowledgeStudioRepository repository,
    IKnowledgeDocumentStorage storage,
    IDocumentTextExtractorResolver extractorResolver,
    IKnowledgeChunker chunker,
    IKeywordKnowledgeRetriever retriever,
    IUnitOfWork unitOfWork) : IKnowledgeStudioService
{
    private static readonly IReadOnlyDictionary<string, string[]> AllowedContentTypes =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = ["application/pdf", "application/octet-stream"],
            [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/octet-stream"],
            [".txt"] = ["text/plain", "application/octet-stream"],
            [".md"] = ["text/markdown", "text/plain", "application/octet-stream"],
            [".markdown"] = ["text/markdown", "text/plain", "application/octet-stream"]
        };

    private const long MaxBytes = 20 * 1024 * 1024;

    public async Task<IReadOnlyList<KnowledgeCollectionDto>> ListCollectionsAsync(CancellationToken ct = default)
    {
        var collections = await repository.ListCollectionsAsync(ct);
        var result = new List<KnowledgeCollectionDto>(collections.Count);
        foreach (var collection in collections)
        {
            var documents = await repository.ListDocumentsAsync(collection.Id, ct);
            var chunks = await repository.ListCollectionChunksAsync(collection.Id, false, ct);
            result.Add(MapCollection(collection, documents.Count, chunks.Count));
        }
        return result.OrderByDescending(item => item.UpdatedAt).ToList();
    }

    public async Task<KnowledgeCollectionDto?> GetCollectionAsync(Guid id, CancellationToken ct = default)
    {
        var collection = await repository.GetCollectionAsync(id, ct);
        if (collection is null) return null;
        var documents = await repository.ListDocumentsAsync(id, ct);
        var chunks = await repository.ListCollectionChunksAsync(id, false, ct);
        return MapCollection(collection, documents.Count, chunks.Count);
    }

    public async Task<KnowledgeCollectionDto> CreateCollectionAsync(
        CreateKnowledgeCollectionCommand command,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            throw new RequestValidationException(
                "knowledge.collection.name_required",
                "Collection name is required.",
                new Dictionary<string, string[]> { ["name"] = ["Collection name is required."] });

        var now = DateTimeOffset.UtcNow;
        var collection = new KnowledgeCollectionState(
            Guid.NewGuid(),
            command.Name.Trim(),
            command.Description?.Trim() ?? string.Empty,
            string.IsNullOrWhiteSpace(command.Owner) ? "Unassigned" : command.Owner.Trim(),
            command.Classification,
            KnowledgeCollectionStatus.Active,
            now,
            now,
            1);

        await repository.AddCollectionAsync(collection, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return MapCollection(collection, 0, 0);
    }

    public async Task<KnowledgeCollectionDto?> UpdateCollectionAsync(
        Guid id,
        UpdateKnowledgeCollectionCommand command,
        CancellationToken ct = default)
    {
        var current = await repository.GetCollectionAsync(id, ct);
        if (current is null) return null;
        var expectedRevision = command.ExpectedRevision ?? current.Revision;
        var updated = current with
        {
            Name = string.IsNullOrWhiteSpace(command.Name) ? current.Name : command.Name.Trim(),
            Description = command.Description is null ? current.Description : command.Description.Trim(),
            Owner = string.IsNullOrWhiteSpace(command.Owner) ? current.Owner : command.Owner.Trim(),
            Classification = command.Classification ?? current.Classification,
            UpdatedAt = DateTimeOffset.UtcNow,
            Revision = current.Revision + 1
        };
        await repository.UpdateCollectionAsync(updated, expectedRevision, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return await GetCollectionAsync(id, ct);
    }

    public Task<KnowledgeCollectionDto?> ArchiveCollectionAsync(Guid id, CancellationToken ct = default)
        => SetCollectionStatusAsync(id, KnowledgeCollectionStatus.Archived, ct);

    public Task<KnowledgeCollectionDto?> RestoreCollectionAsync(Guid id, CancellationToken ct = default)
        => SetCollectionStatusAsync(id, KnowledgeCollectionStatus.Active, ct);

    public async Task<IReadOnlyList<KnowledgeDocumentDto>> ListDocumentsAsync(Guid collectionId, CancellationToken ct = default)
        => (await repository.ListDocumentsAsync(collectionId, ct)).Select(item => MapDocument(item)!).ToList();

    public async Task<KnowledgeDocumentDto?> GetDocumentAsync(Guid id, CancellationToken ct = default)
        => MapDocument(await repository.GetDocumentAsync(id, ct));

    public async Task<KnowledgeDocumentDto> UploadAsync(KnowledgeUpload upload, CancellationToken ct = default)
    {
        var collection = await repository.GetCollectionAsync(upload.CollectionId, ct)
            ?? throw new ResourceNotFoundException(
                "knowledge.collection.not_found",
                $"Knowledge collection '{upload.CollectionId}' was not found.");
        if (collection.Status == KnowledgeCollectionStatus.Archived)
            throw new DomainRuleViolationException(
                "knowledge.collection.archived",
                "Documents cannot be uploaded into an archived collection.");

        var extension = Path.GetExtension(upload.FileName).ToLowerInvariant();
        ValidateUploadMetadata(extension, upload.ContentType, upload.Length);

        await using var validatedContent = new MemoryStream();
        await upload.Content.CopyToAsync(validatedContent, ct);
        validatedContent.Position = 0;
        ValidateFileSignature(extension, validatedContent);
        validatedContent.Position = 0;

        var stored = await storage.StoreAsync(upload.FileName, upload.ContentType, validatedContent, ct);
        var now = DateTimeOffset.UtcNow;
        var document = new KnowledgeDocumentState(
            Guid.NewGuid(),
            upload.CollectionId,
            Path.GetFileNameWithoutExtension(stored.SafeFileName),
            stored.SafeFileName,
            stored.ContentType,
            stored.SizeBytes,
            stored.StorageKey,
            KnowledgeDocumentStage.Uploaded,
            upload.Classification,
            string.IsNullOrWhiteSpace(upload.Owner) ? collection.Owner : upload.Owner.Trim(),
            "General",
            [],
            1,
            null,
            now,
            now,
            null,
            1);

        await repository.AddDocumentAsync(document, ct);
        await repository.AddLifecycleEntryAsync(new KnowledgeLifecycleState(
            Guid.NewGuid(),
            document.Id,
            "system",
            "uploaded",
            null,
            KnowledgeDocumentStage.Uploaded,
            KnowledgeDocumentStage.Uploaded,
            now), ct);
        await unitOfWork.SaveChangesAsync(ct);
        return MapDocument(document)!;
    }

    public async Task<KnowledgeDocumentDto?> UpdateDocumentAsync(
        Guid id,
        UpdateKnowledgeDocumentCommand command,
        CancellationToken ct = default)
    {
        var current = await repository.GetDocumentAsync(id, ct);
        if (current is null) return null;
        try
        {
            KnowledgeDocumentStagePolicy.EnsureMutable(current.Stage);
        }
        catch (InvalidOperationException exception)
        {
            throw new DomainRuleViolationException("knowledge.document.immutable", exception.Message);
        }

        var expectedRevision = command.ExpectedRevision ?? current.Revision;
        var updated = current with
        {
            Title = string.IsNullOrWhiteSpace(command.Title) ? current.Title : command.Title.Trim(),
            Owner = string.IsNullOrWhiteSpace(command.Owner) ? current.Owner : command.Owner.Trim(),
            Category = command.Category is null ? current.Category : command.Category.Trim(),
            Tags = command.Tags is null ? current.Tags : NormalizeTags(command.Tags),
            Classification = command.Classification ?? current.Classification,
            UpdatedAt = DateTimeOffset.UtcNow,
            Revision = current.Revision + 1
        };
        await repository.UpdateDocumentAsync(updated, expectedRevision, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return MapDocument(updated);
    }

    public Task<KnowledgeDocumentDto?> RetryAsync(Guid id, CancellationToken ct = default)
        => ProcessAsync(id, ct);

    public async Task<KnowledgeDocumentDto?> ProcessAsync(Guid id, CancellationToken ct = default)
    {
        var current = await repository.GetDocumentAsync(id, ct);
        if (current is null) return null;
        if (current.Stage == KnowledgeDocumentStage.Published)
            throw new DomainRuleViolationException(
                "knowledge.document.published_reprocess",
                "Published documents cannot be reprocessed. Create a new version.");
        if (!await storage.ExistsAsync(current.StorageKey, ct))
            throw new CapabilityUnavailableException(
                "knowledge.storage.missing_document",
                "The uploaded source file is not available in document storage.");

        var extracting = current with
        {
            Stage = KnowledgeDocumentStage.Extracting,
            Error = null,
            UpdatedAt = DateTimeOffset.UtcNow,
            Revision = current.Revision + 1
        };
        await repository.UpdateDocumentAsync(extracting, current.Revision, ct);
        await unitOfWork.SaveChangesAsync(ct);

        try
        {
            await using var stream = await storage.OpenAsync(extracting.StorageKey, ct);
            var extension = Path.GetExtension(extracting.OriginalFileName).ToLowerInvariant();
            var extractor = extractorResolver.Resolve(extension, extracting.ContentType);
            var extracted = await extractor.ExtractAsync(stream, extracting.OriginalFileName, ct);

            var chunking = extracting with
            {
                Stage = KnowledgeDocumentStage.Chunking,
                UpdatedAt = DateTimeOffset.UtcNow,
                Revision = extracting.Revision + 1
            };
            await repository.UpdateDocumentAsync(chunking, extracting.Revision, ct);
            await unitOfWork.SaveChangesAsync(ct);

            var chunks = chunker.Chunk(extracted, chunking);
            if (chunks.Count == 0)
                throw new InvalidDataException("The document did not produce any usable text chunks.");
            await repository.ReplaceChunksAsync(id, chunks, ct);

            var processed = chunking with
            {
                Title = string.IsNullOrWhiteSpace(chunking.Title) ? extracted.Title : chunking.Title,
                Stage = KnowledgeDocumentStage.Processed,
                Error = extracted.Warnings.Count == 0 ? null : string.Join(" ", extracted.Warnings),
                UpdatedAt = DateTimeOffset.UtcNow,
                Revision = chunking.Revision + 1
            };
            await repository.UpdateDocumentAsync(processed, chunking.Revision, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return MapDocument(processed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var latest = await repository.GetDocumentAsync(id, ct) ?? extracting;
            var failed = latest with
            {
                Stage = KnowledgeDocumentStage.Failed,
                Error = "Document processing failed. Inspect server logs using the correlation id.",
                UpdatedAt = DateTimeOffset.UtcNow,
                Revision = latest.Revision + 1
            };
            await repository.UpdateDocumentAsync(failed, latest.Revision, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return MapDocument(failed);
        }
    }

    public async Task<KnowledgeDocumentDto?> TransitionAsync(
        Guid id,
        string action,
        KnowledgeLifecycleCommand command,
        CancellationToken ct = default)
    {
        var current = await repository.GetDocumentAsync(id, ct);
        if (current is null) return null;
        var parsedAction = ParseAction(action);
        KnowledgeDocumentStage next;
        try
        {
            next = KnowledgeDocumentStagePolicy.Transition(
                current.Stage,
                parsedAction,
                current.Classification >= KnowledgeClassification.Confidential);
        }
        catch (InvalidOperationException exception)
        {
            throw new DomainRuleViolationException("knowledge.lifecycle.invalid_transition", exception.Message);
        }

        var expectedRevision = command.ExpectedRevision ?? current.Revision;
        var now = DateTimeOffset.UtcNow;
        var updated = current with
        {
            Stage = next,
            PublishedAt = next == KnowledgeDocumentStage.Published ? now : current.PublishedAt,
            UpdatedAt = now,
            Revision = current.Revision + 1
        };
        await repository.UpdateDocumentAsync(updated, expectedRevision, ct);

        if (next == KnowledgeDocumentStage.Published)
            await repository.SetChunksPublishedAsync(id, true, ct);
        if (next is KnowledgeDocumentStage.Deprecated or KnowledgeDocumentStage.Archived)
            await repository.SetChunksPublishedAsync(id, false, ct);

        await repository.AddLifecycleEntryAsync(new KnowledgeLifecycleState(
            Guid.NewGuid(),
            id,
            string.IsNullOrWhiteSpace(command.Actor) ? "system" : command.Actor.Trim(),
            action.Trim().ToLowerInvariant(),
            command.Reason,
            current.Stage,
            next,
            now), ct);
        await unitOfWork.SaveChangesAsync(ct);
        return MapDocument(updated);
    }

    public async Task<IReadOnlyList<KnowledgeChunkDto>> GetChunksAsync(Guid documentId, CancellationToken ct = default)
        => (await repository.ListChunksAsync(documentId, ct)).Select(MapChunk).ToList();

    public async Task<KnowledgeQueryResponse> QueryAsync(
        Guid collectionId,
        KnowledgeQueryCommand command,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Query))
            throw new RequestValidationException(
                "knowledge.query.required",
                "A retrieval query is required.",
                new Dictionary<string, string[]> { ["query"] = ["Enter a question or search phrase."] });

        var collection = await repository.GetCollectionAsync(collectionId, ct);
        if (collection is null)
            throw new ResourceNotFoundException(
                "knowledge.collection.not_found",
                $"Knowledge collection '{collectionId}' was not found.");
        if (collection.Status == KnowledgeCollectionStatus.Archived)
            throw new DomainRuleViolationException(
                "knowledge.collection.archived",
                "Archived collections are not eligible for retrieval.");

        var documents = (await repository.ListDocumentsAsync(collectionId, ct))
            .Where(document => KnowledgeDocumentStagePolicy.IsRetrievable(document.Stage))
            .ToDictionary(document => document.Id, document => document.Title);
        var chunks = await repository.ListCollectionChunksAsync(collectionId, true, ct);
        var ranked = retriever.Rank(
            command.Query,
            documents,
            chunks,
            Math.Clamp(command.MaxResults, 1, 20),
            Math.Clamp(command.MinimumConfidence, 0, 1));

        var results = new List<KnowledgeSearchResultDto>();
        var budget = 0;
        foreach (var candidate in ranked)
        {
            if (budget + candidate.Chunk.EstimatedTokens > Math.Max(1, command.TokenBudget)) continue;
            budget += candidate.Chunk.EstimatedTokens;
            results.Add(new KnowledgeSearchResultDto(
                candidate.Chunk.Id,
                candidate.Chunk.DocumentId,
                candidate.DocumentTitle,
                results.Count + 1,
                candidate.Confidence,
                candidate.Chunk.Text,
                candidate.Chunk.PageNumber,
                candidate.Chunk.Section,
                candidate.MatchingTerms,
                candidate.Chunk.EstimatedTokens));
        }

        var exclusions = new List<string>();
        if (chunks.Count == 0) exclusions.Add("No eligible published chunks were available.");
        if (ranked.Count > results.Count) exclusions.Add("Some ranked chunks were excluded by the token budget.");
        if (results.Count == 0 && chunks.Count > 0) exclusions.Add("No chunks met the minimum confidence threshold.");

        return new KnowledgeQueryResponse(
            collectionId,
            command.Query,
            budget,
            results,
            exclusions,
            DateTimeOffset.UtcNow);
    }

    public async Task<KnowledgeCollectionHealthDto?> GetHealthAsync(Guid collectionId, CancellationToken ct = default)
    {
        if (await repository.GetCollectionAsync(collectionId, ct) is null) return null;
        var documents = await repository.ListDocumentsAsync(collectionId, ct);
        var chunks = await repository.ListCollectionChunksAsync(collectionId, false, ct);
        var failed = documents.Count(document => document.Stage == KnowledgeDocumentStage.Failed);
        return new KnowledgeCollectionHealthDto(
            collectionId,
            documents.Count,
            documents.Count(document => document.Stage == KnowledgeDocumentStage.Published),
            failed,
            chunks.Count,
            chunks.Count(chunk => chunk.Published),
            failed > 0 ? "Degraded" : "Healthy");
    }

    public async Task<bool> DeleteDocumentAsync(Guid id, CancellationToken ct = default)
    {
        var current = await repository.GetDocumentAsync(id, ct);
        if (current is null) return false;
        if (current.Stage == KnowledgeDocumentStage.Published)
            throw new DomainRuleViolationException(
                "knowledge.document.published_delete",
                "Published documents must be deprecated and archived before deletion.");

        await storage.DeleteAsync(current.StorageKey, ct);
        await repository.DeleteChunksAsync(id, ct);
        await repository.DeleteLifecycleAsync(id, ct);
        await repository.DeleteDocumentAsync(id, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task<KnowledgeCollectionDto?> SetCollectionStatusAsync(
        Guid id,
        KnowledgeCollectionStatus status,
        CancellationToken ct)
    {
        var current = await repository.GetCollectionAsync(id, ct);
        if (current is null) return null;
        var updated = current with
        {
            Status = status,
            UpdatedAt = DateTimeOffset.UtcNow,
            Revision = current.Revision + 1
        };
        await repository.UpdateCollectionAsync(updated, current.Revision, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return await GetCollectionAsync(id, ct);
    }

    private static void ValidateUploadMetadata(string extension, string contentType, long length)
    {
        if (!AllowedContentTypes.TryGetValue(extension, out var allowed))
            throw new RequestValidationException(
                "knowledge.upload.extension_unsupported",
                "Supported file types are PDF, DOCX, TXT and Markdown.",
                new Dictionary<string, string[]> { ["file"] = ["Choose a PDF, DOCX, TXT or Markdown file."] });
        if (length <= 0 || length > MaxBytes)
            throw new RequestValidationException(
                "knowledge.upload.size_invalid",
                "File size must be between 1 byte and 20 MB.",
                new Dictionary<string, string[]> { ["file"] = ["The maximum file size is 20 MB."] });
        if (!allowed.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            throw new RequestValidationException(
                "knowledge.upload.mime_invalid",
                $"Content type '{contentType}' does not match the selected file type.",
                new Dictionary<string, string[]> { ["file"] = ["The file content type is not allowed."] });
    }

    private static void ValidateFileSignature(string extension, Stream stream)
    {
        Span<byte> header = stackalloc byte[8];
        var read = stream.Read(header);
        stream.Position = 0;
        var valid = extension switch
        {
            ".pdf" => read >= 5 && header[..5].SequenceEqual("%PDF-"u8),
            ".docx" => read >= 4 && header[0] == (byte)'P' && header[1] == (byte)'K',
            ".txt" or ".md" or ".markdown" => !header[..read].Contains((byte)0),
            _ => false
        };
        if (!valid)
            throw new RequestValidationException(
                "knowledge.upload.signature_invalid",
                "The uploaded file content does not match its extension.",
                new Dictionary<string, string[]> { ["file"] = ["The file appears malformed or has an incorrect extension."] });
    }

    private static KnowledgeDocumentAction ParseAction(string action)
        => action.Trim().ToLowerInvariant() switch
        {
            "submit" => KnowledgeDocumentAction.Submit,
            "approve" => KnowledgeDocumentAction.Approve,
            "reject" => KnowledgeDocumentAction.Reject,
            "publish" => KnowledgeDocumentAction.Publish,
            "deprecate" => KnowledgeDocumentAction.Deprecate,
            "archive" => KnowledgeDocumentAction.Archive,
            "restore" => KnowledgeDocumentAction.Restore,
            _ => throw new RequestValidationException(
                "knowledge.lifecycle.action_invalid",
                $"Unknown document lifecycle action '{action}'.")
        };

    private static IReadOnlyList<string> NormalizeTags(IEnumerable<string> tags)
        => tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static KnowledgeCollectionDto MapCollection(
        KnowledgeCollectionState state,
        int documentCount,
        int chunkCount)
        => new(
            state.Id,
            state.Name,
            state.Description,
            state.Owner,
            state.Classification,
            state.Status,
            documentCount,
            chunkCount,
            state.CreatedAt,
            state.UpdatedAt,
            state.Revision);

    private static KnowledgeDocumentDto? MapDocument(KnowledgeDocumentState? state)
        => state is null
            ? null
            : new KnowledgeDocumentDto(
                state.Id,
                state.CollectionId,
                state.Title,
                state.OriginalFileName,
                state.ContentType,
                state.SizeBytes,
                state.Stage,
                state.Classification,
                state.Owner,
                state.Category,
                state.Tags,
                state.Version,
                state.Error,
                state.CreatedAt,
                state.UpdatedAt,
                state.PublishedAt,
                state.Revision);

    private static KnowledgeChunkDto MapChunk(KnowledgeChunkState state)
        => new(
            state.Id,
            state.DocumentId,
            state.CollectionId,
            state.Sequence,
            state.Text,
            state.PageNumber,
            state.Section,
            state.CharacterCount,
            state.EstimatedTokens,
            state.Classification,
            state.Published);

}
