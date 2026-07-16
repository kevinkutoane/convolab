using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Prompt.ValueObjects;

/// <summary>
/// Represents a semantic version (MAJOR.MINOR.PATCH) for prompt versioning.
/// Prompts are immutable; every edit creates a new version.
/// </summary>
public class SemanticVersion : ValueObject
{
    public int Major { get; private set; }
    public int Minor { get; private set; }
    public int Patch { get; private set; }

    private SemanticVersion(int major, int minor, int patch)
    {
        if (major < 0) throw new ArgumentException("Major version cannot be negative.", nameof(major));
        if (minor < 0) throw new ArgumentException("Minor version cannot be negative.", nameof(minor));
        if (patch < 0) throw new ArgumentException("Patch version cannot be negative.", nameof(patch));

        Major = major;
        Minor = minor;
        Patch = patch;
    }

    public static SemanticVersion Initial() => new(1, 0, 0);

    public static SemanticVersion Create(int major, int minor, int patch) => new(major, minor, patch);

    public static SemanticVersion Parse(string version)
    {
        var parts = version.Split('.');
        if (parts.Length != 3)
            throw new ArgumentException($"Invalid semantic version format: '{version}'. Expected MAJOR.MINOR.PATCH.", nameof(version));

        return new SemanticVersion(
            int.Parse(parts[0]),
            int.Parse(parts[1]),
            int.Parse(parts[2]));
    }

    public SemanticVersion IncrementMajor() => new(Major + 1, 0, 0);
    public SemanticVersion IncrementMinor() => new(Major, Minor + 1, 0);
    public SemanticVersion IncrementPatch() => new(Major, Minor, Patch + 1);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Major;
        yield return Minor;
        yield return Patch;
    }

    private SemanticVersion() { }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
