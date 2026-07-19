using System.Collections.Concurrent;
using ConvoLab.Domain.Intelligence.Aggregates;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.Interfaces;
using ConvoLab.Domain.Intelligence.ValueObjects;

namespace ConvoLab.Application.Services;

/// <summary>
/// In-memory placeholder infrastructure for the Intelligence Engine. These
/// are development stand-ins: real persistence (EF Core) and real provider
/// adapters replace them without touching the domain or application layers.
/// </summary>
public class InMemoryIntelligenceProviderRepository : IIntelligenceProviderRepository
{
    private readonly ConcurrentDictionary<Guid, IntelligenceProvider> _store = new();

    public Task<IntelligenceProvider?> GetByIdAsync(IntelligenceProviderId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(id.Value, out var provider) ? provider : null);

    public Task<IReadOnlyList<IntelligenceProvider>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<IntelligenceProvider>>(_store.Values.ToList());

    public Task AddAsync(IntelligenceProvider provider, CancellationToken cancellationToken = default)
    {
        _store[provider.Id.Value] = provider;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(IntelligenceProvider provider, CancellationToken cancellationToken = default)
    {
        _store[provider.Id.Value] = provider;
        return Task.CompletedTask;
    }
}

/// <summary>In-memory execution request store.</summary>
public class InMemoryExecutionRequestRepository : IExecutionRequestRepository
{
    private readonly ConcurrentDictionary<Guid, ExecutionRequest> _store = new();

    public Task<ExecutionRequest?> GetByIdAsync(ExecutionRequestId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(id.Value, out var request) ? request : null);

    public Task AddAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        _store[request.Id.Value] = request;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        _store[request.Id.Value] = request;
        return Task.CompletedTask;
    }
}

/// <summary>In-memory budget store.</summary>
public class InMemoryExecutionBudgetRepository : IExecutionBudgetRepository
{
    private readonly ConcurrentDictionary<Guid, ExecutionBudget> _store = new();

    public Task<ExecutionBudget?> GetByIdAsync(ExecutionBudgetId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(id.Value, out var budget) ? budget : null);

    public Task<ExecutionBudget?> FindByScopeAsync(CostAttribution scope, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.FirstOrDefault(b => b.Scope == scope));

    public Task AddAsync(ExecutionBudget budget, CancellationToken cancellationToken = default)
    {
        _store[budget.Id.Value] = budget;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ExecutionBudget budget, CancellationToken cancellationToken = default)
    {
        _store[budget.Id.Value] = budget;
        return Task.CompletedTask;
    }
}
