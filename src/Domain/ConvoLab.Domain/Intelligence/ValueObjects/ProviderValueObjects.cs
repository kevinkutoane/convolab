using ConvoLab.Domain.Common;
using ConvoLab.Domain.Intelligence.Enums;

namespace ConvoLab.Domain.Intelligence.ValueObjects;

/// <summary>
/// The set of capabilities a model (or provider) offers. Capability matching
/// is set containment: a requirement is satisfied when every required
/// capability is present.
/// </summary>
public class CapabilitySet : ValueObject
{
    private readonly List<IntelligenceCapability> _capabilities = new();
    public IReadOnlyCollection<IntelligenceCapability> Capabilities => _capabilities.AsReadOnly();

    private CapabilitySet() { } // For EF Core

    private CapabilitySet(IEnumerable<IntelligenceCapability> capabilities)
    {
        _capabilities = capabilities.Distinct().OrderBy(c => c).ToList();
    }

    public static CapabilitySet Of(params IntelligenceCapability[] capabilities) => new(capabilities);
    public static CapabilitySet From(IEnumerable<IntelligenceCapability> capabilities) => new(capabilities);
    public static CapabilitySet Empty() => new(Enumerable.Empty<IntelligenceCapability>());

    public bool Supports(IntelligenceCapability capability) => _capabilities.Contains(capability);

    /// <summary>True when this set satisfies every capability in the requirement.</summary>
    public bool Satisfies(CapabilitySet required) => required._capabilities.All(_capabilities.Contains);

    public CapabilitySet With(IntelligenceCapability capability)
        => new(_capabilities.Append(capability));

    protected override IEnumerable<object> GetEqualityComponents()
        => _capabilities.Cast<object>();
}

/// <summary>
/// Rate limits granted by a provider. Zero means unlimited/unknown.
/// </summary>
public class RateLimitWindow : ValueObject
{
    public int RequestsPerMinute { get; private set; }
    public int TokensPerMinute { get; private set; }

    private RateLimitWindow() { } // For EF Core

    private RateLimitWindow(int rpm, int tpm)
    {
        if (rpm < 0 || tpm < 0) throw new ArgumentException("Rate limits cannot be negative.");
        RequestsPerMinute = rpm;
        TokensPerMinute = tpm;
    }

    public static RateLimitWindow Create(int requestsPerMinute, int tokensPerMinute) => new(requestsPerMinute, tokensPerMinute);
    public static RateLimitWindow Unlimited() => new(0, 0);

    public bool AllowsRequests(int observedRequestsPerMinute)
        => RequestsPerMinute == 0 || observedRequestsPerMinute < RequestsPerMinute;

    public bool AllowsTokens(int observedTokensPerMinute)
        => TokensPerMinute == 0 || observedTokensPerMinute < TokensPerMinute;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return RequestsPerMinute;
        yield return TokensPerMinute;
    }
}

/// <summary>
/// Point-in-time provider health: availability, latency, error rate, capacity,
/// and circuit state. Health informs execution planning — an open circuit or
/// unavailable provider is never selected.
/// </summary>
public class ProviderHealthSnapshot : ValueObject
{
    public ProviderAvailability Availability { get; private set; }
    public TimeSpan AverageLatency { get; private set; }
    public double ErrorRate { get; private set; }          // 0.0 – 1.0
    public double CapacityUtilisation { get; private set; } // 0.0 – 1.0
    public CircuitStatus Circuit { get; private set; }
    public DateTime ObservedAt { get; private set; }

    private ProviderHealthSnapshot() { } // For EF Core

    private ProviderHealthSnapshot(ProviderAvailability availability, TimeSpan latency, double errorRate, double capacity, CircuitStatus circuit, DateTime observedAt)
    {
        if (errorRate is < 0 or > 1) throw new ArgumentException("Error rate must be between 0 and 1.");
        if (capacity is < 0 or > 1) throw new ArgumentException("Capacity utilisation must be between 0 and 1.");
        Availability = availability;
        AverageLatency = latency;
        ErrorRate = errorRate;
        CapacityUtilisation = capacity;
        Circuit = circuit;
        ObservedAt = observedAt;
    }

    public static ProviderHealthSnapshot Create(
        ProviderAvailability availability,
        TimeSpan averageLatency,
        double errorRate,
        double capacityUtilisation = 0,
        CircuitStatus circuit = CircuitStatus.Closed)
        => new(availability, averageLatency, errorRate, capacityUtilisation, circuit, DateTime.UtcNow);

    public static ProviderHealthSnapshot Unknown()
        => new(ProviderAvailability.Unknown, TimeSpan.Zero, 0, 0, CircuitStatus.Closed, DateTime.UtcNow);

