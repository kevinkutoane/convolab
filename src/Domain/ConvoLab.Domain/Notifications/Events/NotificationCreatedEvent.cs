using ConvoLab.Domain.Events;

namespace ConvoLab.Domain.Notifications.Events;

public record NotificationCreatedEvent(Notification Notification) : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
