using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Plugins.ValueObjects;

public class PluginVersion : ValueObject
{
    public string Value { get; private set; } = string.Empty;

    private PluginVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("PluginVersion cannot be empty.", nameof(value));
        }
        Value = value;
    }

    public static PluginVersion FromString(string value)
    {
        return new PluginVersion(value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    // For EF Core
    private PluginVersion() { }

    public static implicit operator string(PluginVersion version) => version.Value;
    public static implicit operator PluginVersion(string value) => new(value);
}
