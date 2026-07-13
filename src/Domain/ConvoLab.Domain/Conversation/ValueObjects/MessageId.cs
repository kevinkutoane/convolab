using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Conversation.ValueObjects;

public class MessageId : ValueObject
{
    public Guid Value { get; private set; }

    private MessageId(Guid value)
    {
        Value = value;
    }

    public static MessageId CreateUnique() => new(Guid.NewGuid());

    public static MessageId Create(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    private MessageId() { }
}
