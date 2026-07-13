using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Events;

namespace ConvoLab.Domain.Conversation.Events;

public record AttachmentAddedEvent(ConversationId ConversationId, AttachmentId AttachmentId, MessageId MessageId, DateTime AttachedAt) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
