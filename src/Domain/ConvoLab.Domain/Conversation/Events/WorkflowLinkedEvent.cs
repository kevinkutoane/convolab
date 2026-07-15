using ConvoLab.Domain.Events;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Execution.ValueObjects;

namespace ConvoLab.Domain.Conversation.Events;

public record WorkflowLinkedEvent(ConversationId ConversationId, ExecutionId ExecutionId, DateTime OccurredOn) : IDomainEvent;
