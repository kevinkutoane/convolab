using ConvoLab.Domain.Events;
using ConvoLab.Domain.Tracing.ValueObjects;
namespace ConvoLab.Domain.Tracing.Events;
public record TraceCompletedEvent(TraceId TraceId, string OperationName) : IDomainEvent {
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
