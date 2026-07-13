using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.ValueObjects;

namespace ConvoLab.Domain.Conversation.ValueObjects;

public class ConversationTimeline : ValueObject
{
    public IReadOnlyList<TimelineEntry> Entries { get; private set; }

    private ConversationTimeline(IEnumerable<TimelineEntry> entries)
    {
        Entries = entries.ToList().AsReadOnly();
    }

    public static ConversationTimeline Create(IEnumerable<TimelineEntry> entries) => new(entries);

    public ConversationTimeline AddEntry(TimelineEntry entry)
    {
        var newEntries = Entries.ToList();
        newEntries.Add(entry);
        return new ConversationTimeline(newEntries);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        foreach (var entry in Entries)
        {
            yield return entry;
        }
    }

    private ConversationTimeline() { Entries = new List<TimelineEntry>().AsReadOnly(); }
}
