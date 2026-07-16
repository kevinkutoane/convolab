using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Knowledge.ValueObjects;

/// <summary>Strongly-typed identifier for a KnowledgeSource aggregate.</summary>
public class KnowledgeSourceId : ValueObject
{
    public Guid Value { get; private set; }
    private KnowledgeSourceId(Guid value) => Value = value;
    private KnowledgeSourceId() { } // For EF Core

    public static KnowledgeSourceId CreateUnique() => new(Guid.NewGuid());
    public static KnowledgeSourceId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("KnowledgeSourceId cannot be empty.", nameof(value));
        return new KnowledgeSourceId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
    public static implicit operator Guid(KnowledgeSourceId id) => id.Value;
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed identifier for a KnowledgeCollection aggregate.</summary>
public class KnowledgeCollectionId : ValueObject
{
    public Guid Value { get; private set; }
    private KnowledgeCollectionId(Guid value) => Value = value;
    private KnowledgeCollectionId() { } // For EF Core

    public static KnowledgeCollectionId CreateUnique() => new(Guid.NewGuid());
    public static KnowledgeCollectionId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("KnowledgeCollectionId cannot be empty.", nameof(value));
        return new KnowledgeCollectionId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
    public static implicit operator Guid(KnowledgeCollectionId id) => id.Value;
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed identifier for a KnowledgeDocument entity.</summary>
public class KnowledgeDocumentId : ValueObject
{
    public Guid Value { get; private set; }
    private KnowledgeDocumentId(Guid value) => Value = value;
    private KnowledgeDocumentId() { } // For EF Core

    public static KnowledgeDocumentId CreateUnique() => new(Guid.NewGuid());
    public static KnowledgeDocumentId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("KnowledgeDocumentId cannot be empty.", nameof(value));
        return new KnowledgeDocumentId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
    public static implicit operator Guid(KnowledgeDocumentId id) => id.Value;
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed identifier for a KnowledgeChunk entity.</summary>
public class KnowledgeChunkId : ValueObject
{
    public Guid Value { get; private set; }
    private KnowledgeChunkId(Guid value) => Value = value;
    private KnowledgeChunkId() { } // For EF Core

    public static KnowledgeChunkId CreateUnique() => new(Guid.NewGuid());
    public static KnowledgeChunkId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("KnowledgeChunkId cannot be empty.", nameof(value));
        return new KnowledgeChunkId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
    public static implicit operator Guid(KnowledgeChunkId id) => id.Value;
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed identifier for a KnowledgeQuery value object.</summary>
public class KnowledgeQueryId : ValueObject
{
    public Guid Value { get; private set; }
    private KnowledgeQueryId(Guid value) => Value = value;
    private KnowledgeQueryId() { } // For EF Core

    public static KnowledgeQueryId CreateUnique() => new(Guid.NewGuid());
    public static KnowledgeQueryId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("KnowledgeQueryId cannot be empty.", nameof(value));
        return new KnowledgeQueryId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
    public static implicit operator Guid(KnowledgeQueryId id) => id.Value;
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed identifier for a KnowledgePackage aggregate.</summary>
public class KnowledgePackageId : ValueObject
{
    public Guid Value { get; private set; }
    private KnowledgePackageId(Guid value) => Value = value;
    private KnowledgePackageId() { } // For EF Core

    public static KnowledgePackageId CreateUnique() => new(Guid.NewGuid());
    public static KnowledgePackageId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("KnowledgePackageId cannot be empty.", nameof(value));
        return new KnowledgePackageId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
    public static implicit operator Guid(KnowledgePackageId id) => id.Value;
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed identifier for a KnowledgeSnapshot entity.</summary>
public class KnowledgeSnapshotId : ValueObject
{
    public Guid Value { get; private set; }
    private KnowledgeSnapshotId(Guid value) => Value = value;
    private KnowledgeSnapshotId() { } // For EF Core

    public static KnowledgeSnapshotId CreateUnique() => new(Guid.NewGuid());
    public static KnowledgeSnapshotId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("KnowledgeSnapshotId cannot be empty.", nameof(value));
        return new KnowledgeSnapshotId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
    public static implicit operator Guid(KnowledgeSnapshotId id) => id.Value;
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed identifier for a KnowledgeConnector aggregate.</summary>
public class KnowledgeConnectorId : ValueObject
{
    public Guid Value { get; private set; }
    private KnowledgeConnectorId(Guid value) => Value = value;
    private KnowledgeConnectorId() { } // For EF Core

    public static KnowledgeConnectorId CreateUnique() => new(Guid.NewGuid());
    public static KnowledgeConnectorId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("KnowledgeConnectorId cannot be empty.", nameof(value));
        return new KnowledgeConnectorId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
    public static implicit operator Guid(KnowledgeConnectorId id) => id.Value;
    public override string ToString() => Value.ToString();
}
