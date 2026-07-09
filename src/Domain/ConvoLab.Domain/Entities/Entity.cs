using ConvoLab.Domain.Events;

namespace ConvoLab.Domain.Entities;

/// <summary>
/// Base class for all domain entities.
/// Entities have a unique identity and a lifecycle within the domain.
/// </summary>
public abstract class Entity
{
    /// <summary>
    /// The unique identifier for this entity.
    /// </summary>
    public int Id { get; protected set; }

    /// <summary>
    /// The date and time when this entity was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; protected set; }

    /// <summary>
    /// The date and time when this entity was last updated (UTC).
    /// </summary>
    public DateTime? UpdatedAt { get; protected set; }

    /// <summary>
    /// Collection of domain events that occurred on this entity.
    /// </summary>
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Gets a read-only collection of domain events.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Protected constructor for derived classes.
    /// </summary>
    protected Entity()
    {
    }

    /// <summary>
    /// Protected constructor for creating a new entity with a generated ID.
    /// </summary>
    protected Entity(int id)
    {
        Id = id;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a domain event to the entity.
    /// </summary>
    /// <param name="domainEvent">The domain event to add.</param>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears all domain events from the entity.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current entity.
    /// Entities are equal if they have the same ID.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return Id == other.Id && Id != 0;
    }

    /// <summary>
    /// Serves as the default hash function.
    /// </summary>
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    /// <summary>
    /// Returns a string representation of the entity.
    /// </summary>
    public override string ToString()
    {
        return $"{GetType().Name} [Id={Id}]";
    }
}
