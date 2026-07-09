using MediatR;

namespace ConvoLab.Domain.Events;

public interface IDomainEvent : INotification
{
    DateTime OccurredOn { get; }
}
