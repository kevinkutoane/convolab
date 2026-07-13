using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Conversation.ValueObjects;

public class SessionId : ValueObject
{
    public Guid Value { get; private set; }

    private SessionId(Guid value)
    {
        Value = value;
    }

    public static SessionId CreateUnique() => new(Guid.NewGuid());

    public static SessionId Create(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    private SessionId() { }
}
