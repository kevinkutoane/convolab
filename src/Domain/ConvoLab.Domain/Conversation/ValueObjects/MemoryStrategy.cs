using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Conversation.ValueObjects;

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

    // For EF Core
    private MemoryStrategy() { Name = null!; Parameters = null!; }
}
