using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;
using ConvoLab.Domain.Conversation.Entities;
namespace ConvoLab.Application.Services;
public class PlaceholderConversationEngine : IConversationEngine {
    public Task<ConversationId> StartConversationAsync(string title, UserId initialParticipantId, CancellationToken cancellationToken = default) => Task.FromResult(new ConversationId(Guid.NewGuid()));
    public Task AddMessageToConversationAsync(ConversationId conversationId, UserId senderId, string content, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<Message>> GetConversationMessagesAsync(ConversationId conversationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Message>>(new List<Message>());
    public Task EndConversationAsync(ConversationId conversationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
