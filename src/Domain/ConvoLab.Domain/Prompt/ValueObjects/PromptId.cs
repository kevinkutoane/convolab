using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Prompt.ValueObjects;

public class PromptId : ValueObject
{
    public Guid Value { get; private set; }

    private PromptId(Guid value) => Value = value;

    public static PromptId CreateUnique() => new(Guid.NewGuid());

    public static PromptId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("PromptId cannot be empty.", nameof(value));
        return new PromptId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    // For EF Core
    private PromptId() { }

    public static implicit operator Guid(PromptId id) => id.Value;
    public static implicit operator PromptId(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
