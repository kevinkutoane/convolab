using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Conversation.ValueObjects;

public class MessageContent : ValueObject
{
    public string Value { get; private set; } = null!;

    private MessageContent(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("MessageContent cannot be empty.", nameof(value));
        }
        Value = value;
    }

    public static MessageContent FromString(string value)
    {
        return new MessageContent(value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    // For EF Core
    private MessageContent() { }

    public static implicit operator string(MessageContent content) => content.Value;
    public static implicit operator MessageContent(string value) => new(value);
}
