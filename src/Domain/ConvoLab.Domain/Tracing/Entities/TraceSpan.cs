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
        TraceId = traceId;
        Name = name;
        StartTime = DateTime.UtcNow;
        Status = SpanStatus.InProgress;
        ParentSpanId = parentSpanId;
        Attributes = new Dictionary<string, string>();
    }

    public void Complete()
    {
        Status = SpanStatus.Completed;
        EndTime = DateTime.UtcNow;
    }

    // For EF Core
    private TraceSpan() { 
        Name = null!;
        Attributes = new Dictionary<string, string>();
    }
}
