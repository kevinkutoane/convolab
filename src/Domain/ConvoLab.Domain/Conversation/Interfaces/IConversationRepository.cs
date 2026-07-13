using ConvoLab.Domain.Conversation.Aggregates;
using ConvoLab.Domain.Conversation.ValueObjects;

namespace ConvoLab.Domain.Conversation.Interfaces;

public interface IConversationRepository
{
    Task<Aggregates.Conversation?> GetByIdAsync(ConversationId id);
    Task AddAsync(Aggregates.Conversation conversation);
    Task UpdateAsync(Aggregates.Conversation conversation);
    Task DeleteAsync(ConversationId id);
}
