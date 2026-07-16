using ConvoLab.Domain.Prompt.Enums;
using PromptAggregate = ConvoLab.Domain.Prompt.Aggregates.Prompt;

namespace ConvoLab.Domain.Prompt.Policies;

/// <summary>
/// Encapsulates governance rules for the Prompt domain.
/// These rules enforce business invariants that span multiple entities or require
/// contextual information beyond what the aggregate itself can validate.
/// </summary>
public static class PromptGovernancePolicy
{
    /// <summary>
    /// Determines whether a prompt requires formal approval before activation.
    /// Prompts in production environments or with active policies always require approval.
    /// </summary>
    public static bool RequiresApproval(PromptAggregate prompt)
    {
        return prompt.Policies.Any(p => p.IsActive && p.PolicyType == "Governance")
               || prompt.Metadata.Environment == "Production";
    }

    /// <summary>
    /// Validates that all required variables are present in the provided variable set.
    /// Returns a list of missing variable keys.
    /// </summary>
    public static IEnumerable<string> ValidateVariables(PromptAggregate prompt, IReadOnlyDictionary<string, string> providedVariables)
    {
        var activeVersion = prompt.GetActiveVersion();
        if (activeVersion == null) return Enumerable.Empty<string>();

        return activeVersion.Variables
            .Where(v => v.IsRequired && !providedVariables.ContainsKey(v.Key))
            .Select(v => v.Key);
    }

    /// <summary>
    /// Determines whether a prompt can be rendered in its current state.
    /// </summary>
    public static bool CanRender(PromptAggregate prompt)
    {
        return prompt.Status == PromptStatus.Active && prompt.ActiveVersionId != null;
    }

    /// <summary>
    /// Validates that the total traffic weight across all variants sums to 100
    /// for a valid A/B test configuration.
    /// </summary>
    public static bool HasValidVariantWeights(PromptAggregate prompt)
    {
        if (!prompt.Variants.Any()) return true;
        return prompt.Variants.Sum(v => v.TrafficWeight) == 100;
    }
}
