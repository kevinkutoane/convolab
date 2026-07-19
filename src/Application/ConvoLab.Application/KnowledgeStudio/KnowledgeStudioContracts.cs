using ConvoLab.Domain.Knowledge.Enums;

namespace ConvoLab.Application.KnowledgeStudio;

public enum KnowledgeCollectionStatus { Active, Archived }

public sealed record KnowledgeCollectionDto(
    Guid Id,
    string Name,
    string Description,
    string Owner,
    KnowledgeClassification Classification,
    KnowledgeCollectionStatus Status,
    int DocumentCount,
    int ChunkCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long Revision);

public sealed record KnowledgeDocumentDto(
    Guid Id,
    Guid CollectionId,
    string Title,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    KnowledgeDocumentStage Status,
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

public sealed record KnowledgeChunkDto(
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

public sealed record KnowledgeLifecycleEntryDto(
    Guid Id,
    Guid DocumentId,
    string Actor,
    string Action,
    string? Reason,
    KnowledgeDocumentStage PreviousStatus,
    KnowledgeDocumentStage NewStatus,
    DateTimeOffset At);

public sealed record KnowledgeSearchResultDto(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentTitle,
    int Rank,
    double Confidence,
    string Text,
    int? PageNumber,
    string? Section,
    IReadOnlyList<string> MatchingTerms,
    int EstimatedTokens);

public sealed record KnowledgeQueryResponse(
    Guid CollectionId,
    string Query,
    int TokenEstimate,
    IReadOnlyList<KnowledgeSearchResultDto> Results,
    IReadOnlyList<string> Exclusions,
    DateTimeOffset RetrievedAt);

public sealed record KnowledgeCollectionHealthDto(
    Guid CollectionId,
    int Documents,
    int PublishedDocuments,
    int FailedDocuments,
    int Chunks,
    int PublishedChunks,
    string Status);

public sealed record CreateKnowledgeCollectionCommand(
    string Name,
    string Description,
    string Owner,
    KnowledgeClassification Classification);

public sealed record UpdateKnowledgeCollectionCommand(
    string? Name,
    string? Description,
    string? Owner,
    KnowledgeClassification? Classification,
    long? ExpectedRevision = null);

public sealed record UpdateKnowledgeDocumentCommand(
    string? Title,
    string? Owner,
    string? Category,
    IReadOnlyList<string>? Tags,
    KnowledgeClassification? Classification,
    long? ExpectedRevision = null);

public sealed record KnowledgeLifecycleCommand(
    string Actor,
    string? Reason,
    long? ExpectedRevision = null);

public sealed record KnowledgeQueryCommand(
    string Query,
    int MaxResults = 5,
    double MinimumConfidence = 0.05,
    int TokenBudget = 2000);

public sealed record KnowledgeUpload(
    Guid CollectionId,
    string FileName,
    string ContentType,
    long Length,
    Stream Content,
    string Owner,
    KnowledgeClassification Classification);

public interface IKnowledgeStudioService
{
    Task<IReadOnlyList<KnowledgeCollectionDto>> ListCollectionsAsync(CancellationToken ct = default);
    Task<KnowledgeCollectionDto?> GetCollectionAsync(Guid id, CancellationToken ct = default);
    Task<KnowledgeCollectionDto> CreateCollectionAsync(CreateKnowledgeCollectionCommand command, CancellationToken ct = default);
    Task<KnowledgeCollectionDto?> UpdateCollectionAsync(Guid id, UpdateKnowledgeCollectionCommand command, CancellationToken ct = default);
    Task<KnowledgeCollectionDto?> ArchiveCollectionAsync(Guid id, CancellationToken ct = default);
    Task<KnowledgeCollectionDto?> RestoreCollectionAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeDocumentDto>> ListDocumentsAsync(Guid collectionId, CancellationToken ct = default);
    Task<KnowledgeDocumentDto?> GetDocumentAsync(Guid id, CancellationToken ct = default);
    Task<KnowledgeDocumentDto> UploadAsync(KnowledgeUpload upload, CancellationToken ct = default);
    Task<KnowledgeDocumentDto?> UpdateDocumentAsync(Guid id, UpdateKnowledgeDocumentCommand command, CancellationToken ct = default);
    Task<KnowledgeDocumentDto?> ProcessAsync(Guid id, CancellationToken ct = default);
    Task<KnowledgeDocumentDto?> RetryAsync(Guid id, CancellationToken ct = default);
    Task<KnowledgeDocumentDto?> TransitionAsync(Guid id, string action, KnowledgeLifecycleCommand command, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeChunkDto>> GetChunksAsync(Guid documentId, CancellationToken ct = default);
    Task<KnowledgeQueryResponse> QueryAsync(Guid collectionId, KnowledgeQueryCommand command, CancellationToken ct = default);
    Task<KnowledgeCollectionHealthDto?> GetHealthAsync(Guid collectionId, CancellationToken ct = default);
    Task<bool> DeleteDocumentAsync(Guid id, CancellationToken ct = default);
}

public sealed record StoredKnowledgeDocument(string StorageKey, string SafeFileName, string ContentType, long SizeBytes);
public interface IKnowledgeDocumentStorage
{
    Task<StoredKnowledgeDocument> StoreAsync(string fileName, string contentType, Stream content, CancellationToken ct = default);
    Task<Stream> OpenAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
    Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default);
    Task<bool> ProbeAsync(CancellationToken ct = default);
}

public sealed record ExtractedSection(string? Heading, int? PageNumber, string Text);
public sealed record ExtractedKnowledgeDocument(string Title, string FullText, IReadOnlyList<ExtractedSection> Sections, IReadOnlyList<string> Warnings);
public interface IDocumentTextExtractor { bool CanExtract(string extension, string contentType); Task<ExtractedKnowledgeDocument> ExtractAsync(Stream stream, string fileName, CancellationToken ct = default); }
public interface IDocumentTextExtractorResolver { IDocumentTextExtractor Resolve(string extension, string contentType); }
