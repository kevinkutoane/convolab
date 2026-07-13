using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Conversation.ValueObjects;

public class ConversationMetadata : ValueObject
{
    public Dictionary<string, string> Metadata { get; private set; }

    private ConversationMetadata(Dictionary<string, string> metadata)
    {
        Metadata = metadata;
    }

    public static ConversationMetadata Create(Dictionary<string, string> metadata) => new(metadata);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        foreach (var item in Metadata)
        {
            yield return item.Key;
            yield return item.Value;
        }
    }

    private ConversationMetadata() { Metadata = new Dictionary<string, string>(); }
}
