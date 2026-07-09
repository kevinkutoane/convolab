using ConvoLab.Domain.Common;
using ConvoLab.Domain.Tracing.Enums;
namespace ConvoLab.Domain.Tracing.Entities;
public class TraceSpan : BaseEntity<Guid> {
    public string Name { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public SpanStatus Status { get; private set; }
    public string? ParentSpanId { get; private set; }
    public Dictionary<string, string> Attributes { get; private set; }
    private TraceSpan() { }
    private TraceSpan(Guid id, string name, DateTime startTime, SpanStatus status, string? parentSpanId, Dictionary<string, string> attributes) : base(id) {
        Name = name; StartTime = startTime; Status = status; ParentSpanId = parentSpanId; Attributes = attributes ?? new Dictionary<string, string>();
    }
    public static TraceSpan Start(string name, string? parentSpanId = null, Dictionary<string, string>? attributes = null) => new TraceSpan(Guid.NewGuid(), name, DateTime.UtcNow, SpanStatus.InProgress, parentSpanId, attributes ?? new Dictionary<string, string>());
}
