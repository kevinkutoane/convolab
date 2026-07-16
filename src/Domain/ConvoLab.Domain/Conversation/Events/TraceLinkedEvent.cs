using ConvoLab.Domain.Events;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Tracing.ValueObjects;

namespace ConvoLab.Domain.Conversation.Events;

public record TraceLinkedEvent(ConversationId ConversationId, TraceId TraceId, DateTime OccurredOn) : IDomainEvent;
