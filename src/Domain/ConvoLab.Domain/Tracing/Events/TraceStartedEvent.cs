using ConvoLab.Domain.Events;
using ConvoLab.Domain.Tracing.ValueObjects;
namespace ConvoLab.Domain.Tracing.Events;
public record TraceStartedEvent(TraceId TraceId, string OperationName) : IDomainEvent {
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
