using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Tracing.ValueObjects;

public class Span : ValueObject
{
    public Guid Id { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Name { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public TimeSpan? Duration => EndTime - StartTime;
    
    private readonly List<TraceEvent> _events = new();
    public IReadOnlyCollection<TraceEvent> Events => _events.AsReadOnly();
    
    private readonly List<Metric> _metrics = new();
    public IReadOnlyCollection<Metric> Metrics => _metrics.AsReadOnly();

    public Span(Guid id, string name, Guid? parentId = null)
    {
        Id = id;
        Name = name;
        ParentId = parentId;
        StartTime = DateTime.UtcNow;
    }

    public void End() => EndTime = DateTime.UtcNow;

    public void AddEvent(string name, IReadOnlyDictionary<string, string>? attributes = null)
    {
        _events.Add(TraceEvent.Create(name, attributes));
    }

    public void AddMetric(string name, double value, string? unit = null)
    {
        _metrics.Add(Metric.Create(name, value, unit));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Id;
    }

    private Span() { Id = Guid.Empty; Name = null!; }
}

public class TraceEvent : ValueObject
{
    public string Name { get; private set; }
    public DateTime Timestamp { get; private set; }
    public IReadOnlyDictionary<string, string> Attributes { get; private set; }

    private TraceEvent(string name, IReadOnlyDictionary<string, string>? attributes = null)
    {
        Name = name;
        Timestamp = DateTime.UtcNow;
        Attributes = attributes ?? new Dictionary<string, string>();
    }

    public static TraceEvent Create(string name, IReadOnlyDictionary<string, string>? attributes = null) => 
        new(name, attributes);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Name;
        yield return Timestamp;
    }

    private TraceEvent() { Name = null!; Attributes = new Dictionary<string, string>(); }
}

public class Metric : ValueObject
{
    public string Name { get; private set; }
    public double Value { get; private set; }
    public string? Unit { get; private set; }
    public DateTime Timestamp { get; private set; }

    private Metric(string name, double value, string? unit = null)
    {
        Name = name;
        Value = value;
        Unit = unit;
        Timestamp = DateTime.UtcNow;
    }

    public static Metric Create(string name, double value, string? unit = null) => 
        new(name, value, unit);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Name;
        yield return Value;
        yield return Unit ?? string.Empty;
        yield return Timestamp;
    }

    private Metric() { Name = null!; }
}

public class Artifact : ValueObject
{
    public string Name { get; private set; }
    public string ContentType { get; private set; }
    public byte[] Data { get; private set; }
    public string? StorageUrl { get; private set; }

    private Artifact(string name, string contentType, byte[] data, string? storageUrl = null)
    {
        Name = name;
        ContentType = contentType;
        Data = data;
        StorageUrl = storageUrl;
    }

    public static Artifact Create(string name, string contentType, byte[] data, string? storageUrl = null) => 
        new(name, contentType, data, storageUrl);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Name;
        yield return ContentType;
        yield return StorageUrl ?? string.Empty;
    }

    private Artifact() { Name = null!; ContentType = null!; Data = null!; }
}
