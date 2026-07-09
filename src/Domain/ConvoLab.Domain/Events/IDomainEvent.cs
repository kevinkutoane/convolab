namespace ConvoLab.Domain.Events;

/// <summary>
/// Marker interface for domain events.
/// Domain events represent something significant that has occurred in the business domain.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// The date and time when the event occurred (UTC).
    /// </summary>
    DateTime OccurredAt { get; }
}
