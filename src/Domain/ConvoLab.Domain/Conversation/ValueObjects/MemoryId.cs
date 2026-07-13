using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Conversation.ValueObjects;

public class MemoryId : ValueObject
{
    public Guid Value { get; private set; }

    private MemoryId(Guid value)
    {
        Value = value;
    }

    public static MemoryId CreateUnique() => new(Guid.NewGuid());

    public static MemoryId Create(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    private MemoryId() { }
}
