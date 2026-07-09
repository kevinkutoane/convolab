using ConvoLab.Domain.Events;
namespace ConvoLab.Domain.Common;
public abstract class BaseAggregateRoot<TId> : BaseEntity<TId> where TId : notnull {
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    protected BaseAggregateRoot(TId id) : base(id) { }
    protected BaseAggregateRoot() : base() { }
    protected void AddDomainEvent(IDomainEvent domainEvent) { _domainEvents.Add(domainEvent); }
    public void ClearDomainEvents() { _domainEvents.Clear(); }
}
