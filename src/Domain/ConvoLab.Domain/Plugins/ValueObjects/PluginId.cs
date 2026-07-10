using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Plugins.ValueObjects;

public class PluginId : ValueObject
{
    public Guid Value { get; private set; }

    private PluginId(Guid value)
    {
        Value = value;
    }

    public static PluginId CreateUnique()
    {
        return new PluginId(Guid.NewGuid());
    }

    public static PluginId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("PluginId cannot be empty.", nameof(value));
        }
        return new PluginId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    // For EF Core
    private PluginId() { }

    public static implicit operator Guid(PluginId id) => id.Value;
    public static implicit operator PluginId(Guid value) => new(value);
}
