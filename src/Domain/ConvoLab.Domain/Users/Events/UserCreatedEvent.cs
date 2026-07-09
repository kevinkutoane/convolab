using ConvoLab.Domain.Events;
using ConvoLab.Domain.Users.ValueObjects;
namespace ConvoLab.Domain.Users.Events;
public record UserCreatedEvent(UserId UserId, string Username, string Email) : IDomainEvent {
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
