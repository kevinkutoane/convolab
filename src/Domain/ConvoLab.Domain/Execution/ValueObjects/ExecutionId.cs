using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Execution.ValueObjects;

public class ExecutionId : ValueObject
{
    public Guid Value { get; private set; }

    private ExecutionId(Guid value)
    {
        Value = value;
    }

    public static ExecutionId CreateUnique()
    {
        return new ExecutionId(Guid.NewGuid());
    }

    public static ExecutionId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("ExecutionId cannot be empty.", nameof(value));
        }
        return new ExecutionId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    // For EF Core
    private ExecutionId() { }
}
