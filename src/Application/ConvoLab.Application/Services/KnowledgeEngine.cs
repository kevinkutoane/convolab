using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Domain.Knowledge.Aggregates;
using ConvoLab.Domain.Knowledge.Entities;
using ConvoLab.Domain.Knowledge.Enums;
using ConvoLab.Domain.Knowledge.Interfaces;
using ConvoLab.Domain.Knowledge.Policies;
using ConvoLab.Domain.Knowledge.ValueObjects;

namespace ConvoLab.Application.Services;

/// <summary>
/// The Knowledge Engine application service — orchestrates the knowledge domain
/// to expose business capabilities: register sources, publish knowledge, query,
/// rank, assemble packages, refresh sources, validate connectors, snapshot, and
/// archive.
///
/// Provider-independent by design: retrieval is delegated to IKnowledgeRetriever
/// implementations registered in infrastructure; the engine itself never touches
/// vector stores, embeddings, or external systems.
/// </summary>
public class KnowledgeEngine : IKnowledgeEngine
{
    private readonly IKnowledgeSourceRepository _sources;
    private readonly IKnowledgeCollectionRepository _collections;
    private readonly IKnowledgeConnectorRepository _connectors;
    private readonly IEnumerable<IKnowledgeRetriever> _retrievers;
    private readonly KnowledgeGovernancePolicy _governance;

    public KnowledgeEngine(
        IKnowledgeSourceRepository sources,
        IKnowledgeCollectionRepository collections,
        IKnowledgeConnectorRepository connectors,
        IEnumerable<IKnowledgeRetriever> retrievers,
        KnowledgeGovernancePolicy governance)
    {
        _sources = sources;
        _collections = collections;
        _connectors = connectors;
        _retrievers = retrievers;
        _governance = governance;
    }

    // ── Source registration & governance ────────────────────────────────

    public async Task<KnowledgeSourceId> RegisterSourceAsync(
        string name,
        KnowledgeSourceType sourceType,
        KnowledgeOwner owner,
        KnowledgePolicy? policy = null,
        KnowledgeMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var source = KnowledgeSource.Register(name, sourceType, owner, policy, metadata);
        _governance.EnsureSourceIsGoverned(source);
        await _sources.AddAsync(source, cancellationToken);
        return source.Id;
    }

    public async Task PublishKnowledgeAsync(
        KnowledgeSourceId sourceId,
        KnowledgeDocumentId documentId,
        CancellationToken cancellationToken = default)
    {
        var source = await GetSourceAsync(sourceId, cancellationToken);
        source.PublishDocument(documentId);
        await _sources.UpdateAsync(source, cancellationToken);
    }

    public async Task ArchiveKnowledgeAsync(
        KnowledgeSourceId sourceId,
        KnowledgeDocumentId documentId,
        CancellationToken cancellationToken = default)
    {
        var source = await GetSourceAsync(sourceId, cancellationToken);
        source.ArchiveDocument(documentId);
        await _sources.UpdateAsync(source, cancellationToken);
    }

    // ── Retrieval ───────────────────────────────────────────────────────

    public async Task<IReadOnlyList<KnowledgeResult>> QueryKnowledgeAsync(
        KnowledgeQuery query,
        CancellationToken cancellationToken = default)
    {
        var retriever = ResolveRetriever(query.Strategy.Type);
        var results = await retriever.RetrieveAsync(query, cancellationToken);
        return await RankResultsAsync(results, query.Strategy, cancellationToken);
    }

    public Task<IReadOnlyList<KnowledgeResult>> RankResultsAsync(
        IReadOnlyList<KnowledgeResult> results,
        KnowledgeRetrievalStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        // Domain ranking rules: filter below-threshold results, order by ranking
        // (highest relevance first), and cap at the strategy's max results.
        IReadOnlyList<KnowledgeResult> ranked = results
            .Where(r => r.Confidence >= strategy.MinConfidence)
            .OrderBy(r => r.Ranking)
            .Take(strategy.MaxResults)
            .ToList()
            .AsReadOnly();

        return Task.FromResult(ranked);
    }

    public async Task<KnowledgePackage> RetrievePackageAsync(
        KnowledgeQuery query,
        CancellationToken cancellationToken = default)
    {
        var ranked = await QueryKnowledgeAsync(query, cancellationToken);

        var package = KnowledgePackage.StartAssembly(query);
        foreach (var result in ranked)
        {
            package.AddResult(result);
        }
        package.Seal();

        return package;
    }

