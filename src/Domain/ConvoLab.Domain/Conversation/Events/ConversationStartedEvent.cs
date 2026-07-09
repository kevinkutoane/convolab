using ConvoLab.Domain.Events;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;
namespace ConvoLab.Domain.Conversation.Events;
public record ConversationStartedEvent(ConversationId ConversationId, string Title, UserId InitiatorId) : IDomainEvent {
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
