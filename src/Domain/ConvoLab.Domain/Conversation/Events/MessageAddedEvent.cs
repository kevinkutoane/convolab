using ConvoLab.Domain.Events;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;
namespace ConvoLab.Domain.Conversation.Events;
public record MessageAddedEvent(ConversationId ConversationId, Guid MessageId, UserId SenderId, string Content) : IDomainEvent {
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
