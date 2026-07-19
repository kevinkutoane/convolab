using ConvoLab.Domain.Knowledge.Enums;

namespace ConvoLab.Application.KnowledgeStudio;

public sealed record KnowledgeCollectionState(
    Guid Id,
    string Name,
    string Description,
    string Owner,
    KnowledgeClassification Classification,
    KnowledgeCollectionStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long Revision);

public sealed record KnowledgeDocumentState(
    Guid Id,
    Guid CollectionId,
    string Title,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string StorageKey,
    KnowledgeDocumentStage Stage,
    KnowledgeClassification Classification,
    string Owner,
    string Category,
    IReadOnlyList<string> Tags,
    int Version,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt,
    long Revision);

public sealed record KnowledgeChunkState(
    Guid Id,
    Guid DocumentId,
    Guid CollectionId,
    int Sequence,
    string Text,
    int? PageNumber,
    string? Section,
    int CharacterCount,
    int EstimatedTokens,
    KnowledgeClassification Classification,
    bool Published);

public sealed record KnowledgeLifecycleState(
    Guid Id,
    Guid DocumentId,
    string Actor,
    string Action,
    string? Reason,
    KnowledgeDocumentStage PreviousStage,
    KnowledgeDocumentStage NewStage,
    DateTimeOffset At);

public sealed record RankedKnowledgeChunk(
    KnowledgeChunkState Chunk,
    string DocumentTitle,
    double Confidence,
    IReadOnlyList<string> MatchingTerms);

public interface IKnowledgeStudioRepository
{
    Task<IReadOnlyList<KnowledgeCollectionState>> ListCollectionsAsync(CancellationToken ct = default);
    Task<KnowledgeCollectionState?> GetCollectionAsync(Guid id, CancellationToken ct = default);
    Task AddCollectionAsync(KnowledgeCollectionState collection, CancellationToken ct = default);
    Task UpdateCollectionAsync(KnowledgeCollectionState collection, long expectedRevision, CancellationToken ct = default);

    Task<IReadOnlyList<KnowledgeDocumentState>> ListDocumentsAsync(Guid collectionId, CancellationToken ct = default);
    Task<KnowledgeDocumentState?> GetDocumentAsync(Guid id, CancellationToken ct = default);
    Task AddDocumentAsync(KnowledgeDocumentState document, CancellationToken ct = default);
    Task UpdateDocumentAsync(KnowledgeDocumentState document, long expectedRevision, CancellationToken ct = default);
    Task DeleteDocumentAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<KnowledgeChunkState>> ListChunksAsync(Guid documentId, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeChunkState>> ListCollectionChunksAsync(Guid collectionId, bool publishedOnly, CancellationToken ct = default);
    Task ReplaceChunksAsync(Guid documentId, IReadOnlyList<KnowledgeChunkState> chunks, CancellationToken ct = default);
    Task SetChunksPublishedAsync(Guid documentId, bool published, CancellationToken ct = default);
    Task DeleteChunksAsync(Guid documentId, CancellationToken ct = default);

    Task AddLifecycleEntryAsync(KnowledgeLifecycleState entry, CancellationToken ct = default);
    Task DeleteLifecycleAsync(Guid documentId, CancellationToken ct = default);
}

public interface IKnowledgeChunker
{
    IReadOnlyList<KnowledgeChunkState> Chunk(ExtractedKnowledgeDocument document, KnowledgeDocumentState source);
}

public interface IKeywordKnowledgeRetriever
{
    IReadOnlyList<RankedKnowledgeChunk> Rank(
        string query,
        IReadOnlyDictionary<Guid, string> documentTitles,
        IReadOnlyList<KnowledgeChunkState> chunks,
        int maxResults,
        double minimumConfidence);
}
