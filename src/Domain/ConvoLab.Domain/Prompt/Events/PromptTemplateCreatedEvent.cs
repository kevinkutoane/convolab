using ConvoLab.Domain.Events;
using ConvoLab.Domain.Prompt.ValueObjects;
using ConvoLab.Domain.Prompt.Enums;
namespace ConvoLab.Domain.Prompt.Events;
public record PromptTemplateCreatedEvent(PromptTemplateId PromptTemplateId, string Name, PromptType Type) : IDomainEvent {
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
