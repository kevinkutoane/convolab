using ConvoLab.Domain.Events;
using ConvoLab.Domain.Evaluation.ValueObjects;
using ConvoLab.Domain.Conversation.ValueObjects;
namespace ConvoLab.Domain.Evaluation.Events;
public record EvaluationCompletedEvent(EvaluationId EvaluationId, ConversationId ConversationId) : IDomainEvent {
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
