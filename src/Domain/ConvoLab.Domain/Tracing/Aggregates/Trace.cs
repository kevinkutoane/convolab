using ConvoLab.Domain.Common;
using ConvoLab.Domain.Tracing.ValueObjects;
using ConvoLab.Domain.Tracing.Entities;
using ConvoLab.Domain.Tracing.Enums;
using ConvoLab.Domain.Tracing.Events;
namespace ConvoLab.Domain.Tracing.Aggregates;
public class Trace : BaseAggregateRoot<TraceId> {
    public string OperationName { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public TraceStatus Status { get; private set; }
    private readonly List<TraceSpan> _spans = new();
    public IReadOnlyCollection<TraceSpan> Spans => _spans.AsReadOnly();
    private Trace() : base() { }
    private Trace(TraceId id, string operationName) : base(id) {
        OperationName = operationName; StartTime = DateTime.UtcNow; Status = TraceStatus.InProgress;
        AddDomainEvent(new TraceStartedEvent(id, operationName));
    }
    public static Trace Start(string operationName) => new Trace(new TraceId(Guid.NewGuid()), operationName);
}
