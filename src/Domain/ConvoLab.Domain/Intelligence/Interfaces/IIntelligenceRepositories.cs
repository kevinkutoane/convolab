using ConvoLab.Domain.Intelligence.Aggregates;
using ConvoLab.Domain.Intelligence.ValueObjects;

namespace ConvoLab.Domain.Intelligence.Interfaces;

/// <summary>Repository for the provider catalogue.</summary>
public interface IIntelligenceProviderRepository
{
    Task<IntelligenceProvider?> GetByIdAsync(IntelligenceProviderId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IntelligenceProvider>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(IntelligenceProvider provider, CancellationToken cancellationToken = default);
    Task UpdateAsync(IntelligenceProvider provider, CancellationToken cancellationToken = default);
}

/// <summary>Repository for execution requests.</summary>
public interface IExecutionRequestRepository
{
    Task<ExecutionRequest?> GetByIdAsync(ExecutionRequestId id, CancellationToken cancellationToken = default);
    Task AddAsync(ExecutionRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(ExecutionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Repository for execution budgets.</summary>
public interface IExecutionBudgetRepository
{
    Task<ExecutionBudget?> GetByIdAsync(ExecutionBudgetId id, CancellationToken cancellationToken = default);
    Task<ExecutionBudget?> FindByScopeAsync(CostAttribution scope, CancellationToken cancellationToken = default);
    Task AddAsync(ExecutionBudget budget, CancellationToken cancellationToken = default);
    Task UpdateAsync(ExecutionBudget budget, CancellationToken cancellationToken = default);
}

/// <summary>
/// The provider-independent execution port. Infrastructure implements one
/// adapter per provider family; the domain and application layers only ever
/// see this contract. This is the ONLY seam through which real providers
/// (OpenAI, Gemini, Anthropic, internal models...) will ever be attached.
/// </summary>
public interface IIntelligenceExecutor
{
    /// <summary>Provider kinds this executor can serve.</summary>
    IReadOnlyCollection<Enums.ProviderKind> SupportedProviders { get; }

    /// <summary>
    /// Executes a planned workload and returns the normalized result.
    /// Implementations must translate provider wire formats into
    /// ExecutionResult and never leak SDK types.
    /// </summary>
    Task<ExecutionResult> ExecuteAsync(
        ExecutionRequest request,
        string renderedPrompt,
        CancellationToken cancellationToken = default);
}
