using ConvoLab.Domain.Common;
using ConvoLab.Domain.Knowledge.Entities;
using ConvoLab.Domain.Knowledge.Enums;
using ConvoLab.Domain.Knowledge.Events;
using ConvoLab.Domain.Knowledge.ValueObjects;

namespace ConvoLab.Domain.Knowledge.Aggregates;

/// <summary>
/// The KnowledgeSource aggregate root. Represents a governed enterprise knowledge
/// origin (SharePoint site, Confluence space, database, API, document store, etc.)
/// and owns the documents ingested from it.
///
/// Core invariants:
/// - Every source has an accountable owner and a governing policy.
/// - Published documents are immutable; changes create new versions.
/// - Retrieval only ever sees Published documents.
/// - The source knows nothing about vector stores, embeddings, or providers.
/// </summary>
public class KnowledgeSource : BaseAggregateRoot<KnowledgeSourceId>
{
    public string Name { get; private set; }
    public KnowledgeSourceType SourceType { get; private set; }
    public KnowledgeLifecycleStatus Status { get; private set; }
    public KnowledgeOwner Owner { get; private set; }
    public KnowledgePolicy Policy { get; private set; }
    public KnowledgeMetadata Metadata { get; private set; }
    public KnowledgeHealth Health { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<KnowledgeDocument> _documents = new();
    public IReadOnlyCollection<KnowledgeDocument> Documents => _documents.AsReadOnly();

    private KnowledgeSource()
    {
        Name = null!; Owner = null!; Policy = null!; Metadata = null!; Health = null!;
    } // For EF Core

    private KnowledgeSource(
        KnowledgeSourceId id,
        string name,
        KnowledgeSourceType sourceType,
        KnowledgeOwner owner,
        KnowledgePolicy policy,
        KnowledgeMetadata metadata) : base(id)
    {
        Name = name;
        SourceType = sourceType;
        Owner = owner;
        Policy = policy;
        Metadata = metadata;
        Status = KnowledgeLifecycleStatus.Draft;
        Health = KnowledgeHealth.Unknown();
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new KnowledgeSourceRegisteredEvent(id, name, sourceType, owner.OwnerId));
    }

    /// <summary>Registers a new enterprise knowledge source under governance.</summary>
    public static KnowledgeSource Register(
        string name,
        KnowledgeSourceType sourceType,
        KnowledgeOwner owner,
        KnowledgePolicy? policy = null,
        KnowledgeMetadata? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Knowledge source name cannot be empty.", nameof(name));

        return new KnowledgeSource(
            KnowledgeSourceId.CreateUnique(),
            name,
            sourceType,
            owner ?? throw new ArgumentNullException(nameof(owner)),
            policy ?? KnowledgePolicy.Default(),
            metadata ?? KnowledgeMetadata.Empty());
    }

    #region Document Ingestion

    /// <summary>
    /// Ingests a document into the source. The document inherits the source policy
    /// unless a document-specific policy is provided.
    /// </summary>
    public KnowledgeDocument IngestDocument(
        string title,
        string externalId,
        string uri,
        KnowledgeMetadata? metadata = null,
        KnowledgePolicy? policy = null,
        string contentHash = "")
    {
        if (Status == KnowledgeLifecycleStatus.Archived)
            throw new InvalidOperationException("Cannot ingest documents into an Archived source.");

        var reference = KnowledgeReference.Create(Id, externalId, uri, title);
        var document = KnowledgeDocument.Create(title, reference, metadata, policy ?? Policy, contentHash);
        _documents.Add(document);
        UpdatedAt = DateTime.UtcNow;

        return document;
    }

    /// <summary>
    /// Marks a document as indexed once chunking has completed, raising the
    /// KnowledgeIndexed domain event for downstream consumers (tracing, evaluation).
    /// </summary>
    public void MarkDocumentIndexed(KnowledgeDocumentId documentId)
    {
        var document = GetDocument(documentId);
        if (!document.Chunks.Any())
            throw new InvalidOperationException("Cannot mark a document as indexed before chunks exist.");

        AddDomainEvent(new KnowledgeIndexedEvent(Id, documentId, document.Chunks.Count));
        UpdatedAt = DateTime.UtcNow;
    }

    #endregion

    #region Document Lifecycle

    /// <summary>Publishes a document version, making it retrievable across the platform.</summary>
    public void PublishDocument(KnowledgeDocumentId documentId)
    {
        var document = GetDocument(documentId);
        document.Publish();
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new KnowledgeVersionPublishedEvent(Id, documentId, document.Version.ToString()));
    }

    /// <summary>Deprecates a published document so it is excluded from retrieval.</summary>
    public void DeprecateDocument(KnowledgeDocumentId documentId)
    {
        var document = GetDocument(documentId);
        document.Deprecate();
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new KnowledgeDeprecatedEvent(Id, documentId));
    }

    /// <summary>Archives a document, removing it permanently from the retrievable estate.</summary>
    public void ArchiveDocument(KnowledgeDocumentId documentId)
    {
        var document = GetDocument(documentId);
        document.Archive();
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new KnowledgeArchivedEvent(Id, documentId));
    }

    /// <summary>
    /// Registers a content change detected at the origin system: creates a new
    /// document version and raises KnowledgeUpdated.
    /// </summary>
    public void RegisterContentChange(KnowledgeDocumentId documentId, string newContentHash)
    {
        var document = GetDocument(documentId);
        document.CreateNewVersion(newContentHash);
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new KnowledgeUpdatedEvent(Id, documentId, document.Version.ToString()));
    }

    #endregion

    #region Source Lifecycle

    /// <summary>Activates the source, making its published documents retrievable.</summary>
    public void Activate()
    {
        if (Status == KnowledgeLifecycleStatus.Archived)
            throw new InvalidOperationException("Cannot activate an Archived source.");
        Status = KnowledgeLifecycleStatus.Published;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deprecate()
    {
        if (Status != KnowledgeLifecycleStatus.Published)
            throw new InvalidOperationException("Only a Published source can be deprecated.");
        Status = KnowledgeLifecycleStatus.Deprecated;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        if (Status == KnowledgeLifecycleStatus.Published)
            throw new InvalidOperationException("Cannot archive a Published source. Deprecate it first.");
        Status = KnowledgeLifecycleStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }

    #endregion

    #region Governance & Health

    public void AssignOwner(KnowledgeOwner newOwner)
    {
        Owner = newOwner ?? throw new ArgumentNullException(nameof(newOwner));
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyPolicy(KnowledgePolicy policy)
    {
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateMetadata(KnowledgeMetadata metadata)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReportHealth(KnowledgeHealth health)
    {
        Health = health ?? throw new ArgumentNullException(nameof(health));
        UpdatedAt = DateTime.UtcNow;
    }

    #endregion

    #region Helpers

    public KnowledgeDocument GetDocument(KnowledgeDocumentId documentId)
    {
        return _documents.FirstOrDefault(d => d.Id == documentId)
            ?? throw new InvalidOperationException($"Document '{documentId}' not found in source '{Name}'.");
    }

    /// <summary>Returns only the documents that retrieval is allowed to see.</summary>
    public IEnumerable<KnowledgeDocument> GetRetrievableDocuments()
        => _documents.Where(d => d.IsRetrievable);

    public bool IsRetrievable => Status == KnowledgeLifecycleStatus.Published;

    #endregion
}
