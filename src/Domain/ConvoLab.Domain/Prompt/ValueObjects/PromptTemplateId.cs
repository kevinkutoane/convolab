using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Prompt.ValueObjects;

public class PromptTemplateId : ValueObject
{
    public Guid Value { get; private set; }

    private PromptTemplateId(Guid value)
    {
        Value = value;
    }

    public static PromptTemplateId CreateUnique()
    {
        return new PromptTemplateId(Guid.NewGuid());
    }

    public static PromptTemplateId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("PromptTemplateId cannot be empty.", nameof(value));
        }
        return new PromptTemplateId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    // For EF Core
    private PromptTemplateId() { }

    public static implicit operator Guid(PromptTemplateId id) => id.Value;
    public static implicit operator PromptTemplateId(Guid value) => new(value);
}
