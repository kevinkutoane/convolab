using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Domain.Knowledge.ValueObjects;
using ConvoLab.Domain.Knowledge.Entities;
namespace ConvoLab.Application.Services;
public class PlaceholderKnowledgeEngine : IKnowledgeEngine {
    public Task<KnowledgeBaseId> CreateKnowledgeBaseAsync(string name, string description, CancellationToken cancellationToken = default) => Task.FromResult(new KnowledgeBaseId(Guid.NewGuid()));
    public Task<KnowledgeItemId> AddKnowledgeItemAsync(KnowledgeBaseId baseId, string title, string content, string source, CancellationToken cancellationToken = default) => Task.FromResult(new KnowledgeItemId(Guid.NewGuid()));
    public Task<IReadOnlyList<KnowledgeItem>> SearchKnowledgeAsync(KnowledgeBaseId baseId, string query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<KnowledgeItem>>(new List<KnowledgeItem>());
    public Task<bool> UpdateKnowledgeItemContentAsync(KnowledgeItemId itemId, string newContent, CancellationToken cancellationToken = default) => Task.FromResult(true);
}
