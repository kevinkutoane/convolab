using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.Enums;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Evaluation.ValueObjects;
using ConvoLab.Domain.Tracing.ValueObjects;

namespace ConvoLab.Domain.Conversation.Entities;

public class ConversationMessage : BaseEntity<MessageId>
{
    public ParticipantRole Role { get; private set; }
    public string Content { get; private set; }
    public ParticipantId SenderId { get; private set; }
    public DateTime Timestamp { get; private set; }
    public ConversationMetadata Metadata { get; private set; }
    
    private readonly List<AttachmentId> _attachmentIds = new();
    public IReadOnlyList<AttachmentId> AttachmentIds => _attachmentIds.AsReadOnly();

    public MessageId? ParentMessageId { get; private set; }
    public bool IsStreaming { get; private set; }
    public TokenUsage? TokenUsage { get; private set; }
    public EvaluationId? EvaluationId { get; private set; }
    public TraceId? TraceId { get; private set; }

    private ConversationMessage(
        MessageId id,
        ParticipantRole role,
        string content,
        ParticipantId senderId,
        DateTime timestamp,
        ConversationMetadata metadata,
        IEnumerable<AttachmentId> attachmentIds,
        MessageId? parentMessageId,
        bool isStreaming,
        TokenUsage? tokenUsage,
        EvaluationId? evaluationId,
        TraceId? traceId)
        : base(id)
    {
        Role = role;
        Content = content;
        SenderId = senderId;
        Timestamp = timestamp;
        Metadata = metadata;
        _attachmentIds = attachmentIds.ToList();
        ParentMessageId = parentMessageId;
        IsStreaming = isStreaming;
        TokenUsage = tokenUsage;
        EvaluationId = evaluationId;
        TraceId = traceId;
    }

    public static ConversationMessage Create(
        ParticipantRole role,
        string content,
        ParticipantId senderId,
        ConversationMetadata? metadata = null,
        IEnumerable<AttachmentId>? attachmentIds = null,
        MessageId? parentMessageId = null,
        bool isStreaming = false,
        TokenUsage? tokenUsage = null,
        EvaluationId? evaluationId = null,
        TraceId? traceId = null)
    {
        return new(
            MessageId.CreateUnique(),
            role,
            content,
            senderId,
            DateTime.UtcNow,
            metadata ?? ConversationMetadata.Create(new Dictionary<string, string>()),
            attachmentIds ?? new List<AttachmentId>(),
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
    }
}
