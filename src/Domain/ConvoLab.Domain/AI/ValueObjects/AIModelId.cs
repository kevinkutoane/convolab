using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.AI.ValueObjects;

public class AIModelId : ValueObject
{
    public Guid Value { get; private set; }

    private AIModelId(Guid value)
    {
        Value = value;
    }

    public static AIModelId CreateUnique()
    {
        return new AIModelId(Guid.NewGuid());
    }

    public static AIModelId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("AIModelId cannot be empty.", nameof(value));
        }
        return new AIModelId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    // For EF Core
    private AIModelId() { }

    public static implicit operator Guid(AIModelId id) => id.Value;
    public static implicit operator AIModelId(Guid value) => new(value);
}
