using ConvoLab.Domain.Common;
using ConvoLab.Domain.AI.ValueObjects;
using ConvoLab.Domain.Execution.ValueObjects;
using ConvoLab.Domain.Events;

namespace ConvoLab.Domain.Execution.Events;

public record AIRequested(ExecutionId ExecutionId, AIModelId ModelId, string Prompt) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
