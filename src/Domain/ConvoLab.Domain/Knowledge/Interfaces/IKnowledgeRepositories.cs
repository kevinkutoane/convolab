using ConvoLab.Domain.Knowledge.Aggregates;
using ConvoLab.Domain.Knowledge.ValueObjects;

namespace ConvoLab.Domain.Knowledge.Interfaces;

/// <summary>Persistence abstraction for KnowledgeSource aggregates.</summary>
public interface IKnowledgeSourceRepository
{
    Task<KnowledgeSource?> GetByIdAsync(KnowledgeSourceId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeSource>> GetByIdsAsync(IEnumerable<KnowledgeSourceId> ids, CancellationToken cancellationToken = default);
    Task AddAsync(KnowledgeSource source, CancellationToken cancellationToken = default);
    Task UpdateAsync(KnowledgeSource source, CancellationToken cancellationToken = default);
}

/// <summary>Persistence abstraction for KnowledgeCollection aggregates.</summary>
public interface IKnowledgeCollectionRepository
{
    Task<KnowledgeCollection?> GetByIdAsync(KnowledgeCollectionId id, CancellationToken cancellationToken = default);
    Task AddAsync(KnowledgeCollection collection, CancellationToken cancellationToken = default);
    Task UpdateAsync(KnowledgeCollection collection, CancellationToken cancellationToken = default);
}

/// <summary>Persistence abstraction for KnowledgeConnector aggregates.</summary>
public interface IKnowledgeConnectorRepository
{
    Task<KnowledgeConnector?> GetByIdAsync(KnowledgeConnectorId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeConnector>> GetBySourceIdAsync(KnowledgeSourceId sourceId, CancellationToken cancellationToken = default);
    Task AddAsync(KnowledgeConnector connector, CancellationToken cancellationToken = default);
    Task UpdateAsync(KnowledgeConnector connector, CancellationToken cancellationToken = default);
}

/// <summary>
/// The retrieval abstraction the domain exposes to infrastructure. Implementations
/// (keyword index, vector store, hybrid search, graph traversal) live entirely
/// outside the domain — the domain only speaks in queries and results.
/// </summary>
public interface IKnowledgeRetriever
{
    /// <summary>The strategy types this retriever can serve.</summary>
    IReadOnlyCollection<Enums.RetrievalStrategyType> SupportedStrategies { get; }

    /// <summary>Executes the query against the retrievable knowledge estate.</summary>
    Task<IReadOnlyList<KnowledgeResult>> RetrieveAsync(KnowledgeQuery query, CancellationToken cancellationToken = default);
}
