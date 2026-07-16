using ConvoLab.Domain.Common;
using ConvoLab.Domain.Intelligence.Enums;

namespace ConvoLab.Domain.Intelligence.ValueObjects;

/// <summary>
/// Token usage for an execution: input, output, cached, and reasoning tokens.
/// Immutable; combine usages with Add.
/// </summary>
public class ExecutionUsage : ValueObject
{
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public int CachedTokens { get; private set; }
    public int ReasoningTokens { get; private set; }
    public int TotalTokens => InputTokens + OutputTokens + ReasoningTokens;

    private ExecutionUsage() { } // For EF Core

    private ExecutionUsage(int input, int output, int cached, int reasoning)
    {
        if (input < 0 || output < 0 || cached < 0 || reasoning < 0)
            throw new ArgumentException("Token counts cannot be negative.");
        InputTokens = input;
        OutputTokens = output;
        CachedTokens = cached;
        ReasoningTokens = reasoning;
    }

    public static ExecutionUsage Create(int inputTokens, int outputTokens, int cachedTokens = 0, int reasoningTokens = 0)
        => new(inputTokens, outputTokens, cachedTokens, reasoningTokens);

    public static ExecutionUsage Zero() => new(0, 0, 0, 0);

    public ExecutionUsage Add(ExecutionUsage other)
        => new(InputTokens + other.InputTokens,
               OutputTokens + other.OutputTokens,
               CachedTokens + other.CachedTokens,
               ReasoningTokens + other.ReasoningTokens);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return InputTokens;
        yield return OutputTokens;
        yield return CachedTokens;
        yield return ReasoningTokens;
    }
}

/// <summary>
/// Monetary cost with explicit currency. Estimated vs actual is expressed by
/// the owning concept, not by this value object.
/// </summary>
public class ExecutionCost : ValueObject
{
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "USD";

    private ExecutionCost() { } // For EF Core

    private ExecutionCost(decimal amount, string currency)
    {
        if (amount < 0) throw new ArgumentException("Cost cannot be negative.");
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency is required.");
        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public static ExecutionCost Create(decimal amount, string currency = "USD") => new(amount, currency);
    public static ExecutionCost Zero(string currency = "USD") => new(0m, currency);

    public ExecutionCost Add(ExecutionCost other)
    {
        EnsureSameCurrency(other);
        return new(Amount + other.Amount, Currency);
    }

    public bool Exceeds(ExecutionCost other)
    {
        EnsureSameCurrency(other);
        return Amount > other.Amount;
    }

    private void EnsureSameCurrency(ExecutionCost other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Currency mismatch: {Currency} vs {other.Currency}.");
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}

/// <summary>
/// Per-model pricing used for cost estimation, expressed per 1,000 tokens.
/// A pricing card, not a billing system.
/// </summary>
public class ModelPricing : ValueObject
{
    public decimal InputPricePer1K { get; private set; }
    public decimal OutputPricePer1K { get; private set; }
    public decimal CachedInputPricePer1K { get; private set; }
    public string Currency { get; private set; } = "USD";

    private ModelPricing() { } // For EF Core

    private ModelPricing(decimal input, decimal output, decimal cached, string currency)
    {
        if (input < 0 || output < 0 || cached < 0)
            throw new ArgumentException("Prices cannot be negative.");
        InputPricePer1K = input;
        OutputPricePer1K = output;
        CachedInputPricePer1K = cached;
        Currency = currency.ToUpperInvariant();
    }

    public static ModelPricing Create(decimal inputPricePer1K, decimal outputPricePer1K, decimal cachedInputPricePer1K = 0m, string currency = "USD")
        => new(inputPricePer1K, outputPricePer1K, cachedInputPricePer1K, currency);

    public static ModelPricing Free() => new(0m, 0m, 0m, "USD");

    /// <summary>Estimates the cost of a given usage against this pricing card.</summary>
    public ExecutionCost EstimateCost(ExecutionUsage usage)
    {
        var billableInput = Math.Max(0, usage.InputTokens - usage.CachedTokens);
        var amount =
            (billableInput / 1000m) * InputPricePer1K +
            (usage.CachedTokens / 1000m) * CachedInputPricePer1K +
            ((usage.OutputTokens + usage.ReasoningTokens) / 1000m) * OutputPricePer1K;
        return ExecutionCost.Create(Math.Round(amount, 6), Currency);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return InputPricePer1K;
        yield return OutputPricePer1K;
        yield return CachedInputPricePer1K;
        yield return Currency;
    }
}

/// <summary>
/// Attribution of cost to platform dimensions: conversation, workflow, tenant,
/// and provider. Enables per-conversation and per-workflow cost reporting.
/// </summary>
public class CostAttribution : ValueObject
{
    public Guid? ConversationId { get; private set; }
    public Guid? WorkflowId { get; private set; }
    public Guid? TenantId { get; private set; }
    public IntelligenceProviderId? ProviderId { get; private set; }

    private CostAttribution() { } // For EF Core

    private CostAttribution(Guid? conversationId, Guid? workflowId, Guid? tenantId, IntelligenceProviderId? providerId)
    {
        ConversationId = conversationId;
        WorkflowId = workflowId;
        TenantId = tenantId;
        ProviderId = providerId;
    }

    public static CostAttribution Create(Guid? conversationId = null, Guid? workflowId = null, Guid? tenantId = null, IntelligenceProviderId? providerId = null)
        => new(conversationId, workflowId, tenantId, providerId);

    public static CostAttribution None() => new(null, null, null, null);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return ConversationId ?? Guid.Empty;
        yield return WorkflowId ?? Guid.Empty;
        yield return TenantId ?? Guid.Empty;
        yield return ProviderId?.Value ?? Guid.Empty;
    }
}

/// <summary>Latency target that execution planning must respect.</summary>
public class LatencyTarget : ValueObject
{
    public TimeSpan Target { get; private set; }
    public TimeSpan Maximum { get; private set; }

    private LatencyTarget() { } // For EF Core

    private LatencyTarget(TimeSpan target, TimeSpan maximum)
    {
        if (target <= TimeSpan.Zero) throw new ArgumentException("Latency target must be positive.");
        if (maximum < target) throw new ArgumentException("Maximum latency cannot be below the target.");
        Target = target;
        Maximum = maximum;
    }

    public static LatencyTarget Create(TimeSpan target, TimeSpan? maximum = null)
        => new(target, maximum ?? target * 3);

    public static LatencyTarget Default() => new(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));

    public bool IsSatisfiedBy(TimeSpan observed) => observed <= Maximum;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Target;
        yield return Maximum;
    }
}
