using ConvoLab.Domain.Conversation.Aggregates;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;

namespace ConvoLab.Domain.Conversation.Interfaces;

public interface IConversationFactory
{
    Aggregates.Conversation CreateConversation(UserId creatorId, string title, ConversationMetadata metadata, ConversationWindow window, ConversationContext context);
}
