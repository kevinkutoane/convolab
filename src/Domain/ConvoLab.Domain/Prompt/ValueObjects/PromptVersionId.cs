using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Prompt.ValueObjects;

public class PromptVersionId : ValueObject
{
    public Guid Value { get; private set; }

    private PromptVersionId(Guid value) => Value = value;

    public static PromptVersionId CreateUnique() => new(Guid.NewGuid());

    public static PromptVersionId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("PromptVersionId cannot be empty.", nameof(value));
        return new PromptVersionId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    private PromptVersionId() { }

    public static implicit operator Guid(PromptVersionId id) => id.Value;
    public static implicit operator PromptVersionId(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
