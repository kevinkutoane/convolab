using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Events;

namespace ConvoLab.Domain.Conversation.Entities;

public class ConversationMemory : BaseEntity<MemoryId>
{
    public MemoryStrategy Strategy { get; private set; }
    public MemoryWindow Window { get; private set; }
    public string Content { get; private set; }
    public DateTime LastUpdated { get; private set; }
    public MemoryType Type { get; private set; }

    private ConversationMemory(MemoryId id, MemoryStrategy strategy, MemoryWindow window, string content, MemoryType type)
        : base(id)
    {
        Strategy = strategy;
        Window = window;
        Content = content;
        LastUpdated = DateTime.UtcNow;
        Type = type;
    }

    public static ConversationMemory Create(MemoryStrategy strategy, MemoryWindow window, string content, MemoryType type)
    {
        return new(MemoryId.CreateUnique(), strategy, window, content, type);
    }

    public void UpdateContent(string content)
    {
        Content = content;
        LastUpdated = DateTime.UtcNow;
    }

    private ConversationMemory() { 
        Content = null!;
        Strategy = null!;
        Window = null!;
    }
}

public enum MemoryType
{
    ShortTerm,
    LongTerm,
    Summary
}



public class MemoryStrategy : ValueObject
{
    public string Name { get; private set; }
    public IDictionary<string, string> Parameters { get; private set; }
    private MemoryStrategy(string name, IDictionary<string, string> parameters)
    {
        Name = name;
        Parameters = new Dictionary<string, string>(parameters);
    }
    public static MemoryStrategy Create(string name, IDictionary<string, string>? parameters = null)
        => new(name, parameters ?? new Dictionary<string, string>());
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Name;
        foreach (var kvp in Parameters.OrderBy(x => x.Key))
        {
            yield return kvp.Key;
            yield return kvp.Value;
        }
    }
    private MemoryStrategy() { Name = null!; Parameters = null!; }
}

public class MemoryWindow : ValueObject
{
    public int Size { get; private set; }
    public string Unit { get; private set; } // e.g., "Messages", "Tokens", "Time"
    private MemoryWindow(int size, string unit)
    {
        Size = size;
        Unit = unit;
    }
    public static MemoryWindow Create(int size, string unit) => new(size, unit);
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Size;
        yield return Unit;
    }
    private MemoryWindow() { Unit = null!; }
}
