using ConvoLab.Domain.Prompt.Aggregates;
using ConvoLab.Domain.Prompt.Entities;
using ConvoLab.Domain.Prompt.Enums;
using ConvoLab.Domain.Prompt.Policies;
using ConvoLab.Domain.Prompt.ValueObjects;
using Xunit;

namespace ConvoLab.Domain.Tests.Prompt;

public class PromptGovernancePolicyTests
{
    private readonly Guid _authorId = Guid.NewGuid();
    private readonly PromptOwner _owner = PromptOwner.Create(Guid.NewGuid(), "Platform Team");

    private Domain.Prompt.Aggregates.Prompt CreateActivePrompt(string content = "Hello {{Name}}", string? environment = null)
    {
        var metadata = PromptMetadata.Create("Test", "Testing", null, environment);
        var prompt = Domain.Prompt.Aggregates.Prompt.Create("Test", content, _owner, metadata, _authorId, "Author");
        prompt.SubmitForApproval();
        prompt.Approve(Guid.NewGuid(), "Reviewer");
        prompt.Activate();
        return prompt;
    }

    [Fact]
    public void CanRender_Active_Prompt_Should_Return_True()
    {
        var prompt = CreateActivePrompt();

        Assert.True(PromptGovernancePolicy.CanRender(prompt));
    }

    [Fact]
    public void CanRender_Draft_Prompt_Should_Return_False()
    {
        var metadata = PromptMetadata.Create("Test", "Testing");
        var prompt = Domain.Prompt.Aggregates.Prompt.Create("Test", "Content", _owner, metadata, _authorId, "Author");

        Assert.False(PromptGovernancePolicy.CanRender(prompt));
    }

    [Fact]
    public void RequiresApproval_Production_Prompt_Should_Return_True()
    {
        var metadata = PromptMetadata.Create("Test", "Testing", null, "Production");
        var prompt = Domain.Prompt.Aggregates.Prompt.Create("Test", "Content", _owner, metadata, _authorId, "Author");

        Assert.True(PromptGovernancePolicy.RequiresApproval(prompt));
    }

    [Fact]
    public void RequiresApproval_Non_Production_Without_Policy_Should_Return_False()
    {
        var metadata = PromptMetadata.Create("Test", "Testing", null, "Development");
        var prompt = Domain.Prompt.Aggregates.Prompt.Create("Test", "Content", _owner, metadata, _authorId, "Author");

        Assert.False(PromptGovernancePolicy.RequiresApproval(prompt));
    }

    [Fact]
    public void ValidateVariables_With_All_Required_Variables_Should_Return_Empty()
    {
        var variables = new[] { PromptVariable.Create("Name", true, "Customer name") };
        var metadata = PromptMetadata.Create("Test", "Testing");
        var prompt = Domain.Prompt.Aggregates.Prompt.Create("Test", "Hello {{Name}}", _owner, metadata, _authorId, "Author");
        prompt.CreateNewVersion("Hello {{Name}}", _authorId, "Author", "With variable", variables);
        prompt.SubmitForApproval();
        prompt.Approve(Guid.NewGuid(), "Reviewer");
        prompt.Activate();

        var provided = new Dictionary<string, string> { { "Name", "John" } };
        var missing = PromptGovernancePolicy.ValidateVariables(prompt, provided);

        Assert.Empty(missing);
    }

    [Fact]
    public void ValidateVariables_With_Missing_Required_Variable_Should_Return_Key()
    {
        var variables = new[] { PromptVariable.Create("Name", true, "Customer name") };
        var metadata = PromptMetadata.Create("Test", "Testing");
        var prompt = Domain.Prompt.Aggregates.Prompt.Create("Test", "Hello {{Name}}", _owner, metadata, _authorId, "Author");
        prompt.CreateNewVersion("Hello {{Name}}", _authorId, "Author", "With variable", variables);
        prompt.SubmitForApproval();
        prompt.Approve(Guid.NewGuid(), "Reviewer");
        prompt.Activate();

        var provided = new Dictionary<string, string>();
        var missing = PromptGovernancePolicy.ValidateVariables(prompt, provided);

        Assert.Contains("Name", missing);
    }

    [Fact]
    public void HasValidVariantWeights_With_Weights_Summing_To_100_Should_Return_True()
    {
        var prompt = CreateActivePrompt();
        var versionId = prompt.ActiveVersionId!;
        prompt.AddVariant("Control", versionId, 50);
        prompt.AddVariant("Treatment", versionId, 50);

        Assert.True(PromptGovernancePolicy.HasValidVariantWeights(prompt));
    }

    [Fact]
    public void HasValidVariantWeights_With_No_Variants_Should_Return_True()
    {
        var prompt = CreateActivePrompt();

        Assert.True(PromptGovernancePolicy.HasValidVariantWeights(prompt));
    }
}
