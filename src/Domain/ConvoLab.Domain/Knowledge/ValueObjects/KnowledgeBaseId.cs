using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Knowledge.ValueObjects;

public class KnowledgeBaseId : ValueObject
{
    public Guid Value { get; private set; }

    private KnowledgeBaseId(Guid value)
    {
        Value = value;
    }

    public static KnowledgeBaseId CreateUnique()
    {
        return new KnowledgeBaseId(Guid.NewGuid());
    }

    public static KnowledgeBaseId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("KnowledgeBaseId cannot be empty.", nameof(value));
        }
        return new KnowledgeBaseId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    // For EF Core
    private KnowledgeBaseId() { }

    public static implicit operator Guid(KnowledgeBaseId id) => id.Value;
    public static implicit operator KnowledgeBaseId(Guid value) => new(value);
}
