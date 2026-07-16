using ConvoLab.Domain.Common;
using ConvoLab.Domain.Knowledge.ValueObjects;

namespace ConvoLab.Domain.Knowledge.Entities;

/// <summary>
/// An immutable point-in-time snapshot of a knowledge collection: which documents
/// (and versions) were published at a given moment. Snapshots enable reproducible
/// retrieval, audits, and rollback of the knowledge estate.
/// </summary>
public class KnowledgeSnapshot : BaseEntity<KnowledgeSnapshotId>
{
    public KnowledgeCollectionId CollectionId { get; private set; }
    public string Label { get; private set; }
    public DateTime CapturedAt { get; private set; }

    private readonly List<SnapshotEntry> _entries = new();
    public IReadOnlyCollection<SnapshotEntry> Entries => _entries.AsReadOnly();

    private KnowledgeSnapshot() { CollectionId = null!; Label = null!; } // For EF Core

    private KnowledgeSnapshot(
        KnowledgeSnapshotId id,
        KnowledgeCollectionId collectionId,
        string label,
        IEnumerable<SnapshotEntry> entries) : base(id)
    {
        CollectionId = collectionId;
        Label = label;
        CapturedAt = DateTime.UtcNow;
        _entries.AddRange(entries);
    }

    public static KnowledgeSnapshot Capture(
        KnowledgeCollectionId collectionId,
        string label,
        IEnumerable<SnapshotEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Snapshot label cannot be empty.", nameof(label));

        var entryList = entries?.ToList() ?? throw new ArgumentNullException(nameof(entries));
        if (!entryList.Any())
            throw new InvalidOperationException("A snapshot must capture at least one published document.");

        return new KnowledgeSnapshot(KnowledgeSnapshotId.CreateUnique(), collectionId, label, entryList);
    }

    public int DocumentCount => _entries.Count;
}

/// <summary>A single (document, version) pair captured in a snapshot.</summary>

