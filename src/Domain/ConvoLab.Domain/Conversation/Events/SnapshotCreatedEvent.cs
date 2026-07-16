using ConvoLab.Domain.Events;
using ConvoLab.Domain.Conversation.ValueObjects;

namespace ConvoLab.Domain.Conversation.Events;

public record SnapshotCreatedEvent(ConversationId ConversationId, SnapshotId SnapshotId, DateTime OccurredOn) : IDomainEvent;
