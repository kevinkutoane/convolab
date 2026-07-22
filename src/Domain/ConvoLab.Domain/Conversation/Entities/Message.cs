using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;
namespace ConvoLab.Domain.Conversation.Entities;
public class Message : BaseEntity<Guid> {
    public UserId SenderId { get; private set; } = null!;
    public MessageContent Content { get; private set; } = null!;
    public DateTime Timestamp { get; private set; }
    private Message() { }
    private Message(Guid id, UserId senderId, MessageContent content, DateTime timestamp) : base(id) {
        SenderId = senderId; Content = content; Timestamp = timestamp;
    }
    public static Message Create(UserId senderId, MessageContent content) => new Message(Guid.NewGuid(), senderId, content, DateTime.UtcNow);
}
