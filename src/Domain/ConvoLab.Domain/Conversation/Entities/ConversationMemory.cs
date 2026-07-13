using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.Enums;
using ConvoLab.Domain.Conversation.ValueObjects;

namespace ConvoLab.Domain.Conversation.Entities;

public class ConversationMemory : BaseEntity<MemoryId>
{
    public MemoryStrategy Strategy { get; private set; }
    public MemoryWindow Window { get; private set; }
    public string Content { get; private set; } // Represents the summarized or raw memory content
    public DateTime LastUpdated { get; private set; }

    private ConversationMemory(MemoryId id, MemoryStrategy strategy, MemoryWindow window, string content, DateTime lastUpdated)
        : base(id)
    {
        Strategy = strategy;
        Window = window;
        Content = content;
        LastUpdated = lastUpdated;
    }

    public static ConversationMemory Create(MemoryStrategy strategy, MemoryWindow window, string content)
    {
        return new(MemoryId.CreateUnique(), strategy, window, content, DateTime.UtcNow);
    }

    public void UpdateContent(string newContent)
    {
        Content = newContent;
        LastUpdated = DateTime.UtcNow;
    }

    private ConversationMemory() { 
        Window = null!;
        Content = null!;
    }
}
