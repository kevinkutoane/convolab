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

    private Trace() : base()
    {
        OperationName = null!;
    }

    private Trace(TraceId id, string operationName, Guid correlationId) : base(id)
    {
        if (string.IsNullOrWhiteSpace(operationName)) throw new ArgumentException("Operation name is required.", nameof(operationName));
        OperationName = operationName.Trim();
        CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
        StartTime = DateTime.UtcNow;
        Status = TraceStatus.InProgress;
        AddDomainEvent(new TraceStartedEvent(id, OperationName));
    }

    public static Trace Start(string operationName, Guid correlationId)
        => new(TraceId.CreateUnique(), operationName, correlationId);

    public static Trace Restore(
        TraceId id,
        string operationName,
        Guid correlationId,
        DateTime startTime,
        DateTime? endTime,
        TraceStatus status,
        IReadOnlyList<TraceSpan>? spans = null)
    {
        var trace = new Trace(id, operationName, correlationId)
        {
            StartTime = startTime,
            EndTime = endTime,
            Status = status
        };
        trace.ClearDomainEvents();
        if (spans is not null) trace._spans.AddRange(spans);
        return trace;
    }

    public TraceSpan StartSpan(string name, Guid? parentId = null, IReadOnlyDictionary<string, string>? attributes = null)
    {
        if (Status != TraceStatus.InProgress) throw new InvalidOperationException("Cannot add a span to a completed trace.");
        var span = new TraceSpan(Guid.NewGuid(), Id.Value, name, parentId);
        span.AddAttributes(attributes);
        _spans.Add(span);
        return span;
    }

    public void Complete()
    {
        if (Status != TraceStatus.InProgress) return;
        foreach (var span in _spans.Where(item => item.Status == SpanStatus.InProgress)) span.Complete();
        Status = TraceStatus.Completed;
        EndTime = DateTime.UtcNow;
        AddDomainEvent(new TraceCompletedEvent(Id, EndTime.Value));
    }

    public void Fail(string errorMessage)
    {
        if (Status != TraceStatus.InProgress) return;
        foreach (var span in _spans.Where(item => item.Status == SpanStatus.InProgress)) span.Fail(errorMessage);
        Status = TraceStatus.Failed;
        EndTime = DateTime.UtcNow;
    }
}
