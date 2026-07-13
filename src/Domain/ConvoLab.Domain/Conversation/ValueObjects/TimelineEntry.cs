using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.ValueObjects;

namespace ConvoLab.Domain.Conversation.ValueObjects;

public class TimelineEntry : ValueObject
{
    public TimelineEntryId Id { get; private set; }
    public string EventName { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string Description { get; private set; }
    public ConversationMetadata Metadata { get; private set; }

    private TimelineEntry(TimelineEntryId id, string eventName, DateTime timestamp, string description, ConversationMetadata metadata)
    {
        Id = id;
        EventName = eventName;
        Timestamp = timestamp;
        Description = description;
        Metadata = metadata;
    }

    public static TimelineEntry Create(string eventName, string description, ConversationMetadata metadata)
    {
        return new(TimelineEntryId.CreateUnique(), eventName, DateTime.UtcNow, description, metadata);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Id;
        yield return EventName;
        yield return Timestamp;
        yield return Description;
        yield return Metadata;
    }

    private TimelineEntry() { Id = null!; EventName = null!; Description = null!; Metadata = null!; }
}
