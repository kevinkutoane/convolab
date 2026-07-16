using ConvoLab.Domain.Common;
using ConvoLab.Domain.Knowledge.Entities;
using ConvoLab.Domain.Knowledge.Enums;
using ConvoLab.Domain.Knowledge.Events;
using ConvoLab.Domain.Knowledge.ValueObjects;

namespace ConvoLab.Domain.Knowledge.Aggregates;

/// <summary>
/// The KnowledgeCollection aggregate root. A logical, governed grouping of knowledge
/// sources scoped to a business purpose (e.g. "Claims Policies", "Product FAQs").
/// Collections are the unit that workflows scope their queries to, and the unit
/// snapshots are captured against.
///
/// Core invariants:
/// - A collection must have an owner and a policy.
/// - A source can only be linked once.
/// - Snapshots are immutable once captured.
/// </summary>
public class KnowledgeCollection : BaseAggregateRoot<KnowledgeCollectionId>
{
    public string Name { get; private set; }
    public string Description { get; private set; }
    public KnowledgeOwner Owner { get; private set; }
    public KnowledgePolicy Policy { get; private set; }
    public KnowledgeLifecycleStatus Status { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<KnowledgeSourceId> _sourceIds = new();
    private readonly List<KnowledgeSnapshot> _snapshots = new();

    public IReadOnlyCollection<KnowledgeSourceId> SourceIds => _sourceIds.AsReadOnly();
    public IReadOnlyCollection<KnowledgeSnapshot> Snapshots => _snapshots.AsReadOnly();

    private KnowledgeCollection() { Name = null!; Description = null!; Owner = null!; Policy = null!; } // For EF Core

    private KnowledgeCollection(
        KnowledgeCollectionId id,
        string name,
        string description,
        KnowledgeOwner owner,
        KnowledgePolicy policy) : base(id)
    {
        Name = name;
        Description = description;
        Owner = owner;
        Policy = policy;
        Status = KnowledgeLifecycleStatus.Draft;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new KnowledgeCollectionCreatedEvent(id, name, owner.OwnerId));
    }

    public static KnowledgeCollection Create(
        string name,
        string description,
        KnowledgeOwner owner,
        KnowledgePolicy? policy = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Collection name cannot be empty.", nameof(name));

        return new KnowledgeCollection(
            KnowledgeCollectionId.CreateUnique(),
            name,
            description ?? string.Empty,
            owner ?? throw new ArgumentNullException(nameof(owner)),
            policy ?? KnowledgePolicy.Default());
    }

    #region Source Membership

    public void LinkSource(KnowledgeSourceId sourceId)
    {
        if (_sourceIds.Contains(sourceId))
            throw new InvalidOperationException("Source is already linked to this collection.");
        _sourceIds.Add(sourceId);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UnlinkSource(KnowledgeSourceId sourceId)
    {
        if (!_sourceIds.Remove(sourceId))
            throw new InvalidOperationException("Source is not linked to this collection.");
        UpdatedAt = DateTime.UtcNow;
    }

    #endregion

    #region Snapshots

    /// <summary>
    /// Captures an immutable snapshot of the published documents supplied by the
    /// application layer, enabling reproducible retrieval and audit.
    /// </summary>
    public KnowledgeSnapshot CaptureSnapshot(string label, IEnumerable<SnapshotEntry> entries)
    {
        var snapshot = KnowledgeSnapshot.Capture(Id, label, entries);
        _snapshots.Add(snapshot);
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new KnowledgeSnapshotCreatedEvent(snapshot.Id, Id, label, snapshot.DocumentCount));
        return snapshot;
    }

    #endregion

    #region Lifecycle & Governance

    public void Publish()
    {
        if (!_sourceIds.Any())
            throw new InvalidOperationException("A collection must have at least one linked source before publishing.");
        Status = KnowledgeLifecycleStatus.Published;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deprecate()
    {
        if (Status != KnowledgeLifecycleStatus.Published)
            throw new InvalidOperationException("Only a Published collection can be deprecated.");
        Status = KnowledgeLifecycleStatus.Deprecated;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        if (Status == KnowledgeLifecycleStatus.Published)
            throw new InvalidOperationException("Cannot archive a Published collection. Deprecate it first.");
        Status = KnowledgeLifecycleStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }

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

    public bool IsQueryable => Status == KnowledgeLifecycleStatus.Published;

    #endregion
}
