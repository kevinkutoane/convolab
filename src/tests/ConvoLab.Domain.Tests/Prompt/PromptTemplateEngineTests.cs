using ConvoLab.Domain.Prompt.Enums;
using ConvoLab.Domain.Prompt.Services;
using ConvoLab.Domain.Prompt.ValueObjects;

namespace ConvoLab.Domain.Tests.Prompt;

public sealed class PromptTemplateEngineTests
{
    private static readonly IReadOnlyList<PromptTemplateSection> Sections =
    [
        PromptTemplateSection.Create(PromptSectionType.UserMessage, "User", "Question: {{customerMessage}}", 20),
        PromptTemplateSection.Create(PromptSectionType.System, "System", "Use {{knowledgePackage}} only.", 10)
    ];

    [Fact]
    public void Render_Is_Deterministic_And_Ordered()
    {
        var variables = new Dictionary<string, string>
        {
            ["customerMessage"] = "Can I claim?",
            ["knowledgePackage"] = "Policy wording"
        };

        var first = PromptTemplateEngine.Render(Sections, variables, true);
        var second = PromptTemplateEngine.Render(Sections.Reverse(), variables, true);

        Assert.Equal(first, second);
        Assert.True(first.IndexOf("SYSTEM", StringComparison.Ordinal) < first.IndexOf("USERMESSAGE", StringComparison.Ordinal));
    }

    [Fact]
    public void DiscoverVariables_Removes_Duplicates()
    {
        var variables = PromptTemplateEngine.DiscoverVariables(Sections);
        Assert.Equal(["customerMessage", "knowledgePackage"], variables);
    }

    [Fact]
    public void Required_Missing_Variables_Are_Reported()
    {
        var missing = PromptTemplateEngine.FindMissingRequiredVariables(
            Sections,
            new Dictionary<string, string> { ["customerMessage"] = "Hello" });
        Assert.Equal(["knowledgePackage"], missing);
    }
}
