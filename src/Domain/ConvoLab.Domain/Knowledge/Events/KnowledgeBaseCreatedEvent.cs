using ConvoLab.Domain.Events;
using ConvoLab.Domain.Knowledge.ValueObjects;
namespace ConvoLab.Domain.Knowledge.Events;
public record KnowledgeBaseCreatedEvent(KnowledgeBaseId KnowledgeBaseId, string Name) : IDomainEvent {
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
