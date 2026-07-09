using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;
using ConvoLab.Domain.Conversation.Entities;
namespace ConvoLab.Application.Common.Interfaces;
public interface IConversationEngine {
    Task<ConversationId> StartConversationAsync(string title, UserId initialParticipantId, CancellationToken cancellationToken = default);
    Task AddMessageToConversationAsync(ConversationId conversationId, UserId senderId, string content, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Message>> GetConversationMessagesAsync(ConversationId conversationId, CancellationToken cancellationToken = default);
    Task EndConversationAsync(ConversationId conversationId, CancellationToken cancellationToken = default);
}
