using ConvoLab.Domain.Common;
using ConvoLab.Domain.Knowledge.Enums;

namespace ConvoLab.Domain.Knowledge.ValueObjects;

/// <summary>
/// The accountable owner of a knowledge asset. Every governed knowledge source
/// must have a named owner for audit and stewardship purposes.
/// </summary>
public class KnowledgeOwner : ValueObject
{
    public Guid OwnerId { get; private set; }
    public string Name { get; private set; }
    public string Department { get; private set; }

    private KnowledgeOwner() { Name = null!; Department = null!; } // For EF Core

    private KnowledgeOwner(Guid ownerId, string name, string department)
    {
        OwnerId = ownerId;
        Name = name;
        Department = department;
    }

    public static KnowledgeOwner Create(Guid ownerId, string name, string department)
    {
        if (ownerId == Guid.Empty)
            throw new ArgumentException("Owner id cannot be empty.", nameof(ownerId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Owner name cannot be empty.", nameof(name));
        return new KnowledgeOwner(ownerId, name, department ?? string.Empty);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return OwnerId;
        yield return Name;
        yield return Department;
    }
}

/// <summary>
/// Semantic version of a knowledge asset. Knowledge is immutable once published;
/// changes always produce a new version.
/// </summary>
public class KnowledgeVersion : ValueObject
{
    public int Major { get; private set; }
    public int Minor { get; private set; }
    public int Patch { get; private set; }

    private KnowledgeVersion() { } // For EF Core

    private KnowledgeVersion(int major, int minor, int patch)
    {
        if (major < 0 || minor < 0 || patch < 0)
            throw new ArgumentException("Version components cannot be negative.");
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    public static KnowledgeVersion Initial() => new(1, 0, 0);
    public static KnowledgeVersion Create(int major, int minor, int patch) => new(major, minor, patch);

    public KnowledgeVersion IncrementMajor() => new(Major + 1, 0, 0);
    public KnowledgeVersion IncrementMinor() => new(Major, Minor + 1, 0);
    public KnowledgeVersion IncrementPatch() => new(Major, Minor, Patch + 1);

    public bool IsNewerThan(KnowledgeVersion other) =>
        (Major, Minor, Patch).CompareTo((other.Major, other.Minor, other.Patch)) > 0;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Major;
        yield return Minor;
        yield return Patch;
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

/// <summary>
/// A governance policy attached to knowledge assets: classification, sensitivity,
/// retention, and allowed-audience rules that retrieval must honour.
/// </summary>
public class KnowledgePolicy : ValueObject
{
    public string Name { get; private set; }
    public KnowledgeClassification Classification { get; private set; }
    public SensitivityLabel Sensitivity { get; private set; }
    public TimeSpan? RetentionPeriod { get; private set; }
    public bool RequiresApprovalBeforePublish { get; private set; }

    private KnowledgePolicy() { Name = null!; } // For EF Core

    private KnowledgePolicy(
        string name,
        KnowledgeClassification classification,
        SensitivityLabel sensitivity,
        TimeSpan? retentionPeriod,
        bool requiresApprovalBeforePublish)
    {
        Name = name;
        Classification = classification;
        Sensitivity = sensitivity;
        RetentionPeriod = retentionPeriod;
        RequiresApprovalBeforePublish = requiresApprovalBeforePublish;
    }

    public static KnowledgePolicy Create(
        string name,
        KnowledgeClassification classification = KnowledgeClassification.Internal,
        SensitivityLabel sensitivity = SensitivityLabel.None,
        TimeSpan? retentionPeriod = null,
        bool requiresApprovalBeforePublish = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Policy name cannot be empty.", nameof(name));
        return new KnowledgePolicy(name, classification, sensitivity, retentionPeriod, requiresApprovalBeforePublish);
    }

    /// <summary>Default open policy for non-sensitive internal knowledge.</summary>
    public static KnowledgePolicy Default() =>
        new("Default", KnowledgeClassification.Internal, SensitivityLabel.None, null, false);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Name;
        yield return Classification;
        yield return Sensitivity;
        yield return RetentionPeriod ?? TimeSpan.Zero;
        yield return RequiresApprovalBeforePublish;
    }
}

/// <summary>
/// Free-form governed metadata attached to knowledge assets: tags, language,
/// business domain, and arbitrary key/value attributes used by metadata search.
/// </summary>
public class KnowledgeMetadata : ValueObject
{
    public string Description { get; private set; }
    public string Language { get; private set; }
    public string BusinessDomain { get; private set; }
    public IReadOnlyList<string> Tags { get; private set; }
    public IReadOnlyDictionary<string, string> Attributes { get; private set; }

    private KnowledgeMetadata()
    {
        Description = null!; Language = null!; BusinessDomain = null!;
        Tags = new List<string>(); Attributes = new Dictionary<string, string>();
    } // For EF Core

    private KnowledgeMetadata(
        string description,
        string language,
        string businessDomain,
        IReadOnlyList<string> tags,
        IReadOnlyDictionary<string, string> attributes)
    {
        Description = description;
        Language = language;
        BusinessDomain = businessDomain;
        Tags = tags;
        Attributes = attributes;
    }

    public static KnowledgeMetadata Create(
        string description,
        string language = "en",
        string businessDomain = "",
        IEnumerable<string>? tags = null,
        IDictionary<string, string>? attributes = null)
    {
        return new KnowledgeMetadata(
            description ?? string.Empty,
            language,
            businessDomain,
            (tags ?? Enumerable.Empty<string>()).ToList().AsReadOnly(),
            new Dictionary<string, string>(attributes ?? new Dictionary<string, string>()));
    }

    public static KnowledgeMetadata Empty() => Create(string.Empty);

    public bool HasTag(string tag) => Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Description;
        yield return Language;
        yield return BusinessDomain;
        foreach (var tag in Tags) yield return tag;
        foreach (var kvp in Attributes.OrderBy(k => k.Key)) { yield return kvp.Key; yield return kvp.Value; }
    }
}

/// <summary>
/// A stable, provider-independent reference to a piece of knowledge in its source system,
/// e.g. a SharePoint URL, a database row locator, or a Confluence page id.
/// </summary>
public class KnowledgeReference : ValueObject
{
    public KnowledgeSourceId SourceId { get; private set; }
    public string ExternalId { get; private set; }
    public string Uri { get; private set; }
    public string DisplayName { get; private set; }

    private KnowledgeReference() { SourceId = null!; ExternalId = null!; Uri = null!; DisplayName = null!; } // For EF Core

    private KnowledgeReference(KnowledgeSourceId sourceId, string externalId, string uri, string displayName)
    {
        SourceId = sourceId;
        ExternalId = externalId;
        Uri = uri;
        DisplayName = displayName;
    }

    public static KnowledgeReference Create(KnowledgeSourceId sourceId, string externalId, string uri, string displayName)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("External id cannot be empty.", nameof(externalId));
        return new KnowledgeReference(sourceId, externalId, uri ?? string.Empty, displayName ?? externalId);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return SourceId;
        yield return ExternalId;
        yield return Uri;
        yield return DisplayName;
    }
}

/// <summary>
/// A citation attached to retrieved knowledge so the Prompt Engine and downstream
/// consumers can attribute answers to their sources. Citations are mandatory in packages.
/// </summary>
public class KnowledgeCitation : ValueObject
{
    public KnowledgeReference Reference { get; private set; }
    public KnowledgeDocumentId DocumentId { get; private set; }
    public KnowledgeChunkId? ChunkId { get; private set; }
    public string Excerpt { get; private set; }
    public KnowledgeVersion Version { get; private set; }

