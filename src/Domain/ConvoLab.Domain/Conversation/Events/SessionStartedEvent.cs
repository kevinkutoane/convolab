using ConvoLab.Domain.Common;
using ConvoLab.Domain.Events;
using ConvoLab.Domain.Conversation.ValueObjects;

namespace ConvoLab.Domain.Conversation.Events;

public record SessionStartedEvent(ConversationId ConversationId, SessionId SessionId, DateTime StartedAt) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
