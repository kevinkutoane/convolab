using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Conversation.ValueObjects;

public class SnapshotId : ValueObject
{
    public Guid Value { get; private set; }

    private SnapshotId(Guid value)
    {
        Value = value;
    }

    public static SnapshotId CreateUnique() => new(Guid.NewGuid());

    public static SnapshotId Create(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    private SnapshotId() { }
}
