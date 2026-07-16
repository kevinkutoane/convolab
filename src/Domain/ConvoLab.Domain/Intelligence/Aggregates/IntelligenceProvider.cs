using ConvoLab.Domain.Common;
using ConvoLab.Domain.Intelligence.Entities;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.Events;
using ConvoLab.Domain.Intelligence.ValueObjects;

namespace ConvoLab.Domain.Intelligence.Aggregates;

/// <summary>
/// A provider of intelligent capabilities, modelled as a business concept —
/// OpenAI, Azure OpenAI, Gemini, Anthropic, Mistral, Ollama, an internal model
/// farm, or any future provider. Owns its model catalogue, health, rate
/// limits, and circuit breaker. Never references an SDK.
/// </summary>
public class IntelligenceProvider : BaseAggregateRoot<IntelligenceProviderId>
{
    private readonly List<IntelligenceModel> _models = new();

    public string Name { get; private set; }
    public ProviderKind Kind { get; private set; }
    public RateLimitWindow RateLimits { get; private set; }
    public ProviderHealthSnapshot Health { get; private set; }
    public bool IsEnabled { get; private set; }

    /// <summary>Consecutive failures observed; drives the circuit breaker.</summary>
    public int ConsecutiveFailures { get; private set; }

    /// <summary>Failures tolerated before the circuit opens.</summary>
    public int CircuitBreakThreshold { get; private set; }

    public IReadOnlyList<IntelligenceModel> Models => _models.AsReadOnly();

    private IntelligenceProvider() : base()
    {
        Name = null!;
        RateLimits = null!;
        Health = null!;
    } // For EF Core

    private IntelligenceProvider(IntelligenceProviderId id, string name, ProviderKind kind, RateLimitWindow rateLimits, int circuitBreakThreshold) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Provider name is required.");
        if (circuitBreakThreshold < 1) throw new ArgumentException("Circuit break threshold must be at least 1.");

        Name = name;
        Kind = kind;
        RateLimits = rateLimits;
        Health = ProviderHealthSnapshot.Unknown();
        IsEnabled = true;
        CircuitBreakThreshold = circuitBreakThreshold;
    }

    public static IntelligenceProvider Register(string name, ProviderKind kind, RateLimitWindow? rateLimits = null, int circuitBreakThreshold = 5)
    {
        var provider = new IntelligenceProvider(
            IntelligenceProviderId.CreateUnique(), name, kind,
            rateLimits ?? RateLimitWindow.Unlimited(), circuitBreakThreshold);

        provider.AddDomainEvent(new ProviderRegisteredEvent(provider.Id, name, kind));
        return provider;
    }

    // ── Model catalogue ─────────────────────────────────────────────────

    public IntelligenceModel AddModel(
        string name,
        CapabilitySet capabilities,
        ModelPricing pricing,
        int maxContextTokens,
        int maxOutputTokens,
        TimeSpan? typicalLatency = null)
    {
        if (_models.Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Model '{name}' is already registered for provider '{Name}'.");

        var model = new IntelligenceModel(
            IntelligenceModelId.CreateUnique(), name, Id, capabilities, pricing,
            maxContextTokens, maxOutputTokens, typicalLatency ?? TimeSpan.FromSeconds(5));

        model.Activate();
        _models.Add(model);
        return model;
    }

    public IntelligenceModel? FindModel(IntelligenceModelId modelId)
        => _models.FirstOrDefault(m => m.Id == modelId);

    /// <summary>All routable models on this provider that can serve the requirement.</summary>
    public IEnumerable<IntelligenceModel> ModelsServing(ExecutionRequirement requirement, int estimatedPromptTokens)
        => IsRoutable ? _models.Where(m => m.CanServe(requirement, estimatedPromptTokens)) : Enumerable.Empty<IntelligenceModel>();

    // ── Health & circuit breaking ───────────────────────────────────────

    /// <summary>A provider is routable when enabled, healthy enough, and circuit not open.</summary>
    public bool IsRoutable => IsEnabled && Health.IsRoutable;

    public void ReportHealth(ProviderHealthSnapshot snapshot)
    {
        var circuitChanged = Health.Circuit != snapshot.Circuit;
        var availabilityChanged = Health.Availability != snapshot.Availability;
        Health = snapshot;

        if (circuitChanged || availabilityChanged)
            AddDomainEvent(new ProviderHealthChangedEvent(Id, snapshot.Availability, snapshot.Circuit));
    }

    /// <summary>Records a successful call: closes the circuit and clears the failure streak.</summary>
    public void RecordSuccess()
    {
        ConsecutiveFailures = 0;
        if (Health.Circuit != CircuitStatus.Closed)
        {
            Health = ProviderHealthSnapshot.Create(
                ProviderAvailability.Available, Health.AverageLatency, Health.ErrorRate,
                Health.CapacityUtilisation, CircuitStatus.Closed);
            AddDomainEvent(new ProviderHealthChangedEvent(Id, Health.Availability, Health.Circuit));
        }
    }

    /// <summary>
    /// Records a failed call. When consecutive failures reach the threshold,
    /// the circuit opens and the provider is excluded from planning.
    /// </summary>
    public void RecordFailure()
    {
        ConsecutiveFailures++;
        if (ConsecutiveFailures >= CircuitBreakThreshold && Health.Circuit != CircuitStatus.Open)
        {
            Health = ProviderHealthSnapshot.Create(
                ProviderAvailability.Degraded, Health.AverageLatency, Health.ErrorRate,
                Health.CapacityUtilisation, CircuitStatus.Open);
            AddDomainEvent(new ProviderHealthChangedEvent(Id, Health.Availability, Health.Circuit));
        }
    }

    /// <summary>Moves an open circuit to half-open, allowing a probe execution.</summary>
    public void AllowProbe()
    {
        if (Health.Circuit != CircuitStatus.Open)
            throw new InvalidOperationException("Only an open circuit can move to half-open.");

        Health = ProviderHealthSnapshot.Create(
            Health.Availability, Health.AverageLatency, Health.ErrorRate,
            Health.CapacityUtilisation, CircuitStatus.HalfOpen);
        AddDomainEvent(new ProviderHealthChangedEvent(Id, Health.Availability, Health.Circuit));
    }

    public void Disable() => IsEnabled = false;
    public void Enable() => IsEnabled = true;
}
