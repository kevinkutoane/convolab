using ConvoLab.Domain.Common;
using ConvoLab.Domain.Events;
using ConvoLab.Domain.Conversation.ValueObjects;

namespace ConvoLab.Domain.Conversation.Events;

public record MemoryUpdatedEvent(ConversationId ConversationId, MemoryId MemoryId, DateTime OccurredOn) : IDomainEvent;
