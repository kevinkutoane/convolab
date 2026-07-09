using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;
namespace ConvoLab.Application.Common.Interfaces;
public interface IWorkflowEngine {
    Task<string> ProcessUserMessageAsync(ConversationId conversationId, UserId userId, string userMessage, CancellationToken cancellationToken = default);
}
