using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Prompt.ValueObjects;

public class PromptExperimentId : ValueObject
{
    public Guid Value { get; private set; }

    private PromptExperimentId(Guid value) => Value = value;

    public static PromptExperimentId CreateUnique() => new(Guid.NewGuid());

    public static PromptExperimentId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("PromptExperimentId cannot be empty.", nameof(value));
        return new PromptExperimentId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    private PromptExperimentId() { }

    public static implicit operator Guid(PromptExperimentId id) => id.Value;
    public static implicit operator PromptExperimentId(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
