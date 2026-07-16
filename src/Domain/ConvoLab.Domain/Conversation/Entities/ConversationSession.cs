using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.Enums;
using ConvoLab.Domain.Conversation.ValueObjects;

namespace ConvoLab.Domain.Conversation.Entities;

public class ConversationSession : BaseEntity<SessionId>
{
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public SessionStatus Status { get; private set; }
    public string? CloseReason { get; private set; }
    public TimeSpan? Duration => EndTime - StartTime;
    
    private readonly List<MessageId> _messageIds = new();
    public IReadOnlyList<MessageId> MessageIds => _messageIds.AsReadOnly();

    private readonly List<ParticipantId> _participantIds = new();
    public IReadOnlyList<ParticipantId> ParticipantIds => _participantIds.AsReadOnly();

    public ConversationMetadata Metadata { get; private set; }

    private ConversationSession(
        SessionId id, 
        DateTime startTime, 
        SessionStatus status, 
        IEnumerable<MessageId> messageIds,
        IEnumerable<ParticipantId> participantIds,
        ConversationMetadata metadata)
        : base(id)
    {
        StartTime = startTime;
        Status = status;
        _messageIds = messageIds.ToList();
        _participantIds = participantIds.ToList();
        Metadata = metadata;
    }

    public static ConversationSession Create(
        IEnumerable<ParticipantId> participantIds,
        ConversationMetadata? metadata = null)
    {
        return new(
            SessionId.CreateUnique(), 
            DateTime.UtcNow, 
            SessionStatus.Active, 
            new List<MessageId>(),
            participantIds,
            metadata ?? ConversationMetadata.Create(new Dictionary<string, string>()));
    }

    public void EndSession(SessionStatus status, string? reason = null)
    {
        EndTime = DateTime.UtcNow;
        Status = status;
        CloseReason = reason;
    }

    public void AddMessage(MessageId messageId)
    {
        if (!_messageIds.Contains(messageId))
        {
            _messageIds.Add(messageId);
        }
    }

    private ConversationSession() { 
        Metadata = null!;
    }
}
