using ConvoLab.Domain.Common;
using ConvoLab.Domain.Knowledge.Enums;
using ConvoLab.Domain.Knowledge.ValueObjects;

namespace ConvoLab.Domain.Knowledge.Entities;

/// <summary>
/// A governed knowledge document ingested from a source. Documents own their
/// chunks, carry a stable reference back to the origin system, and follow the
/// knowledge lifecycle (Draft → PendingApproval → Approved → Published → Deprecated → Archived).
/// Published documents are immutable; changes create a new version.
/// </summary>
public class KnowledgeDocument : BaseEntity<KnowledgeDocumentId>
{
    public string Title { get; private set; }
    public KnowledgeReference Reference { get; private set; }
    public KnowledgeVersion Version { get; private set; }
    public KnowledgeLifecycleStatus Status { get; private set; }
    public KnowledgeMetadata Metadata { get; private set; }
    public KnowledgePolicy Policy { get; private set; }
    public string ContentHash { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    private readonly List<KnowledgeChunk> _chunks = new();
    public IReadOnlyCollection<KnowledgeChunk> Chunks => _chunks.AsReadOnly();

    private KnowledgeDocument()
    {
        Title = null!; Reference = null!; Version = null!;
        Metadata = null!; Policy = null!; ContentHash = null!;
    } // For EF Core

    private KnowledgeDocument(
        KnowledgeDocumentId id,
        string title,
        KnowledgeReference reference,
        KnowledgeMetadata metadata,
        KnowledgePolicy policy,
        string contentHash) : base(id)
    {
        Title = title;
        Reference = reference;
        Metadata = metadata;
        Policy = policy;
        ContentHash = contentHash;
        Version = KnowledgeVersion.Initial();
        Status = KnowledgeLifecycleStatus.Draft;
    }

    public static KnowledgeDocument Create(
        string title,
        KnowledgeReference reference,
        KnowledgeMetadata? metadata = null,
        KnowledgePolicy? policy = null,
        string contentHash = "")
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Document title cannot be empty.", nameof(title));

        return new KnowledgeDocument(
            KnowledgeDocumentId.CreateUnique(),
            title,
            reference ?? throw new ArgumentNullException(nameof(reference)),
            metadata ?? KnowledgeMetadata.Empty(),
            policy ?? KnowledgePolicy.Default(),
            contentHash ?? string.Empty);
    }

    #region Chunking

    /// <summary>
    /// Adds a chunk to the document. Chunking is only allowed while the document
    /// is mutable (not Published/Archived) — published knowledge is immutable.
    /// </summary>
    public KnowledgeChunk AddChunk(ChunkType type, string content, KnowledgeMetadata? metadata = null)
    {
        EnsureMutable();
        var chunk = KnowledgeChunk.Create(Id, type, content, _chunks.Count, metadata);
        _chunks.Add(chunk);
        LastModifiedAt = DateTime.UtcNow;
        return chunk;
    }

    public void ClearChunks()
    {
        EnsureMutable();
        _chunks.Clear();
        LastModifiedAt = DateTime.UtcNow;
    }

    public int TotalEstimatedTokens => _chunks.Sum(c => c.EstimatedTokens);

    #endregion

    #region Lifecycle

    public void SubmitForApproval()
    {
        if (Status != KnowledgeLifecycleStatus.Draft)
            throw new InvalidOperationException($"Only Draft documents can be submitted for approval. Current status: {Status}.");
        if (!_chunks.Any())
            throw new InvalidOperationException("A document must have at least one chunk before it can be submitted for approval.");
        Status = KnowledgeLifecycleStatus.PendingApproval;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void Approve()
    {
        if (Status != KnowledgeLifecycleStatus.PendingApproval)
            throw new InvalidOperationException($"Only PendingApproval documents can be approved. Current status: {Status}.");
        Status = KnowledgeLifecycleStatus.Approved;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void Reject()
    {
        if (Status != KnowledgeLifecycleStatus.PendingApproval)
            throw new InvalidOperationException($"Only PendingApproval documents can be rejected. Current status: {Status}.");
        Status = KnowledgeLifecycleStatus.Draft;
        LastModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Publishes the document, making it retrievable. If the governing policy
    /// requires approval, the document must be Approved first.
    /// </summary>
    public void Publish()
    {
        if (Policy.RequiresApprovalBeforePublish && Status != KnowledgeLifecycleStatus.Approved)
            throw new InvalidOperationException("This document's policy requires approval before publishing.");
        if (Status is KnowledgeLifecycleStatus.Published or KnowledgeLifecycleStatus.Archived)
            throw new InvalidOperationException($"Cannot publish a document in status {Status}.");
        if (!_chunks.Any())
            throw new InvalidOperationException("A document must have at least one chunk before it can be published.");

        Status = KnowledgeLifecycleStatus.Published;
        PublishedAt = DateTime.UtcNow;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void Deprecate()
    {
        if (Status != KnowledgeLifecycleStatus.Published)
            throw new InvalidOperationException("Only Published documents can be deprecated.");
        Status = KnowledgeLifecycleStatus.Deprecated;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        if (Status == KnowledgeLifecycleStatus.Published)
            throw new InvalidOperationException("Cannot archive a Published document. Deprecate it first.");
        Status = KnowledgeLifecycleStatus.Archived;
        LastModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new version of the document (content changed at the source).
    /// Published knowledge is immutable, so versioning returns the document to Draft
    /// with an incremented version for re-approval and re-publication.
    /// </summary>
    public void CreateNewVersion(string newContentHash)
    {
        if (Status == KnowledgeLifecycleStatus.Archived)
            throw new InvalidOperationException("Cannot version an Archived document.");
        Version = Version.IncrementMinor();
        ContentHash = newContentHash ?? string.Empty;
        Status = KnowledgeLifecycleStatus.Draft;
        PublishedAt = null;
        _chunks.Clear();
        LastModifiedAt = DateTime.UtcNow;
    }

    public bool IsRetrievable => Status == KnowledgeLifecycleStatus.Published;

    private void EnsureMutable()
    {
        if (Status is KnowledgeLifecycleStatus.Published or KnowledgeLifecycleStatus.Archived)
            throw new InvalidOperationException($"Document is immutable in status {Status}. Create a new version to change content.");
    }

    #endregion

    #region Governance

    public void UpdateMetadata(KnowledgeMetadata metadata)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        LastModifiedAt = DateTime.UtcNow;
    }

    public void ApplyPolicy(KnowledgePolicy policy)
    {
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        LastModifiedAt = DateTime.UtcNow;
    }

    #endregion
}
