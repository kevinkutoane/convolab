using ConvoLab.Domain.Common;
using ConvoLab.Domain.Tracing.ValueObjects;
using ConvoLab.Domain.Tracing.Entities;
using ConvoLab.Domain.Tracing.Enums;
using ConvoLab.Domain.Tracing.Events;

namespace ConvoLab.Domain.Tracing.Aggregates;

public class Trace : BaseAggregateRoot<TraceId>
{
    public string OperationName { get; private set; }
    public Guid CorrelationId { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public TraceStatus Status { get; private set; }

    private readonly List<TraceSpan> _spans = new();
    public IReadOnlyCollection<TraceSpan> Spans => _spans.AsReadOnly();

    private Trace() : base() { 
        OperationName = null!;
    }

    private Trace(TraceId id, string operationName, Guid correlationId) : base(id)
    {
        OperationName = operationName;
        CorrelationId = correlationId;
        StartTime = DateTime.UtcNow;
        Status = TraceStatus.InProgress;
        AddDomainEvent(new TraceStartedEvent(id, operationName));
    }

    public static Trace Start(string operationName, Guid correlationId) => 
        new Trace(TraceId.CreateUnique(), operationName, correlationId);

    public TraceSpan StartSpan(string name, Guid? parentId = null)
    {
        var span = new TraceSpan(Guid.NewGuid(), Id.Value, name, parentId);
        _spans.Add(span);
        return span;
    }

    public void Complete()
    {
        Status = TraceStatus.Completed;
        EndTime = DateTime.UtcNow;
        AddDomainEvent(new TraceCompletedEvent(Id, EndTime.Value));
    }
}
