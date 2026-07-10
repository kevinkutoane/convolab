using ConvoLab.Domain.Common;
using ConvoLab.Domain.Execution.ValueObjects;
using ConvoLab.Domain.Events;

namespace ConvoLab.Domain.Execution.Events;

public record ExecutionFailed(ExecutionId ExecutionId, string ErrorMessage) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
