using ConvoLab.Domain.Intelligence.Aggregates;
using ConvoLab.Domain.Intelligence.Entities;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.ValueObjects;

namespace ConvoLab.Domain.Intelligence.Services;

/// <summary>
/// The Execution Planner — the routing brain of the Intelligence Engine.
/// Given a requirement, a context, a policy, and the current provider
/// catalogue with live health, it selects a provider and model, determines
/// retry/fallback/streaming/tool strategy, estimates tokens/cost/latency,
/// validates policy, and produces an immutable ExecutionPlan.
///
/// Pure domain logic: no I/O, deterministic given its inputs, fully testable.
/// </summary>
public class ExecutionPlanner
{
    /// <summary>
    /// Produces an immutable ExecutionPlan for the requirement, or throws
    /// when no routable provider/model combination can serve it.
    /// </summary>
    public ExecutionPlan CreatePlan(
        ValueObjects.ExecutionContext context,
        ExecutionRequirement requirement,
        ExecutionPolicy policy,
        IReadOnlyList<IntelligenceProvider> providers,
        ExecutionRetryPolicy? retryPolicy = null)
    {
        var candidates = FindCandidates(context, requirement, providers).ToList();

        if (candidates.Count == 0)
            throw new InvalidOperationException(
                "No routable provider/model combination satisfies the required capabilities, " +
                "context window, health, and latency constraints.");

        // Rank: prefer healthy providers, then models meeting the latency
        // target, then lowest estimated cost. Deterministic and explainable.
        var expectedUsage = EstimateUsage(context, requirement);
        var ranked = candidates
            .OrderByDescending(c => c.Provider.Health.Availability == ProviderAvailability.Available)
            .ThenByDescending(c => c.Model.TypicalLatency <= requirement.Latency.Target)
            .ThenBy(c => c.Model.EstimateCost(expectedUsage).Amount)
            .ThenBy(c => c.Model.TypicalLatency)
            .ToList();

        var best = ranked.First();
        if (!string.IsNullOrWhiteSpace(requirement.PreferredModelName))
        {
            var preferred = ranked.FirstOrDefault(candidate =>
                ModelNamesMatch(candidate.Model.Name, requirement.PreferredModelName));
            if (preferred.Provider is null || preferred.Model is null)
                throw new InvalidOperationException(
                    $"Requested model '{requirement.PreferredModelName}' is not routable for the selected provider and execution requirement.");
            best = preferred;
        }

        var estimatedCost = best.Model.EstimateCost(expectedUsage);

        // Fallback chain: remaining candidates in rank order, capped at 2.
        var fallbackChain = policy.AllowFallback
            ? ExecutionFallbackPolicy.Chain(candidates
                .Where(c => c.Model.Id != best.Model.Id)
                .OrderBy(c => c.Model.EstimateCost(expectedUsage).Amount)
                .Take(2)
                .Select(c => c.Model.Id)
                .ToArray())
            : ExecutionFallbackPolicy.None();

        var useStreaming = requirement.RequiresStreaming && policy.AllowStreaming
                           && best.Model.Capabilities.Supports(IntelligenceCapability.Streaming);

        var allowTools = requirement.RequiresTools && policy.AllowTools
                         && best.Model.Capabilities.Supports(IntelligenceCapability.ToolCalling);

        return ExecutionPlan.Create(
            best.Provider.Id,
            best.Model.Id,
            best.Provider.Name,
            best.Model.Name,
            retryPolicy ?? ExecutionRetryPolicy.Default(),
            fallbackChain,
            useStreaming,
            allowTools,
            expectedUsage,
            estimatedCost,
            best.Model.TypicalLatency,
            policy);
    }

    /// <summary>
    /// Produces a plan for a specific fallback model after primary failure,
    /// preserving the original policy but with no further fallback.
    /// </summary>
    public ExecutionPlan CreateFallbackPlan(
        ValueObjects.ExecutionContext context,
        ExecutionRequirement requirement,
        ExecutionPolicy policy,
        IReadOnlyList<IntelligenceProvider> providers,
        IntelligenceModelId fallbackModelId)
    {
        var matches = FindCandidates(context, requirement, providers)
            .Where(c => c.Model.Id == fallbackModelId)
            .ToList();

        if (matches.Count == 0)
            throw new InvalidOperationException($"Fallback model '{fallbackModelId}' cannot serve the requirement or is not routable.");

        var candidate = matches[0];

        var expectedUsage = EstimateUsage(context, requirement);

        return ExecutionPlan.Create(
            candidate.Provider.Id,
            candidate.Model.Id,
            candidate.Provider.Name,
            candidate.Model.Name,
            ExecutionRetryPolicy.None(),
            ExecutionFallbackPolicy.None(),
            requirement.RequiresStreaming && policy.AllowStreaming && candidate.Model.Capabilities.Supports(IntelligenceCapability.Streaming),
            requirement.RequiresTools && policy.AllowTools && candidate.Model.Capabilities.Supports(IntelligenceCapability.ToolCalling),
            expectedUsage,
            candidate.Model.EstimateCost(expectedUsage),
            candidate.Model.TypicalLatency,
            policy);
    }

    /// <summary>Estimates token usage from the context and requirement.</summary>
    public ExecutionUsage EstimateUsage(ValueObjects.ExecutionContext context, ExecutionRequirement requirement)
        => ExecutionUsage.Create(context.EstimatedPromptTokens, requirement.MaxOutputTokens);

    private static IEnumerable<(IntelligenceProvider Provider, IntelligenceModel Model)> FindCandidates(
        ValueObjects.ExecutionContext context,
        ExecutionRequirement requirement,
        IReadOnlyList<IntelligenceProvider> providers)
        => providers
            .Where(provider => provider.IsRoutable)
            .Where(provider => !requirement.RequiredProviderKind.HasValue || provider.Kind == requirement.RequiredProviderKind.Value)
            .SelectMany(provider => provider.ModelsServing(requirement, context.EstimatedPromptTokens).Select(model => (provider, model)));

    private static bool ModelNamesMatch(string registeredName, string requestedName)
        => NormalizeModelName(registeredName).Equals(NormalizeModelName(requestedName), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeModelName(string value)
    {
        var normalized = new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        return normalized.StartsWith("convolab", StringComparison.Ordinal)
            ? normalized["convolab".Length..]
            : normalized;
    }
}
