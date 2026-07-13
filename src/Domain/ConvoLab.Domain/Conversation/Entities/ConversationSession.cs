using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.Enums;
using ConvoLab.Domain.Conversation.ValueObjects;

namespace ConvoLab.Domain.Conversation.Entities;

public class ConversationSession : BaseEntity<SessionId>
{
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public SessionStatus Status { get; private set; }
    public string? ReasonClosed { get; private set; }
    public TimeSpan? Duration => EndTime - StartTime;
    public IReadOnlyList<MessageId> MessageIds { get; private set; }

    private ConversationSession(SessionId id, DateTime startTime, SessionStatus status, IEnumerable<MessageId> messageIds)
        : base(id)
    {
        StartTime = startTime;
        Status = status;
        MessageIds = messageIds.ToList().AsReadOnly();
    }

    public static ConversationSession Create(IEnumerable<MessageId> messageIds)
    {
        return new(SessionId.CreateUnique(), DateTime.UtcNow, SessionStatus.Active, messageIds);
    }

    public void EndSession(SessionStatus status, string? reasonClosed = null)
    {
        EndTime = DateTime.UtcNow;
        Status = status;
        ReasonClosed = reasonClosed;
    }

    public void AddMessage(MessageId messageId)
    {
        var messageList = MessageIds.ToList();
        messageList.Add(messageId);
        MessageIds = messageList.AsReadOnly();
    }

    private ConversationSession() { MessageIds = new List<MessageId>().AsReadOnly(); }
}