    private KnowledgeCitation() { Reference = null!; DocumentId = null!; Excerpt = null!; Version = null!; } // For EF Core

    private KnowledgeCitation(
        KnowledgeReference reference,
        KnowledgeDocumentId documentId,
        KnowledgeChunkId? chunkId,
        string excerpt,
        KnowledgeVersion version)
    {
        Reference = reference;
        DocumentId = documentId;
        ChunkId = chunkId;
        Excerpt = excerpt;
        Version = version;
    }

    public static KnowledgeCitation Create(
        KnowledgeReference reference,
        KnowledgeDocumentId documentId,
        KnowledgeVersion version,
        KnowledgeChunkId? chunkId = null,
        string excerpt = "")
    {
        return new KnowledgeCitation(reference, documentId, chunkId, excerpt ?? string.Empty, version);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Reference;
        yield return DocumentId;
        yield return ChunkId ?? (object)"none";
        yield return Excerpt;
        yield return Version;
    }
}

/// <summary>
/// An opaque reference to an embedding stored in an external vector store.
/// The domain never holds vectors — only a pointer plus model provenance,
/// keeping the engine provider-independent.
/// </summary>
public class KnowledgeEmbeddingReference : ValueObject
{
    public string VectorStoreKey { get; private set; }
    public string EmbeddingModel { get; private set; }
    public int Dimensions { get; private set; }

