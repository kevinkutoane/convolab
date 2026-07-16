using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.Enums;
using ConvoLab.Domain.Conversation.ValueObjects;

namespace ConvoLab.Domain.Conversation.Entities;

public class ConversationMemory : BaseEntity<MemoryId>
{
    public MemoryStrategy Strategy { get; private set; }
    public MemoryWindow Window { get; private set; }
    public string Content { get; private set; }
    public DateTime LastUpdated { get; private set; }
    public MemoryType Type { get; private set; }
    public bool IsPinned { get; private set; }
    public string? SemanticReference { get; private set; }

    private ConversationMemory(MemoryId id, MemoryStrategy strategy, MemoryWindow window, string content, MemoryType type, bool isPinned = false, string? semanticReference = null)
        : base(id)
    {
        Strategy = strategy;
        Window = window;
        Content = content;
        LastUpdated = DateTime.UtcNow;
        Type = type;
        IsPinned = isPinned;
        SemanticReference = semanticReference;
    }

    public static ConversationMemory Create(MemoryStrategy strategy, MemoryWindow window, string content, MemoryType type, bool isPinned = false, string? semanticReference = null)
    {
        return new(MemoryId.CreateUnique(), strategy, window, content, type, isPinned, semanticReference);
    }

    public void UpdateContent(string content)
    {
        Content = content;
        LastUpdated = DateTime.UtcNow;
    }

    public void Pin() => IsPinned = true;
    public void Unpin() => IsPinned = false;

    private ConversationMemory()
    {
        Content = null!;
        Strategy = null!;
        Window = null!;
    }
}
