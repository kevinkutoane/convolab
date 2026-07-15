using ConvoLab.Domain.Events;
using ConvoLab.Domain.Conversation.ValueObjects;

namespace ConvoLab.Domain.Conversation.Events;

public record ConversationCompletedEvent(ConversationId ConversationId, DateTime OccurredOn) : IDomainEvent;
