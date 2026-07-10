using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Tracing.ValueObjects;

public class TraceId : ValueObject
{
    public Guid Value { get; private set; }

    private TraceId(Guid value)
    {
        Value = value;
    }

    public static TraceId CreateUnique()
    {
        return new TraceId(Guid.NewGuid());
    }

    public static TraceId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("TraceId cannot be empty.", nameof(value));
        }
        return new TraceId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    // For EF Core
    private TraceId() { }

    public static implicit operator Guid(TraceId id) => id.Value;
    public static implicit operator TraceId(Guid value) => new(value);
}
