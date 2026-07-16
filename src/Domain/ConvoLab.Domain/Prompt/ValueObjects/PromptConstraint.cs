using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Prompt.ValueObjects;

/// <summary>
/// Defines a constraint on a prompt, such as maximum token length or required safety filters.
/// Constraints are enforced during rendering and validation.
/// </summary>
public class PromptConstraint : ValueObject
{
    public string Name { get; private set; }
    public string ConstraintType { get; private set; }
    public string Value { get; private set; }

    private PromptConstraint(string name, string constraintType, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Constraint name cannot be empty.", nameof(name));

        Name = name;
        ConstraintType = constraintType;
        Value = value;
    }

    public static PromptConstraint MaxTokens(int maxTokens)
        => new("MaxTokens", "TokenLimit", maxTokens.ToString());

    public static PromptConstraint RequiredVariable(string variableName)
        => new($"Required:{variableName}", "RequiredVariable", variableName);

    public static PromptConstraint Create(string name, string constraintType, string value)
        => new(name, constraintType, value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Name;
        yield return ConstraintType;
    }

    private PromptConstraint() { Name = null!; ConstraintType = null!; Value = null!; }
}
