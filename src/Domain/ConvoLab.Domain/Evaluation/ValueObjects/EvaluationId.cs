using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Evaluation.ValueObjects;

public class EvaluationId : ValueObject
{
    public Guid Value { get; private set; }

    private EvaluationId(Guid value)
    {
        Value = value;
    }

    public static EvaluationId CreateUnique()
    {
        return new EvaluationId(Guid.NewGuid());
    }

    public static EvaluationId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("EvaluationId cannot be empty.", nameof(value));
        }
        return new EvaluationId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    // For EF Core
    private EvaluationId() { }

    public static implicit operator Guid(EvaluationId id) => id.Value;
    public static implicit operator EvaluationId(Guid value) => new(value);
}
