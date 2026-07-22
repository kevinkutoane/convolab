using System.Collections.Concurrent;
using ConvoLab.Domain.Intelligence.Aggregates;
using ConvoLab.Domain.Intelligence.Interfaces;
using ConvoLab.Domain.Intelligence.ValueObjects;

namespace ConvoLab.Application.Services;

/// <summary>Configuration-derived provider catalogue rebuilt idempotently at startup.</summary>
public sealed class RuntimeIntelligenceProviderRepository : IIntelligenceProviderRepository
{
    private readonly ConcurrentDictionary<Guid, IntelligenceProvider> store = new();
    public Task<IntelligenceProvider?> GetByIdAsync(IntelligenceProviderId id, CancellationToken cancellationToken = default) => Task.FromResult(store.GetValueOrDefault(id.Value));
    public Task<IReadOnlyList<IntelligenceProvider>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<IntelligenceProvider>>(store.Values.ToList());
    public Task AddAsync(IntelligenceProvider provider, CancellationToken cancellationToken = default) { store[provider.Id.Value] = provider; return Task.CompletedTask; }
    public Task UpdateAsync(IntelligenceProvider provider, CancellationToken cancellationToken = default) { store[provider.Id.Value] = provider; return Task.CompletedTask; }
}

/// <summary>Process-local state used only while a provider execution is active.</summary>
public sealed class RuntimeExecutionRequestRepository : IExecutionRequestRepository
{
    private readonly ConcurrentDictionary<Guid, ExecutionRequest> store = new();
    public Task<ExecutionRequest?> GetByIdAsync(ExecutionRequestId id, CancellationToken cancellationToken = default) => Task.FromResult(store.GetValueOrDefault(id.Value));
    public Task AddAsync(ExecutionRequest request, CancellationToken cancellationToken = default) { store[request.Id.Value] = request; return Task.CompletedTask; }
    public Task UpdateAsync(ExecutionRequest request, CancellationToken cancellationToken = default) { store[request.Id.Value] = request; return Task.CompletedTask; }
}

/// <summary>Runtime admission state rebuilt from configured ZAR budgets at startup.</summary>
public sealed class RuntimeExecutionBudgetRepository : IExecutionBudgetRepository
{
    private readonly ConcurrentDictionary<Guid, ExecutionBudget> store = new();
    public Task<ExecutionBudget?> GetByIdAsync(ExecutionBudgetId id, CancellationToken cancellationToken = default) => Task.FromResult(store.GetValueOrDefault(id.Value));
    public Task<ExecutionBudget?> FindByScopeAsync(CostAttribution scope, CancellationToken cancellationToken = default) => Task.FromResult(store.Values.FirstOrDefault(item => item.Scope == scope));
    public Task AddAsync(ExecutionBudget budget, CancellationToken cancellationToken = default) { store[budget.Id.Value] = budget; return Task.CompletedTask; }
    public Task UpdateAsync(ExecutionBudget budget, CancellationToken cancellationToken = default) { store[budget.Id.Value] = budget; return Task.CompletedTask; }
}
