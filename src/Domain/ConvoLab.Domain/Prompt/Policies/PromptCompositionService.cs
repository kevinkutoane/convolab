using PromptAggregate = ConvoLab.Domain.Prompt.Aggregates.Prompt;

namespace ConvoLab.Domain.Prompt.Policies;

/// <summary>
/// Domain service responsible for composing a final prompt from multiple
/// ordered sections. This supports the enterprise composition pattern:
/// System + Role + Knowledge + Safety + ConversationMemory + UserMessage.
/// 
/// Composition is provider-agnostic; the output is always a plain string.
/// </summary>
public static class PromptCompositionService
{
    /// <summary>
    /// Composes a prompt from its sections in the canonical order.
    /// Sections are assembled in ascending Order, with disabled sections skipped.
    /// </summary>
    public static string Compose(PromptAggregate prompt, IReadOnlyDictionary<string, string> variables)
    {
        var enabledSections = prompt.Sections
            .Where(s => s.IsEnabled)
            .OrderBy(s => s.Order)
            .ToList();

        if (!enabledSections.Any())
            throw new InvalidOperationException($"Prompt '{prompt.Name}' has no enabled sections for composition.");

        var parts = enabledSections.Select(s => InjectVariables(s.Content, variables));
        return string.Join("\n\n", parts);
    }

    /// <summary>
    /// Validates that the composition order is consistent (no duplicate order values).
    /// </summary>
    public static bool HasConsistentSectionOrder(PromptAggregate prompt)
    {
        var orders = prompt.Sections.Select(s => s.Order).ToList();
        return orders.Count == orders.Distinct().Count();
    }

    private static string InjectVariables(string content, IReadOnlyDictionary<string, string> variables)
    {
        foreach (var variable in variables)
        {
            content = content.Replace($"{{{{{variable.Key}}}}}", variable.Value);
        }
        return content;
    }
}
