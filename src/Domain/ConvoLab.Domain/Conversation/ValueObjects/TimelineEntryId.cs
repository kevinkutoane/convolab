using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Conversation.ValueObjects;

public class TimelineEntryId : ValueObject
{
    public Guid Value { get; private set; }

    private TimelineEntryId(Guid value)
    {
        Value = value;
    }

    public static TimelineEntryId CreateUnique() => new(Guid.NewGuid());

    public static TimelineEntryId Create(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    private TimelineEntryId() { }
}