    // ── Connectors & synchronization ────────────────────────────────────

    public async Task RefreshSourceAsync(
        KnowledgeSourceId sourceId,
        CancellationToken cancellationToken = default)
    {
        var connectors = await _connectors.GetBySourceIdAsync(sourceId, cancellationToken);
        if (!connectors.Any())
            throw new InvalidOperationException($"No connector registered for source '{sourceId}'.");

        foreach (var connector in connectors.Where(c => c.Status is ConnectorStatus.Active or ConnectorStatus.Degraded))
        {
            connector.BeginSynchronization();
            // Actual synchronization is an infrastructure concern executed
            // asynchronously; the domain records intent and state transitions.
            await _connectors.UpdateAsync(connector, cancellationToken);
        }
    }

    public async Task<bool> ValidateConnectorAsync(
        KnowledgeConnectorId connectorId,
        CancellationToken cancellationToken = default)
    {
        var connector = await _connectors.GetByIdAsync(connectorId, cancellationToken)
            ?? throw new InvalidOperationException($"Connector '{connectorId}' not found.");

        connector.BeginValidation();
        // Infrastructure performs the real reachability/permission checks and
        // completes or fails validation; the placeholder path completes it.
        connector.CompleteValidation();
        await _connectors.UpdateAsync(connector, cancellationToken);

        return connector.Status == ConnectorStatus.Active;
    }

    // ── Versioning & snapshots ──────────────────────────────────────────

    public async Task<KnowledgeSnapshotId> CreateSnapshotAsync(
        KnowledgeCollectionId collectionId,
        string label,
        CancellationToken cancellationToken = default)
    {
        var collection = await _collections.GetByIdAsync(collectionId, cancellationToken)
            ?? throw new InvalidOperationException($"Collection '{collectionId}' not found.");

        var sources = await _sources.GetByIdsAsync(collection.SourceIds, cancellationToken);
        var entries = sources
            .SelectMany(s => s.GetRetrievableDocuments())
            .Select(d => SnapshotEntry.Create(d.Id, d.Version, d.ContentHash))
            .ToList();

        var snapshot = collection.CaptureSnapshot(label, entries);
        await _collections.UpdateAsync(collection, cancellationToken);

        return snapshot.Id;
    }

    public async Task PublishVersionAsync(
        KnowledgeSourceId sourceId,
        KnowledgeDocumentId documentId,
        string newContentHash,
        CancellationToken cancellationToken = default)
    {
        var source = await GetSourceAsync(sourceId, cancellationToken);
        source.RegisterContentChange(documentId, newContentHash);
        await _sources.UpdateAsync(source, cancellationToken);
    }

    // ── Legacy capability (Capability 1–4 era; retained for compatibility) ──

    public Task<KnowledgeBaseId> CreateKnowledgeBaseAsync(string name, string description, CancellationToken cancellationToken = default)
        => Task.FromResult(KnowledgeBaseId.CreateUnique());

    public Task<KnowledgeItemId> AddKnowledgeItemAsync(KnowledgeBaseId baseId, string title, string content, string source, CancellationToken cancellationToken = default)
        => Task.FromResult(KnowledgeItemId.CreateUnique());

    public Task<IReadOnlyList<KnowledgeItem>> SearchKnowledgeAsync(KnowledgeBaseId baseId, string query, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<KnowledgeItem>>(new List<KnowledgeItem>());

    public Task<bool> UpdateKnowledgeItemContentAsync(KnowledgeItemId itemId, string newContent, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    // ── Helpers ─────────────────────────────────────────────────────────

    private async Task<KnowledgeSource> GetSourceAsync(KnowledgeSourceId sourceId, CancellationToken ct)
        => await _sources.GetByIdAsync(sourceId, ct)
            ?? throw new InvalidOperationException($"Knowledge source '{sourceId}' not found.");

    private IKnowledgeRetriever ResolveRetriever(RetrievalStrategyType strategy)
        => _retrievers.FirstOrDefault(r => r.SupportedStrategies.Contains(strategy))
            ?? throw new InvalidOperationException($"No retriever registered for strategy '{strategy}'.");
}
