using ConvoLab.Domain.Common;
using ConvoLab.Domain.Events;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;

namespace ConvoLab.Domain.Conversation.Events;

public record MessageAddedEvent(ConversationId ConversationId, MessageId MessageId, UserId SenderId, DateTime SentAt) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
