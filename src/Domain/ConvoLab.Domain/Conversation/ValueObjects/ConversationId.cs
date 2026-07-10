using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Conversation.ValueObjects;

public class ConversationId : ValueObject
{
    public Guid Value { get; private set; }

    private ConversationId(Guid value)
    {
        Value = value;
    }

    public static ConversationId CreateUnique()
    {
        return new ConversationId(Guid.NewGuid());
    }

    public static ConversationId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("ConversationId cannot be empty.", nameof(value));
        }
        return new ConversationId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    // For EF Core
    private ConversationId() { }

    public static implicit operator Guid(ConversationId id) => id.Value;
    public static implicit operator ConversationId(Guid value) => new(value);
}
