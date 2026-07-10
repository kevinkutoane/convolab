using ConvoLab.Domain.Common;
using ConvoLab.Domain.Prompt.ValueObjects;
using ConvoLab.Domain.Execution.ValueObjects;
using ConvoLab.Domain.Events;

namespace ConvoLab.Domain.Execution.Events;

public record PromptRequested(ExecutionId ExecutionId, PromptTemplateId TemplateId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
