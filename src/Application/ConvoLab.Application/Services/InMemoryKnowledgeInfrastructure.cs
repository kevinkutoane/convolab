using ConvoLab.Domain.Knowledge.Aggregates;
using ConvoLab.Domain.Knowledge.Enums;
using ConvoLab.Domain.Knowledge.Interfaces;
using ConvoLab.Domain.Knowledge.ValueObjects;
using System.Collections.Concurrent;

namespace ConvoLab.Application.Services;

/// <summary>
/// In-memory KnowledgeSource repository used until persistent infrastructure
/// lands. Thread-safe; suitable for development, testing, and simulation.
/// </summary>
public class InMemoryKnowledgeSourceRepository : IKnowledgeSourceRepository
{
    private readonly ConcurrentDictionary<Guid, KnowledgeSource> _store = new();

    public Task<KnowledgeSource?> GetByIdAsync(KnowledgeSourceId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(id.Value, out var source) ? source : null);

    public Task<IReadOnlyList<KnowledgeSource>> GetByIdsAsync(IEnumerable<KnowledgeSourceId> ids, CancellationToken cancellationToken = default)
    {
        var results = ids
            .Select(id => _store.TryGetValue(id.Value, out var s) ? s : null)
            .Where(s => s is not null)
            .Cast<KnowledgeSource>()
            .ToList();
        return Task.FromResult<IReadOnlyList<KnowledgeSource>>(results);
    }

    public Task AddAsync(KnowledgeSource source, CancellationToken cancellationToken = default)
    {
        _store[source.Id.Value] = source;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(KnowledgeSource source, CancellationToken cancellationToken = default)
    {
        _store[source.Id.Value] = source;
        return Task.CompletedTask;
    }
}

/// <summary>In-memory KnowledgeCollection repository.</summary>
public class InMemoryKnowledgeCollectionRepository : IKnowledgeCollectionRepository
{
    private readonly ConcurrentDictionary<Guid, KnowledgeCollection> _store = new();

    public Task<KnowledgeCollection?> GetByIdAsync(KnowledgeCollectionId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(id.Value, out var c) ? c : null);

    public Task AddAsync(KnowledgeCollection collection, CancellationToken cancellationToken = default)
    {
        _store[collection.Id.Value] = collection;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(KnowledgeCollection collection, CancellationToken cancellationToken = default)
    {
        _store[collection.Id.Value] = collection;
        return Task.CompletedTask;
    }
}

/// <summary>In-memory KnowledgeConnector repository.</summary>
public class InMemoryKnowledgeConnectorRepository : IKnowledgeConnectorRepository
{
    private readonly ConcurrentDictionary<Guid, KnowledgeConnector> _store = new();

    public Task<KnowledgeConnector?> GetByIdAsync(KnowledgeConnectorId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(id.Value, out var c) ? c : null);

    public Task<IReadOnlyList<KnowledgeConnector>> GetBySourceIdAsync(KnowledgeSourceId sourceId, CancellationToken cancellationToken = default)
    {
        var results = _store.Values.Where(c => c.SourceId == sourceId).ToList();
        return Task.FromResult<IReadOnlyList<KnowledgeConnector>>(results);
    }

    public Task AddAsync(KnowledgeConnector connector, CancellationToken cancellationToken = default)
    {
        _store[connector.Id.Value] = connector;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(KnowledgeConnector connector, CancellationToken cancellationToken = default)
    {
        _store[connector.Id.Value] = connector;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Placeholder retriever that serves every strategy type and returns no results.
/// Real retrievers (keyword index, vector search, hybrid, graph) will be
/// registered by infrastructure and replace this at composition time.
/// </summary>
public class PlaceholderKnowledgeRetriever : IKnowledgeRetriever
{
    public IReadOnlyCollection<RetrievalStrategyType> SupportedStrategies { get; } =
        Enum.GetValues<RetrievalStrategyType>();

    public Task<IReadOnlyList<KnowledgeResult>> RetrieveAsync(KnowledgeQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<KnowledgeResult>>(new List<KnowledgeResult>());
}
