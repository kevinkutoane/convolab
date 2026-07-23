using System.Text.Json;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.KnowledgeStudio;
using ConvoLab.Domain.Knowledge.Enums;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.KnowledgeStudio;

public sealed class EfKnowledgeStudioRepository(ApplicationDbContext db) : IKnowledgeStudioRepository
{
    public async Task<IReadOnlyList<KnowledgeCollectionState>> ListCollectionsAsync(CancellationToken ct = default)
        => (await db.KnowledgeCollections.AsNoTracking().ToListAsync(ct))
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => MapCollection(item)!)
            .ToList();

    public async Task<KnowledgeCollectionState?> GetCollectionAsync(Guid id, CancellationToken ct = default)
        => MapCollection(await db.KnowledgeCollections.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct));

    public Task AddCollectionAsync(KnowledgeCollectionState collection, CancellationToken ct = default)
    {
        db.KnowledgeCollections.Add(new KnowledgeCollectionRecord
        {
            Id = collection.Id,
            Name = collection.Name,
            Description = collection.Description,
            Owner = collection.Owner,
            Classification = collection.Classification,
            Status = collection.Status,
            CreatedAt = collection.CreatedAt,
            UpdatedAt = collection.UpdatedAt,
            Revision = collection.Revision
        });
        return Task.CompletedTask;
    }

    public async Task UpdateCollectionAsync(
        KnowledgeCollectionState collection,
        long expectedRevision,
        CancellationToken ct = default)
    {
        var record = await db.KnowledgeCollections.FirstOrDefaultAsync(item => item.Id == collection.Id, ct)
            ?? throw new ResourceNotFoundException(
                "knowledge.collection.not_found",
                $"Knowledge collection '{collection.Id}' was not found.");
        if (record.Revision != expectedRevision)
            throw new ConcurrencyConflictException("knowledge collection", collection.Id);

        record.Name = collection.Name;
        record.Description = collection.Description;
        record.Owner = collection.Owner;
        record.Classification = collection.Classification;
        record.Status = collection.Status;
        record.UpdatedAt = collection.UpdatedAt;
        record.Revision = collection.Revision;
    }

    public async Task<IReadOnlyList<KnowledgeDocumentState>> ListDocumentsAsync(Guid collectionId, CancellationToken ct = default)
        => (await db.KnowledgeDocuments.AsNoTracking()
                .Where(item => item.CollectionId == collectionId && db.KnowledgeCollections.Any(collection => collection.Id == item.CollectionId))
                .ToListAsync(ct))
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => MapDocument(item)!)
            .ToList();

    public async Task<KnowledgeDocumentState?> GetDocumentAsync(Guid id, CancellationToken ct = default)
        => MapDocument(await db.KnowledgeDocuments.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && db.KnowledgeCollections.Any(collection => collection.Id == item.CollectionId), ct));

    public Task AddDocumentAsync(KnowledgeDocumentState document, CancellationToken ct = default)
    {
        db.KnowledgeDocuments.Add(new KnowledgeDocumentRecord
        {
            Id = document.Id,
            CollectionId = document.CollectionId,
            Title = document.Title,
            OriginalFileName = document.OriginalFileName,
            ContentType = document.ContentType,
            SizeBytes = document.SizeBytes,
            StorageKey = document.StorageKey,
            Status = document.Stage,
            Classification = document.Classification,
            Owner = document.Owner,
            Category = document.Category,
            TagsJson = JsonSerializer.Serialize(document.Tags),
            Version = document.Version,
            Error = document.Error,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            PublishedAt = document.PublishedAt,
            Revision = document.Revision
        });
        return Task.CompletedTask;
    }

    public async Task UpdateDocumentAsync(
        KnowledgeDocumentState document,
        long expectedRevision,
        CancellationToken ct = default)
    {
        var record = await db.KnowledgeDocuments.FirstOrDefaultAsync(item => item.Id == document.Id && db.KnowledgeCollections.Any(collection => collection.Id == item.CollectionId), ct)
            ?? throw new ResourceNotFoundException(
                "knowledge.document.not_found",
                $"Knowledge document '{document.Id}' was not found.");
        if (record.Revision != expectedRevision)
            throw new ConcurrencyConflictException("knowledge document", document.Id);

        record.Title = document.Title;
        record.OriginalFileName = document.OriginalFileName;
        record.ContentType = document.ContentType;
        record.SizeBytes = document.SizeBytes;
        record.StorageKey = document.StorageKey;
        record.Status = document.Stage;
        record.Classification = document.Classification;
        record.Owner = document.Owner;
        record.Category = document.Category;
        record.TagsJson = JsonSerializer.Serialize(document.Tags);
        record.Version = document.Version;
        record.Error = document.Error;
        record.UpdatedAt = document.UpdatedAt;
        record.PublishedAt = document.PublishedAt;
        record.Revision = document.Revision;
    }

    public async Task DeleteDocumentAsync(Guid id, CancellationToken ct = default)
    {
        var record = await db.KnowledgeDocuments.FirstOrDefaultAsync(item => item.Id == id && db.KnowledgeCollections.Any(collection => collection.Id == item.CollectionId), ct);
        if (record is not null) db.KnowledgeDocuments.Remove(record);
    }

    public async Task<IReadOnlyList<KnowledgeChunkState>> ListChunksAsync(Guid documentId, CancellationToken ct = default)
        => (await db.KnowledgeChunks.AsNoTracking()
                .Where(item => item.DocumentId == documentId && db.KnowledgeCollections.Any(collection => collection.Id == item.CollectionId))
                .OrderBy(item => item.Sequence)
                .ToListAsync(ct))
            .Select(MapChunk)
            .ToList();

    public async Task<IReadOnlyList<KnowledgeChunkState>> ListCollectionChunksAsync(
        Guid collectionId,
        bool publishedOnly,
        CancellationToken ct = default)
    {
        var query = db.KnowledgeChunks.AsNoTracking().Where(item => item.CollectionId == collectionId && db.KnowledgeCollections.Any(collection => collection.Id == item.CollectionId));
        if (publishedOnly) query = query.Where(item => item.Published);
        return (await query.OrderBy(item => item.Sequence).ToListAsync(ct)).Select(MapChunk).ToList();
    }

    public async Task ReplaceChunksAsync(
        Guid documentId,
        IReadOnlyList<KnowledgeChunkState> chunks,
        CancellationToken ct = default)
    {
        if (!await db.KnowledgeDocuments.AnyAsync(item => item.Id == documentId && db.KnowledgeCollections.Any(collection => collection.Id == item.CollectionId), ct))
            throw new ResourceNotFoundException("knowledge.document.not_found", $"Knowledge document '{documentId}' was not found.");
        await db.KnowledgeChunks.Where(item => item.DocumentId == documentId).ExecuteDeleteAsync(ct);
        db.KnowledgeChunks.AddRange(chunks.Select(chunk => new KnowledgeChunkRecord
        {
            Id = chunk.Id,
            DocumentId = chunk.DocumentId,
            CollectionId = chunk.CollectionId,
            Sequence = chunk.Sequence,
            Text = chunk.Text,
            PageNumber = chunk.PageNumber,
            Section = chunk.Section,
            CharacterCount = chunk.CharacterCount,
            EstimatedTokens = chunk.EstimatedTokens,
            Classification = chunk.Classification,
            Published = chunk.Published
        }));
    }

    public Task SetChunksPublishedAsync(Guid documentId, bool published, CancellationToken ct = default)
        => db.KnowledgeChunks
            .Where(item => item.DocumentId == documentId && db.KnowledgeCollections.Any(collection => collection.Id == item.CollectionId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Published, published), ct);

    public Task DeleteChunksAsync(Guid documentId, CancellationToken ct = default)
        => db.KnowledgeChunks.Where(item => item.DocumentId == documentId && db.KnowledgeCollections.Any(collection => collection.Id == item.CollectionId)).ExecuteDeleteAsync(ct);

    public Task AddLifecycleEntryAsync(KnowledgeLifecycleState entry, CancellationToken ct = default)
    {
        db.KnowledgeLifecycle.Add(new KnowledgeLifecycleRecord
        {
            Id = entry.Id,
            DocumentId = entry.DocumentId,
            Actor = entry.Actor,
            Action = entry.Action,
            Reason = entry.Reason,
            PreviousStatus = entry.PreviousStage,
            NewStatus = entry.NewStage,
            At = entry.At
        });
        return Task.CompletedTask;
    }

    public Task DeleteLifecycleAsync(Guid documentId, CancellationToken ct = default)
        => db.KnowledgeLifecycle.Where(item => item.DocumentId == documentId && db.KnowledgeDocuments.Any(document => document.Id == item.DocumentId && db.KnowledgeCollections.Any(collection => collection.Id == document.CollectionId))).ExecuteDeleteAsync(ct);

    private static KnowledgeCollectionState? MapCollection(KnowledgeCollectionRecord? record)
        => record is null
            ? null
            : new KnowledgeCollectionState(
                record.Id,
                record.Name,
                record.Description,
                record.Owner,
                record.Classification,
                record.Status,
                record.CreatedAt,
                record.UpdatedAt,
                record.Revision);

    private static KnowledgeDocumentState? MapDocument(KnowledgeDocumentRecord? record)
        => record is null
            ? null
            : new KnowledgeDocumentState(
                record.Id,
                record.CollectionId,
                record.Title,
                record.OriginalFileName,
                record.ContentType,
                record.SizeBytes,
                record.StorageKey,
                record.Status,
                record.Classification,
                record.Owner,
                record.Category,
                JsonSerializer.Deserialize<List<string>>(record.TagsJson) ?? [],
                record.Version,
                record.Error,
                record.CreatedAt,
                record.UpdatedAt,
                record.PublishedAt,
                record.Revision);

    private static KnowledgeChunkState MapChunk(KnowledgeChunkRecord record)
        => new(
            record.Id,
            record.DocumentId,
            record.CollectionId,
            record.Sequence,
            record.Text,
            record.PageNumber,
            record.Section,
            record.CharacterCount,
            record.EstimatedTokens,
            record.Classification,
            record.Published);

}