    /// <summary>A provider is routable when available (or degraded) and its circuit is not open.</summary>
    public bool IsRoutable =>
        Availability is ProviderAvailability.Available or ProviderAvailability.Degraded
        && Circuit != CircuitStatus.Open;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Availability;
        yield return AverageLatency;
        yield return ErrorRate;
        yield return CapacityUtilisation;
        yield return Circuit;
        yield return ObservedAt;
    }
}

/// <summary>
/// Retry policy for intelligent executions: attempts, backoff, and which
/// failure kinds are retryable.
/// </summary>
public class ExecutionRetryPolicy : ValueObject
{
    private static readonly FailureKind[] DefaultRetryable =
        { FailureKind.Transient, FailureKind.RateLimited, FailureKind.Timeout };

    private readonly List<FailureKind> _retryableFailures = new();

    public int MaxAttempts { get; private set; }
    public TimeSpan InitialBackoff { get; private set; }
    public double BackoffMultiplier { get; private set; }
    public IReadOnlyCollection<FailureKind> RetryableFailures => _retryableFailures.AsReadOnly();

    private ExecutionRetryPolicy() { } // For EF Core

    private ExecutionRetryPolicy(int maxAttempts, TimeSpan initialBackoff, double multiplier, IEnumerable<FailureKind> retryable)
    {
        if (maxAttempts < 1) throw new ArgumentException("MaxAttempts must be at least 1.");
        if (multiplier < 1) throw new ArgumentException("Backoff multiplier must be >= 1.");
        MaxAttempts = maxAttempts;
        InitialBackoff = initialBackoff;
        BackoffMultiplier = multiplier;
        _retryableFailures = retryable.Distinct().OrderBy(f => f).ToList();
    }

    public static ExecutionRetryPolicy Create(int maxAttempts, TimeSpan initialBackoff, double backoffMultiplier = 2.0, IEnumerable<FailureKind>? retryableFailures = null)
        => new(maxAttempts, initialBackoff, backoffMultiplier, retryableFailures ?? DefaultRetryable);

    public static ExecutionRetryPolicy Default() => Create(3, TimeSpan.FromSeconds(1));
    public static ExecutionRetryPolicy None() => Create(1, TimeSpan.Zero);

    public bool CanRetry(int attemptNumber, FailureKind failure)
        => attemptNumber < MaxAttempts && _retryableFailures.Contains(failure);

    public TimeSpan BackoffFor(int attemptNumber)
        => TimeSpan.FromMilliseconds(InitialBackoff.TotalMilliseconds * Math.Pow(BackoffMultiplier, Math.Max(0, attemptNumber - 1)));

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return MaxAttempts;
        yield return InitialBackoff;
        yield return BackoffMultiplier;
        foreach (var f in _retryableFailures) yield return f;
    }
}

/// <summary>
/// Fallback policy: an ordered chain of alternative models to try when the
/// primary execution fails beyond retry.
/// </summary>
public class ExecutionFallbackPolicy : ValueObject
{
    private readonly List<IntelligenceModelId> _fallbackModels = new();
    public IReadOnlyList<IntelligenceModelId> FallbackModels => _fallbackModels.AsReadOnly();
    public bool HasFallback => _fallbackModels.Count > 0;

    private ExecutionFallbackPolicy() { } // For EF Core

    private ExecutionFallbackPolicy(IEnumerable<IntelligenceModelId> models)
    {
        _fallbackModels = models.ToList();
    }

    public static ExecutionFallbackPolicy Chain(params IntelligenceModelId[] models) => new(models);
    public static ExecutionFallbackPolicy None() => new(Enumerable.Empty<IntelligenceModelId>());

    public IntelligenceModelId? NextAfter(int failedFallbacks)
        => failedFallbacks < _fallbackModels.Count ? _fallbackModels[failedFallbacks] : null;

    protected override IEnumerable<object> GetEqualityComponents()
        => _fallbackModels.Select(m => (object)m.Value);
}

/// <summary>A recorded execution failure with its classification.</summary>
public class ExecutionFailure : ValueObject
{
    public FailureKind Kind { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }

    private ExecutionFailure() { } // For EF Core

    private ExecutionFailure(FailureKind kind, string reason, DateTime occurredAt)
    {
        Kind = kind;
        Reason = reason ?? string.Empty;
        OccurredAt = occurredAt;
    }

    public static ExecutionFailure Create(FailureKind kind, string reason) => new(kind, reason, DateTime.UtcNow);

    public bool IsRetryable(ExecutionRetryPolicy policy, int attemptNumber) => policy.CanRetry(attemptNumber, Kind);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Kind;
        yield return Reason;
        yield return OccurredAt;
    }
}
