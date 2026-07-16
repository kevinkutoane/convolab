using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Conversation.ValueObjects;

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

    // For EF Core
    private MemoryWindow() { Unit = null!; }
}
