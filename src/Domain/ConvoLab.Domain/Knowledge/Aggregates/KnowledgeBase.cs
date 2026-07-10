using ConvoLab.Domain.Common;
using ConvoLab.Domain.Knowledge.ValueObjects;
using ConvoLab.Domain.Knowledge.Entities;
using ConvoLab.Domain.Knowledge.Events;
namespace ConvoLab.Domain.Knowledge.Aggregates;
public class KnowledgeBase : BaseAggregateRoot<KnowledgeBaseId> {
    public string Name { get; private set; }
    public string Description { get; private set; }
    private readonly List<KnowledgeItem> _items = new();
    public IReadOnlyCollection<KnowledgeItem> Items => _items.AsReadOnly();
    private KnowledgeBase() : base() { }
    private KnowledgeBase(KnowledgeBaseId id, string name, string description) : base(id) {
        Name = name; Description = description;
        AddDomainEvent(new KnowledgeBaseCreatedEvent(id, name));
    }
    public static KnowledgeBase Create(string name, string description) => new KnowledgeBase(KnowledgeBaseId.CreateUnique(), name, description);
}
