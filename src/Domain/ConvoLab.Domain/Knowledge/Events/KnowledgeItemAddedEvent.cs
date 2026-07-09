using ConvoLab.Domain.Events;
using ConvoLab.Domain.Knowledge.ValueObjects;
namespace ConvoLab.Domain.Knowledge.Events;
public record KnowledgeItemAddedEvent(KnowledgeBaseId KnowledgeBaseId, KnowledgeItemId KnowledgeItemId, string Title) : IDomainEvent {
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
