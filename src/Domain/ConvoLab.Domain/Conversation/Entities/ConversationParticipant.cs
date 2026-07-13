using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.Enums;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;

namespace ConvoLab.Domain.Conversation.Entities;

public class ConversationParticipant : BaseEntity<ParticipantId>
{
    public UserId UserId { get; private set; }
    public ParticipantRole Role { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime? LeftAt { get; private set; }

    private ConversationParticipant(ParticipantId id, UserId userId, ParticipantRole role, DateTime joinedAt)
        : base(id)
    {
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
    }

    public static ConversationParticipant Create(UserId userId, ParticipantRole role)
    {
        return new(ParticipantId.CreateUnique(), userId, role, DateTime.UtcNow);
    }

    public void LeaveConversation()
    {
        LeftAt = DateTime.UtcNow;
    }

    private ConversationParticipant() { UserId = null!; }
}
