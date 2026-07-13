using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Conversation.ValueObjects;

public class ParticipantId : ValueObject
{
    public Guid Value { get; private set; }

    private ParticipantId(Guid value)
    {
        Value = value;
    }

    public static ParticipantId CreateUnique() => new(Guid.NewGuid());

    public static ParticipantId Create(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    private ParticipantId() { }
}
