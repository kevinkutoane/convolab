using ConvoLab.Domain.Knowledge.Aggregates;
using ConvoLab.Domain.Knowledge.Entities;
using ConvoLab.Domain.Knowledge.Enums;
using ConvoLab.Domain.Knowledge.ValueObjects;

namespace ConvoLab.Application.Common.Interfaces;

/// <summary>
/// The Knowledge Engine — the single source of truth for enterprise knowledge
/// retrieval. Discovers, governs, retrieves, and delivers knowledge to
/// conversational workflows as governed KnowledgePackages.
///
/// Consumers (Conversation Engine, Workflow Engine) interact through this
/// interface only and never learn where knowledge physically lives.
/// </summary>
public interface IKnowledgeEngine
{
    // ── Source registration & governance ────────────────────────────────

    /// <summary>Registers a new enterprise knowledge source under governance.</summary>
    Task<KnowledgeSourceId> RegisterSourceAsync(
        string name,
        KnowledgeSourceType sourceType,
        KnowledgeOwner owner,
        KnowledgePolicy? policy = null,
        KnowledgeMetadata? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>Publishes a document version, making it retrievable platform-wide.</summary>
    Task PublishKnowledgeAsync(
        KnowledgeSourceId sourceId,
        KnowledgeDocumentId documentId,
        CancellationToken cancellationToken = default);

    /// <summary>Archives a document, removing it from the retrievable estate.</summary>
    Task ArchiveKnowledgeAsync(
        KnowledgeSourceId sourceId,
        KnowledgeDocumentId documentId,
        CancellationToken cancellationToken = default);

    // ── Retrieval ───────────────────────────────────────────────────────

    /// <summary>Executes a governed knowledge query and returns ranked results.</summary>
    Task<IReadOnlyList<KnowledgeResult>> QueryKnowledgeAsync(
        KnowledgeQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Ranks a candidate result set according to the strategy's ranking rules.</summary>
    Task<IReadOnlyList<KnowledgeResult>> RankResultsAsync(
        IReadOnlyList<KnowledgeResult> results,
        KnowledgeRetrievalStrategy strategy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a query and assembles a sealed, governed KnowledgePackage —
    /// the only artifact the Prompt Engine may consume.
    /// </summary>
    Task<KnowledgePackage> RetrievePackageAsync(
        KnowledgeQuery query,
        CancellationToken cancellationToken = default);

    // ── Connectors & synchronization ────────────────────────────────────

    /// <summary>Triggers a refresh (synchronization) of a source via its connector.</summary>
    Task RefreshSourceAsync(
        KnowledgeSourceId sourceId,
        CancellationToken cancellationToken = default);

    /// <summary>Validates a connector's reachability, permissions, and capabilities.</summary>
    Task<bool> ValidateConnectorAsync(
        KnowledgeConnectorId connectorId,
        CancellationToken cancellationToken = default);

    // ── Versioning & snapshots ──────────────────────────────────────────

    /// <summary>Captures an immutable snapshot of a collection's published knowledge.</summary>
    Task<KnowledgeSnapshotId> CreateSnapshotAsync(
        KnowledgeCollectionId collectionId,
        string label,
        CancellationToken cancellationToken = default);

    /// <summary>Publishes a new version of a document after a content change.</summary>
    Task PublishVersionAsync(
        KnowledgeSourceId sourceId,
        KnowledgeDocumentId documentId,
        string newContentHash,
        CancellationToken cancellationToken = default);

    // ── Legacy capability (Capability 1–4 era; retained for compatibility) ──

    Task<KnowledgeBaseId> CreateKnowledgeBaseAsync(string name, string description, CancellationToken cancellationToken = default);
    Task<KnowledgeItemId> AddKnowledgeItemAsync(KnowledgeBaseId baseId, string title, string content, string source, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeItem>> SearchKnowledgeAsync(KnowledgeBaseId baseId, string query, CancellationToken cancellationToken = default);
    Task<bool> UpdateKnowledgeItemContentAsync(KnowledgeItemId itemId, string newContent, CancellationToken cancellationToken = default);
}
