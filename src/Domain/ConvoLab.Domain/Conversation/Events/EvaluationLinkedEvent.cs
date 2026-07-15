using ConvoLab.Domain.Events;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Evaluation.ValueObjects;

namespace ConvoLab.Domain.Conversation.Events;

public record EvaluationLinkedEvent(ConversationId ConversationId, EvaluationId EvaluationId, DateTime OccurredOn) : IDomainEvent;
