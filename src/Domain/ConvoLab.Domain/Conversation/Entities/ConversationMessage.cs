using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.Enums;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Evaluation.ValueObjects;
using ConvoLab.Domain.Tracing.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;

namespace ConvoLab.Domain.Conversation.Entities;

public class ConversationMessage : BaseEntity<MessageId>
{
    public ParticipantRole Role { get; private set; }
    public MessageContent Content { get; private set; }
    public UserId SenderId { get; private set; }
    public DateTime SentAt { get; private set; }
    public ConversationMetadata Metadata { get; private set; }
    public IReadOnlyList<AttachmentId> AttachmentIds { get; private set; }
    public MessageId? ParentMessageId { get; private set; }
    public bool IsStreaming { get; private set; }
    public MessageType Type { get; private set; }

    // External References
    public EvaluationId? EvaluationId { get; private set; }
    public TraceId? TraceId { get; private set; }

    private ConversationMessage(
        MessageId id,
        ParticipantRole role,
        MessageContent content,
        UserId senderId,
        DateTime sentAt,
        ConversationMetadata metadata,
        IEnumerable<AttachmentId> attachmentIds,
        MessageType type,
        MessageId? parentMessageId = null,
        bool isStreaming = false)
        : base(id)
    {
        Role = role;
        Content = content;
        SenderId = senderId;
        SentAt = sentAt;
        Metadata = metadata;
        AttachmentIds = attachmentIds.ToList().AsReadOnly();
        Type = type;
        ParentMessageId = parentMessageId;
        IsStreaming = isStreaming;
    }

    public static ConversationMessage Create(
        ParticipantRole role,
        MessageContent content,
        UserId senderId,
        ConversationMetadata metadata,
        MessageType type = MessageType.Text,
        IEnumerable<AttachmentId>? attachmentIds = null,
        MessageId? parentMessageId = null,
        bool isStreaming = false)
    {
        return new(
            MessageId.CreateUnique(),
            role,
            content,
            senderId,
            DateTime.UtcNow,
            metadata,
            attachmentIds ?? new List<AttachmentId>(),
            type,
            parentMessageId,
            isStreaming);
    }

    // Messages are immutable - no setters or modification methods

    private ConversationMessage() { 
        Content = null!;
        SenderId = null!;
        Metadata = null!;
        AttachmentIds = new List<AttachmentId>().AsReadOnly();
    }
}
