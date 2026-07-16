using ConvoLab.Domain.Events;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;

namespace ConvoLab.Domain.Conversation.Events;

public record ParticipantRemovedEvent(ConversationId ConversationId, ParticipantId ParticipantId, UserId UserId, DateTime OccurredOn) : IDomainEvent;
