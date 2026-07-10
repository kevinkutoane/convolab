using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Knowledge.ValueObjects;

public class KnowledgeItemId : ValueObject
{
    public Guid Value { get; private set; }

    private KnowledgeItemId(Guid value)
    {
        Value = value;
    }

    public static KnowledgeItemId CreateUnique()
    {
        return new KnowledgeItemId(Guid.NewGuid());
    }

    public static KnowledgeItemId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("KnowledgeItemId cannot be empty.", nameof(value));
        }
        return new KnowledgeItemId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    // For EF Core
    private KnowledgeItemId() { }

    public static implicit operator Guid(KnowledgeItemId id) => id.Value;
    public static implicit operator KnowledgeItemId(Guid value) => new(value);
}
