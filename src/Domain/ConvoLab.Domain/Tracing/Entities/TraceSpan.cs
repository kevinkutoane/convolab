using ConvoLab.Domain.Common;
using ConvoLab.Domain.Tracing.Enums;

namespace ConvoLab.Domain.Tracing.Entities;

public class TraceSpan : BaseEntity<Guid>
{
    public Guid TraceId { get; private set; }
    public string Name { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public SpanStatus Status { get; private set; }
    public Guid? ParentSpanId { get; private set; }
    public Dictionary<string, string> Attributes { get; private set; }

    public TraceSpan(Guid id, Guid traceId, string name, Guid? parentSpanId = null) : base(id)
    {
        if (traceId == Guid.Empty) throw new ArgumentException("Trace id is required.", nameof(traceId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Span name is required.", nameof(name));
        TraceId = traceId;
        Name = name.Trim();
        StartTime = DateTime.UtcNow;
        Status = SpanStatus.InProgress;
        ParentSpanId = parentSpanId;
        Attributes = new Dictionary<string, string>();
    }

    public static TraceSpan Restore(
        Guid id,
        Guid traceId,
        string name,
        DateTime startTime,
        DateTime? endTime,
        SpanStatus status,
        Guid? parentSpanId,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        var span = new TraceSpan(id, traceId, name, parentSpanId)
        {
            StartTime = startTime,
            EndTime = endTime,
            Status = status,
            Attributes = attributes is null ? new Dictionary<string, string>() : new Dictionary<string, string>(attributes)
        };
        return span;
    }

    public void AddAttributes(IReadOnlyDictionary<string, string>? attributes)
    {
        if (attributes is null) return;
        foreach (var pair in attributes) Attributes[pair.Key] = pair.Value;
    }

    public void Complete()
    {
        if (Status != SpanStatus.InProgress) return;
        Status = SpanStatus.Completed;
        EndTime = DateTime.UtcNow;
    }

    public void Fail(string? errorMessage = null)
    {
        if (Status != SpanStatus.InProgress) return;
        Status = SpanStatus.Failed;
        EndTime = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(errorMessage)) Attributes["error.message"] = errorMessage;
    }

    private TraceSpan()
    {
        Name = null!;
        Attributes = new Dictionary<string, string>();
    }
}
