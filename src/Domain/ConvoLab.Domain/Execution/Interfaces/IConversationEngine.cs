using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;

namespace ConvoLab.Domain.Execution.Interfaces;

public interface IConversationEngine
{
    Task<ConversationId> GetOrCreateConversationAsync(ValueObjects.ExecutionContext context);
    Task AddMessageAsync(ConversationId conversationId, string content, UserId senderId, ValueObjects.ExecutionContext context);
}
