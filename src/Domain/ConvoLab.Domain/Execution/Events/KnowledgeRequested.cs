using ConvoLab.Domain.Common;
using ConvoLab.Domain.Knowledge.ValueObjects;
using ConvoLab.Domain.Execution.ValueObjects;
using ConvoLab.Domain.Events;

namespace ConvoLab.Domain.Execution.Events;

public record KnowledgeRequested(ExecutionId ExecutionId, string Query) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
