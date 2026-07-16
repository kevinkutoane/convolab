using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Prompt.ValueObjects;

/// <summary>
/// Represents a variable within a prompt template, e.g., {{CustomerName}}.
/// Variables make prompts dynamic and reusable across different execution contexts.
/// </summary>
public class PromptVariable : ValueObject
{
    public string Key { get; private set; }
    public string? DefaultValue { get; private set; }
    public bool IsRequired { get; private set; }
    public string Description { get; private set; }

    private PromptVariable(string key, bool isRequired, string description, string? defaultValue)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Variable key cannot be empty.", nameof(key));

        Key = key;
        IsRequired = isRequired;
        Description = description;
        DefaultValue = defaultValue;
    }

    public static PromptVariable Create(string key, bool isRequired, string description, string? defaultValue = null)
        => new(key, isRequired, description, defaultValue);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Key;
    }

    private PromptVariable() { Key = null!; Description = null!; }
}
