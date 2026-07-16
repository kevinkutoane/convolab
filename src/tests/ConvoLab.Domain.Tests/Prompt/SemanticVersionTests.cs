using ConvoLab.Domain.Prompt.ValueObjects;
using Xunit;

namespace ConvoLab.Domain.Tests.Prompt;

public class SemanticVersionTests
{
    [Fact]
    public void Initial_Should_Return_1_0_0()
    {
        var version = SemanticVersion.Initial();

        Assert.Equal("1.0.0", version.ToString());
    }

    [Fact]
    public void IncrementMajor_Should_Reset_Minor_And_Patch()
    {
        var version = SemanticVersion.Create(1, 5, 3);
        var incremented = version.IncrementMajor();

        Assert.Equal("2.0.0", incremented.ToString());
    }

    [Fact]
    public void IncrementMinor_Should_Reset_Patch()
    {
        var version = SemanticVersion.Create(1, 2, 4);
        var incremented = version.IncrementMinor();

        Assert.Equal("1.3.0", incremented.ToString());
    }

    [Fact]
    public void IncrementPatch_Should_Only_Increment_Patch()
    {
        var version = SemanticVersion.Create(2, 1, 0);
        var incremented = version.IncrementPatch();

        Assert.Equal("2.1.1", incremented.ToString());
    }

    [Fact]
    public void Parse_Valid_String_Should_Succeed()
    {
        var version = SemanticVersion.Parse("3.2.1");

        Assert.Equal(3, version.Major);
        Assert.Equal(2, version.Minor);
        Assert.Equal(1, version.Patch);
    }

    [Fact]
    public void Parse_Invalid_String_Should_Throw()
    {
        Assert.Throws<ArgumentException>(() => SemanticVersion.Parse("1.0"));
    }

    [Fact]
    public void Negative_Major_Should_Throw()
    {
        Assert.Throws<ArgumentException>(() => SemanticVersion.Create(-1, 0, 0));
    }

    [Fact]
    public void Two_Versions_With_Same_Values_Should_Be_Equal()
    {
        var v1 = SemanticVersion.Create(1, 2, 3);
        var v2 = SemanticVersion.Create(1, 2, 3);

        Assert.Equal(v1, v2);
    }

    [Fact]
    public void Two_Versions_With_Different_Values_Should_Not_Be_Equal()
    {
        var v1 = SemanticVersion.Create(1, 2, 3);
        var v2 = SemanticVersion.Create(1, 2, 4);

        Assert.NotEqual(v1, v2);
    }
}
