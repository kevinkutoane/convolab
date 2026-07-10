using ConvoLab.Domain.Common;
using ConvoLab.Domain.Knowledge.ValueObjects;
namespace ConvoLab.Domain.Knowledge.Entities;
public class KnowledgeItem : BaseEntity<KnowledgeItemId> {
    public string Title { get; private set; }
    public string Content { get; private set; }
    public string Source { get; private set; }
    private KnowledgeItem() { }
    private KnowledgeItem(KnowledgeItemId id, string title, string content, string source) : base(id) {
        Title = title; Content = content; Source = source;
    }
    public static KnowledgeItem Create(string title, string content, string source) => new KnowledgeItem(KnowledgeItemId.CreateUnique(), title, content, source);
}
