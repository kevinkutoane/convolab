using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Conversation.ValueObjects;

public class ConversationWindow : ValueObject
{
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }

    private ConversationWindow(DateTime startTime, DateTime? endTime)
    {
        StartTime = startTime;
        EndTime = endTime;
    }

    public static ConversationWindow Create(DateTime startTime, DateTime? endTime = null) => new(startTime, endTime);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return StartTime;
        yield return EndTime;
    }

    private ConversationWindow() { }
}
