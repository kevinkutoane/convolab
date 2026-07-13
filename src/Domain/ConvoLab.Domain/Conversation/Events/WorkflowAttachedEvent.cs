using ConvoLab.Domain.Common;
using ConvoLab.Domain.Events;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Execution.ValueObjects;

namespace ConvoLab.Domain.Conversation.Events;

public record WorkflowAttachedEvent(ConversationId ConversationId, ExecutionId WorkflowExecutionId, DateTime AttachedAt) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
