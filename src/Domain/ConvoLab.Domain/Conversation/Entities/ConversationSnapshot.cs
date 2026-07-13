using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.ValueObjects;

namespace ConvoLab.Domain.Conversation.Entities;

public class ConversationSnapshot : BaseEntity<SnapshotId>
{
    public ConversationId ConversationId { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string SerializedState { get; private set; } // JSON or other serialized representation of conversation state

    private ConversationSnapshot(SnapshotId id, ConversationId conversationId, DateTime timestamp, string serializedState)
        : base(id)
    {
        ConversationId = conversationId;
        Timestamp = timestamp;
        SerializedState = serializedState;
    }

    public static ConversationSnapshot Create(ConversationId conversationId, string serializedState)
    {
        return new(SnapshotId.CreateUnique(), conversationId, DateTime.UtcNow, serializedState);
    }

    private ConversationSnapshot() { ConversationId = null!; SerializedState = null!; }
}
