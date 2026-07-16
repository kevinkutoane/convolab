using ConvoLab.Domain.Common;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.ValueObjects;

namespace ConvoLab.Domain.Intelligence.Entities;

/// <summary>
/// A model in the platform catalogue, owned by an IntelligenceProvider.
/// Models declare capabilities, context limits, pricing, and expected latency —
/// everything the Execution Planner needs to route intelligently. The domain
/// never references provider SDKs; a model is a business concept.
/// </summary>
public class IntelligenceModel : BaseEntity<IntelligenceModelId>
{
    public string Name { get; private set; }
    public IntelligenceProviderId ProviderId { get; private set; }
    public CapabilitySet Capabilities { get; private set; }
    public ModelPricing Pricing { get; private set; }
    public int MaxContextTokens { get; private set; }
    public int MaxOutputTokens { get; private set; }
    public TimeSpan TypicalLatency { get; private set; }
    public ModelLifecycleStatus Status { get; private set; }

    private IntelligenceModel() : base()
    {
        Name = null!;
        ProviderId = null!;
        Capabilities = null!;
        Pricing = null!;
    } // For EF Core

    internal IntelligenceModel(
        IntelligenceModelId id,
        string name,
        IntelligenceProviderId providerId,
        CapabilitySet capabilities,
        ModelPricing pricing,
        int maxContextTokens,
        int maxOutputTokens,
        TimeSpan typicalLatency) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Model name is required.");
        if (maxContextTokens <= 0) throw new ArgumentException("MaxContextTokens must be positive.");
        if (maxOutputTokens <= 0) throw new ArgumentException("MaxOutputTokens must be positive.");

        Name = name;
        ProviderId = providerId;
        Capabilities = capabilities;
        Pricing = pricing;
        MaxContextTokens = maxContextTokens;
        MaxOutputTokens = maxOutputTokens;
        TypicalLatency = typicalLatency;
        Status = ModelLifecycleStatus.Registered;
    }

    /// <summary>Model becomes routable by the planner.</summary>
    public void Activate()
    {
        if (Status == ModelLifecycleStatus.Retired)
            throw new InvalidOperationException("A retired model cannot be reactivated.");
        Status = ModelLifecycleStatus.Active;
    }

    /// <summary>Model remains executable but planners should prefer alternatives.</summary>
    public void Deprecate()
    {
        if (Status == ModelLifecycleStatus.Retired)
            throw new InvalidOperationException("A retired model cannot be deprecated.");
        Status = ModelLifecycleStatus.Deprecated;
    }

    /// <summary>Model is removed from routing permanently.</summary>
    public void Retire() => Status = ModelLifecycleStatus.Retired;

    public bool IsRoutable => Status is ModelLifecycleStatus.Active or ModelLifecycleStatus.Deprecated;

    /// <summary>
    /// True when this model can serve the requirement: capabilities satisfied,
    /// context window large enough, and output limit sufficient.
    /// </summary>
    public bool CanServe(ExecutionRequirement requirement, int estimatedPromptTokens)
        => IsRoutable
           && Capabilities.Satisfies(requirement.RequiredCapabilities)
           && estimatedPromptTokens + requirement.MaxOutputTokens <= MaxContextTokens
           && requirement.MaxOutputTokens <= MaxOutputTokens;

    /// <summary>Estimates cost for the expected usage against this model's pricing.</summary>
    public ExecutionCost EstimateCost(ExecutionUsage expectedUsage) => Pricing.EstimateCost(expectedUsage);

    public void UpdatePricing(ModelPricing pricing) => Pricing = pricing;

    public void UpdateCapabilities(CapabilitySet capabilities) => Capabilities = capabilities;
}
