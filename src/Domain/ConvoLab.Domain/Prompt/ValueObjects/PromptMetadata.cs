using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Prompt.ValueObjects;

/// <summary>
/// Carries descriptive and organizational metadata for a prompt.
/// </summary>
public class PromptMetadata : ValueObject
{
    public string Description { get; private set; }
    public string Category { get; private set; }
    public IReadOnlyList<string> Tags { get; private set; }
    public string? Environment { get; private set; }

    private PromptMetadata(string description, string category, IEnumerable<string> tags, string? environment)
    {
        Description = description ?? string.Empty;
        Category = category ?? string.Empty;
        Tags = tags?.ToList().AsReadOnly() ?? new List<string>().AsReadOnly();
        Environment = environment;
    }

    public static PromptMetadata Create(string description, string category, IEnumerable<string>? tags = null, string? environment = null)
        => new(description, category, tags ?? Enumerable.Empty<string>(), environment);

    public static PromptMetadata Empty() => new(string.Empty, string.Empty, Enumerable.Empty<string>(), null);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Description;
        yield return Category;
        foreach (var tag in Tags) yield return tag;
    }

    private PromptMetadata() { Description = null!; Category = null!; Tags = null!; }
}
