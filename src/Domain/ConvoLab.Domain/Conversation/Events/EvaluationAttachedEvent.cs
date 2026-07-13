using ConvoLab.Domain.Common;
using ConvoLab.Domain.Events;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Evaluation.ValueObjects;

namespace ConvoLab.Domain.Conversation.Events;

public record EvaluationAttachedEvent(ConversationId ConversationId, EvaluationId EvaluationId, DateTime AttachedAt) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
