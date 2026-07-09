using ConvoLab.Domain.Knowledge.ValueObjects;
using ConvoLab.Domain.Knowledge.Entities;
namespace ConvoLab.Application.Common.Interfaces;
public interface IKnowledgeEngine {
    Task<KnowledgeBaseId> CreateKnowledgeBaseAsync(string name, string description, CancellationToken cancellationToken = default);
    Task<KnowledgeItemId> AddKnowledgeItemAsync(KnowledgeBaseId baseId, string title, string content, string source, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeItem>> SearchKnowledgeAsync(KnowledgeBaseId baseId, string query, CancellationToken cancellationToken = default);
    Task<bool> UpdateKnowledgeItemContentAsync(KnowledgeItemId itemId, string newContent, CancellationToken cancellationToken = default);
}