    private KnowledgeEmbeddingReference() { VectorStoreKey = null!; EmbeddingModel = null!; } // For EF Core

    private KnowledgeEmbeddingReference(string vectorStoreKey, string embeddingModel, int dimensions)
    {
        VectorStoreKey = vectorStoreKey;
        EmbeddingModel = embeddingModel;
        Dimensions = dimensions;
    }

    public static KnowledgeEmbeddingReference Create(string vectorStoreKey, string embeddingModel, int dimensions)
    {
        if (string.IsNullOrWhiteSpace(vectorStoreKey))
            throw new ArgumentException("Vector store key cannot be empty.", nameof(vectorStoreKey));
        if (dimensions <= 0)
            throw new ArgumentException("Embedding dimensions must be positive.", nameof(dimensions));
        return new KnowledgeEmbeddingReference(vectorStoreKey, embeddingModel ?? string.Empty, dimensions);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return VectorStoreKey;
        yield return EmbeddingModel;
        yield return Dimensions;
    }
}

/// <summary>
/// Health report for a knowledge source or connector: status, freshness, and
/// diagnostic detail used by platform operations.
/// </summary>
public class KnowledgeHealth : ValueObject
{
    public HealthStatus Status { get; private set; }
    public DateTime CheckedAt { get; private set; }
    public string Detail { get; private set; }

    private KnowledgeHealth() { Detail = null!; } // For EF Core

    private KnowledgeHealth(HealthStatus status, DateTime checkedAt, string detail)
    {
        Status = status;
        CheckedAt = checkedAt;
        Detail = detail;
    }

    public static KnowledgeHealth Unknown() => new(HealthStatus.Unknown, DateTime.UtcNow, "Not yet checked.");
    public static KnowledgeHealth Healthy(string detail = "OK") => new(HealthStatus.Healthy, DateTime.UtcNow, detail);
    public static KnowledgeHealth Degraded(string detail) => new(HealthStatus.Degraded, DateTime.UtcNow, detail);
    public static KnowledgeHealth Unhealthy(string detail) => new(HealthStatus.Unhealthy, DateTime.UtcNow, detail);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Status;
        yield return CheckedAt;
        yield return Detail;
    }
}

/// <summary>A single (document, version) pair captured in a snapshot.</summary>
public class SnapshotEntry : ValueObject
{
    public KnowledgeDocumentId DocumentId { get; private set; }
    public KnowledgeVersion Version { get; private set; }
    public string ContentHash { get; private set; }

    private SnapshotEntry() { DocumentId = null!; Version = null!; ContentHash = null!; } // For EF Core

    private SnapshotEntry(KnowledgeDocumentId documentId, KnowledgeVersion version, string contentHash)
    {
        DocumentId = documentId;
        Version = version;
        ContentHash = contentHash;
    }

    public static SnapshotEntry Create(KnowledgeDocumentId documentId, KnowledgeVersion version, string contentHash = "")
        => new(documentId, version, contentHash ?? string.Empty);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return DocumentId;
        yield return Version;
        yield return ContentHash;
    }
}
