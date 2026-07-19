using System.Text.RegularExpressions;
using ConvoLab.Domain.Prompt.ValueObjects;

namespace ConvoLab.Domain.Prompt.Services;

/// <summary>Provider-independent deterministic prompt composition and validation.</summary>
public static partial class PromptTemplateEngine
{
    [GeneratedRegex(@"\{\{\s*([a-zA-Z0-9_.-]+)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex VariablePattern();

    public static IReadOnlyList<string> DiscoverVariables(IEnumerable<PromptTemplateSection> sections)
        => sections
            .SelectMany(section => VariablePattern().Matches(section.Content).Select(match => match.Groups[1].Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static IReadOnlyList<string> FindMissingRequiredVariables(
        IEnumerable<PromptTemplateSection> sections,
        IReadOnlyDictionary<string, string> variables)
        => sections
            .Where(section => section.Required)
            .SelectMany(section => VariablePattern().Matches(section.Content).Select(match => match.Groups[1].Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(variable => !variables.ContainsKey(variable))
            .OrderBy(variable => variable, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string Render(
        IEnumerable<PromptTemplateSection> sections,
        IReadOnlyDictionary<string, string> variables,
        bool requireAllVariables)
    {
        var ordered = sections.OrderBy(section => section.Sequence).ToList();
        if (ordered.Count == 0) throw new InvalidOperationException("At least one prompt section is required.");
        if (ordered.Select(section => section.Sequence).Distinct().Count() != ordered.Count)
            throw new InvalidOperationException("Prompt section sequence values must be unique.");

        var missing = FindMissingRequiredVariables(ordered, variables);
        if (requireAllVariables && missing.Count > 0)
            throw new InvalidOperationException($"Missing required prompt variables: {string.Join(", ", missing)}.");

        var blocks = ordered.Select(section =>
        {
            var rendered = VariablePattern().Replace(
                section.Content,
                match => variables.TryGetValue(match.Groups[1].Value, out var value)
                    ? value
                    : match.Value);
            return $"{section.Type.ToString().ToUpperInvariant()}:\n{rendered}";
        });

        return string.Join("\n\n", blocks);
    }

    public static int EstimateTokens(string text)
        => Math.Max(1, (int)Math.Ceiling((text?.Length ?? 0) / 4d));
}
