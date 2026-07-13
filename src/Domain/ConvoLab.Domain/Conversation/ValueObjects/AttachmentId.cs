using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Conversation.ValueObjects;

public class AttachmentId : ValueObject
{
    public Guid Value { get; private set; }

    private AttachmentId(Guid value)
    {
        Value = value;
    }

    public static AttachmentId CreateUnique() => new(Guid.NewGuid());

    public static AttachmentId Create(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    private AttachmentId() { }
}
