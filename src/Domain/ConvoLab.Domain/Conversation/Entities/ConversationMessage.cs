using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.Enums;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Tracing.ValueObjects;
using ConvoLab.Domain.Evaluation.ValueObjects;

namespace ConvoLab.Domain.Conversation.Entities;

public class ConversationMessage : BaseEntity<MessageId>
{
    public MessageType Type { get; private set; }
    public MessageContent Content { get; private set; }
    public ParticipantId SenderId { get; private set; }
    public DateTime SentAt { get; private set; }
    public ConversationMetadata Metadata { get; private set; }
    public IReadOnlyList<AttachmentId> AttachmentIds { get; private set; }
    public MessageId? ParentMessageId { get; private set; }
    public bool IsStreaming { get; private set; }
    public TokenUsage? TokenUsage { get; private set; }
    public EvaluationId? EvaluationId { get; private set; }
    public TraceId? TraceId { get; private set; }

    private ConversationMessage(
        MessageId id,
        MessageType type,
        MessageContent content,
        ParticipantId senderId,
        DateTime sentAt,
        ConversationMetadata metadata,
        IEnumerable<AttachmentId> attachmentIds,
        MessageId? parentMessageId,
        bool isStreaming,
        TokenUsage? tokenUsage,
        EvaluationId? evaluationId,
        TraceId? traceId)
        : base(id)
    {
        Type = type;
        Content = content;
        SenderId = senderId;
        SentAt = sentAt;
        Metadata = metadata;
        AttachmentIds = attachmentIds.ToList().AsReadOnly();
        ParentMessageId = parentMessageId;
        IsStreaming = isStreaming;
        TokenUsage = tokenUsage;
        EvaluationId = evaluationId;
        TraceId = traceId;
    }

    public static ConversationMessage Create(
        MessageType type,
        MessageContent content,
        ParticipantId senderId,
        ConversationMetadata metadata,
        IEnumerable<AttachmentId> attachmentIds,
        MessageId? parentMessageId = null,
        bool isStreaming = false,
        TokenUsage? tokenUsage = null,
        EvaluationId? evaluationId = null,
        TraceId? traceId = null)
    {
        return new(
            MessageId.CreateUnique(),
            type,
            content,
            senderId,
            DateTime.UtcNow,
            metadata,
            attachmentIds,
            parentMessageId,
            isStreaming,
            tokenUsage,
            evaluationId,
            traceId);
    }

    private ConversationMessage() { 
        Content = null!;
        SenderId = null!;
        Metadata = null!;
        AttachmentIds = new List<AttachmentId>().AsReadOnly();
    }
}
