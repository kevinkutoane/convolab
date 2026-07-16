using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Intelligence.ValueObjects;

/// <summary>
/// The immutable output of the Execution Planner. A plan captures every HOW
/// decision — provider, model, retry, fallback, streaming, tool strategy, and
/// estimates — before a single token is executed. Plans are never mutated;
/// replanning produces a new plan.
/// </summary>
public class ExecutionPlan : ValueObject
{
    public ExecutionPlanId Id { get; private set; } = null!;
    public IntelligenceProviderId ProviderId { get; private set; } = null!;
    public IntelligenceModelId ModelId { get; private set; } = null!;
    public string ProviderName { get; private set; } = string.Empty;
    public string ModelName { get; private set; } = string.Empty;
    public ExecutionRetryPolicy RetryPolicy { get; private set; } = ExecutionRetryPolicy.Default();
    public ExecutionFallbackPolicy FallbackPolicy { get; private set; } = ExecutionFallbackPolicy.None();
    public bool UseStreaming { get; private set; }
    public bool AllowTools { get; private set; }
    public ExecutionUsage EstimatedUsage { get; private set; } = ExecutionUsage.Zero();
    public ExecutionCost EstimatedCost { get; private set; } = ExecutionCost.Zero();
    public TimeSpan EstimatedLatency { get; private set; }
    public ExecutionPolicy Policy { get; private set; } = ExecutionPolicy.Default();
    public DateTime PlannedAt { get; private set; }

    private ExecutionPlan() { } // For EF Core

    private ExecutionPlan(
        ExecutionPlanId id,
        IntelligenceProviderId providerId,
        IntelligenceModelId modelId,
        string providerName,
        string modelName,
        ExecutionRetryPolicy retryPolicy,
        ExecutionFallbackPolicy fallbackPolicy,
        bool useStreaming,
        bool allowTools,
        ExecutionUsage estimatedUsage,
        ExecutionCost estimatedCost,
        TimeSpan estimatedLatency,
        ExecutionPolicy policy)
    {
        Id = id;
        ProviderId = providerId;
        ModelId = modelId;
        ProviderName = providerName;
        ModelName = modelName;
        RetryPolicy = retryPolicy;
        FallbackPolicy = fallbackPolicy;
        UseStreaming = useStreaming;
        AllowTools = allowTools;
        EstimatedUsage = estimatedUsage;
        EstimatedCost = estimatedCost;
        EstimatedLatency = estimatedLatency;
        Policy = policy;
        PlannedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates an immutable plan. The estimated cost must not exceed the
    /// policy's per-execution ceiling — a plan that violates budget is a
    /// contradiction and is rejected at construction.
    /// </summary>
    public static ExecutionPlan Create(
        IntelligenceProviderId providerId,
        IntelligenceModelId modelId,
        string providerName,
        string modelName,
        ExecutionRetryPolicy retryPolicy,
        ExecutionFallbackPolicy fallbackPolicy,
        bool useStreaming,
        bool allowTools,
        ExecutionUsage estimatedUsage,
        ExecutionCost estimatedCost,
        TimeSpan estimatedLatency,
        ExecutionPolicy policy)
    {
        if (estimatedCost.Exceeds(policy.MaxCostPerExecution))
            throw new InvalidOperationException(
                $"Estimated cost {estimatedCost.Amount} {estimatedCost.Currency} exceeds the policy ceiling " +
                $"{policy.MaxCostPerExecution.Amount} {policy.MaxCostPerExecution.Currency}.");

        if (useStreaming && !policy.AllowStreaming)
            throw new InvalidOperationException("Plan requests streaming but the execution policy forbids it.");

        if (allowTools && !policy.AllowTools)
            throw new InvalidOperationException("Plan requests tools but the execution policy forbids them.");

        if (fallbackPolicy.HasFallback && !policy.AllowFallback)
            throw new InvalidOperationException("Plan defines fallbacks but the execution policy forbids fallback.");

        return new ExecutionPlan(
            ExecutionPlanId.CreateUnique(), providerId, modelId, providerName, modelName,
            retryPolicy, fallbackPolicy, useStreaming, allowTools,
            estimatedUsage, estimatedCost, estimatedLatency, policy);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Id;
    }
}
