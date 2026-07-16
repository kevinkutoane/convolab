using ConvoLab.Domain.Prompt.Aggregates;
using ConvoLab.Domain.Prompt.Enums;
using ConvoLab.Domain.Prompt.Events;
using ConvoLab.Domain.Prompt.ValueObjects;
using Xunit;

namespace ConvoLab.Domain.Tests.Prompt;

public class PromptAggregateTests
{
    private readonly Guid _authorId = Guid.NewGuid();
    private const string AuthorName = "Kevin Kutoane";
    private readonly PromptOwner _owner = PromptOwner.Create(Guid.NewGuid(), "Platform Team", "AI Platform");
    private readonly PromptMetadata _metadata = PromptMetadata.Create("A test prompt", "Testing", new[] { "test" });

    private Domain.Prompt.Aggregates.Prompt CreateDraftPrompt(string name = "Test Prompt", string content = "Hello {{CustomerName}}")
        => Domain.Prompt.Aggregates.Prompt.Create(name, content, _owner, _metadata, _authorId, AuthorName);

    // =========================================================
    // Creation Tests
    // =========================================================

    [Fact]
    public void Create_Should_Initialize_With_Draft_Status()
    {
        var prompt = CreateDraftPrompt();

        Assert.Equal(PromptStatus.Draft, prompt.Status);
        Assert.Single(prompt.Versions);
        Assert.Equal("1.0.0", prompt.Versions.First().Version.ToString());
    }

    [Fact]
    public void Create_Should_Raise_PromptCreatedEvent()
    {
        var prompt = CreateDraftPrompt();

        Assert.Contains(prompt.DomainEvents, e => e is PromptCreatedEvent);
    }

    [Fact]
    public void Create_With_Empty_Name_Should_Throw()
    {
        Assert.Throws<ArgumentException>(() => CreateDraftPrompt(name: ""));
    }

    [Fact]
    public void Create_With_Empty_Content_Should_Throw()
    {
        Assert.Throws<ArgumentException>(() => CreateDraftPrompt(content: ""));
    }

    // =========================================================
    // Versioning Tests
    // =========================================================

    [Fact]
    public void CreateNewVersion_Should_Increment_Minor_Version()
    {
        var prompt = CreateDraftPrompt();
        var newVersion = prompt.CreateNewVersion("Hello {{CustomerName}} v2", _authorId, AuthorName, "Updated greeting");

        Assert.Equal("1.1.0", newVersion.Version.ToString());
        Assert.Equal(2, prompt.Versions.Count);
    }

    [Fact]
    public void CreateNewVersion_Should_Raise_PromptVersionCreatedEvent()
    {
        var prompt = CreateDraftPrompt();
        prompt.ClearDomainEvents();

        prompt.CreateNewVersion("New content", _authorId, AuthorName, "Test change");

        Assert.Contains(prompt.DomainEvents, e => e is PromptVersionCreatedEvent);
    }

    // =========================================================
    // Approval Workflow Tests
    // =========================================================

    [Fact]
    public void Full_Approval_Workflow_Should_Succeed()
    {
        var prompt = CreateDraftPrompt();
        var reviewerId = Guid.NewGuid();

        prompt.SubmitForApproval();
        Assert.Equal(PromptStatus.InReview, prompt.Status);

        prompt.Approve(reviewerId, "Reviewer One");
        Assert.Equal(PromptStatus.Approved, prompt.Status);

        prompt.Activate();
        Assert.Equal(PromptStatus.Active, prompt.Status);
        Assert.NotNull(prompt.ActiveVersionId);
    }

    [Fact]
    public void Approve_Should_Raise_PromptApprovedEvent()
    {
        var prompt = CreateDraftPrompt();
        prompt.SubmitForApproval();
        prompt.ClearDomainEvents();

        prompt.Approve(Guid.NewGuid(), "Reviewer");

        Assert.Contains(prompt.DomainEvents, e => e is PromptApprovedEvent);
    }

    [Fact]
    public void Reject_Should_Return_Prompt_To_Draft()
    {
        var prompt = CreateDraftPrompt();
        prompt.SubmitForApproval();

        prompt.Reject(Guid.NewGuid(), "Reviewer", "Needs more context");

        Assert.Equal(PromptStatus.Draft, prompt.Status);
    }

    [Fact]
    public void Reject_Should_Raise_PromptRejectedEvent()
    {
        var prompt = CreateDraftPrompt();
        prompt.SubmitForApproval();
        prompt.ClearDomainEvents();

        prompt.Reject(Guid.NewGuid(), "Reviewer", "Needs work");

        Assert.Contains(prompt.DomainEvents, e => e is PromptRejectedEvent);
    }

    [Fact]
    public void Reject_Without_Reason_Should_Throw()
    {
        var prompt = CreateDraftPrompt();
        prompt.SubmitForApproval();

        Assert.Throws<ArgumentException>(() => prompt.Reject(Guid.NewGuid(), "Reviewer", ""));
    }

    [Fact]
    public void SubmitForApproval_When_Not_Draft_Should_Throw()
    {
        var prompt = CreateDraftPrompt();
        prompt.SubmitForApproval();

        Assert.Throws<InvalidOperationException>(() => prompt.SubmitForApproval());
    }

    // =========================================================
    // Rendering Tests
    // =========================================================

    [Fact]
    public void Render_Should_Inject_Variables_Into_Active_Version()
    {
        var prompt = CreateDraftPrompt(content: "Hello {{CustomerName}}, your claim {{ClaimNumber}} is ready.");
        prompt.SubmitForApproval();
        prompt.Approve(Guid.NewGuid(), "Reviewer");
        prompt.Activate();

        var variables = new Dictionary<string, string>
        {
            { "CustomerName", "John Doe" },
            { "ClaimNumber", "CLM-12345" }
        };

        var rendered = prompt.Render(variables);

        Assert.Equal("Hello John Doe, your claim CLM-12345 is ready.", rendered);
    }

    [Fact]
    public void Render_Should_Raise_PromptRenderedEvent()
    {
        var prompt = CreateDraftPrompt();
        prompt.SubmitForApproval();
        prompt.Approve(Guid.NewGuid(), "Reviewer");
        prompt.Activate();
        prompt.ClearDomainEvents();

        prompt.Render(new Dictionary<string, string>());

        Assert.Contains(prompt.DomainEvents, e => e is PromptRenderedEvent);
    }

    [Fact]
    public void Render_Without_Active_Version_Should_Throw()
    {
        var prompt = CreateDraftPrompt();

        Assert.Throws<InvalidOperationException>(() => prompt.Render(new Dictionary<string, string>()));
    }

    [Fact]
    public void Render_Archived_Prompt_Should_Throw()
    {
        var prompt = CreateDraftPrompt();
        prompt.SubmitForApproval();
        prompt.Approve(Guid.NewGuid(), "Reviewer");
        prompt.Activate();
        prompt.Deprecate();
        prompt.Archive();

        Assert.Throws<InvalidOperationException>(() => prompt.Render(new Dictionary<string, string>()));
    }

    // =========================================================
    // Lifecycle Tests
    // =========================================================

    [Fact]
    public void Deprecate_Active_Prompt_Should_Succeed()
    {
        var prompt = CreateDraftPrompt();
        prompt.SubmitForApproval();
        prompt.Approve(Guid.NewGuid(), "Reviewer");
        prompt.Activate();

        prompt.Deprecate();

        Assert.Equal(PromptStatus.Deprecated, prompt.Status);
    }

    [Fact]
    public void Deprecate_Non_Active_Prompt_Should_Throw()
    {
        var prompt = CreateDraftPrompt();

        Assert.Throws<InvalidOperationException>(() => prompt.Deprecate());
    }

    [Fact]
    public void Archive_Active_Prompt_Should_Throw()
    {
        var prompt = CreateDraftPrompt();
        prompt.SubmitForApproval();
        prompt.Approve(Guid.NewGuid(), "Reviewer");
        prompt.Activate();

        Assert.Throws<InvalidOperationException>(() => prompt.Archive());
    }

    [Fact]
    public void Archive_Deprecated_Prompt_Should_Succeed()
    {
        var prompt = CreateDraftPrompt();
        prompt.SubmitForApproval();
        prompt.Approve(Guid.NewGuid(), "Reviewer");
        prompt.Activate();
        prompt.Deprecate();

        prompt.Archive();

        Assert.Equal(PromptStatus.Archived, prompt.Status);
        Assert.Null(prompt.ActiveVersionId);
    }

    [Fact]
    public void Restore_Archived_Prompt_Should_Return_To_Draft()
    {
        var prompt = CreateDraftPrompt();
        prompt.SubmitForApproval();
        prompt.Approve(Guid.NewGuid(), "Reviewer");
        prompt.Activate();
        prompt.Deprecate();
        prompt.Archive();

        prompt.Restore();

        Assert.Equal(PromptStatus.Draft, prompt.Status);
    }

    // =========================================================
    // Rollback Tests
    // =========================================================

    [Fact]
    public void Rollback_Should_Create_New_Version_With_Previous_Content()
    {
        var prompt = CreateDraftPrompt(content: "Original content");
        var originalVersionId = prompt.Versions.First().Id;

        prompt.CreateNewVersion("New content", _authorId, AuthorName, "Updated");

        var rolledBack = prompt.Rollback(originalVersionId, _authorId, AuthorName);

        Assert.Equal("Original content", rolledBack.Content);
        Assert.Equal(3, prompt.Versions.Count);
    }

    [Fact]
    public void Rollback_To_NonExistent_Version_Should_Throw()
    {
        var prompt = CreateDraftPrompt();

        Assert.Throws<InvalidOperationException>(() =>
            prompt.Rollback(PromptVersionId.CreateUnique(), _authorId, AuthorName));
    }

    // =========================================================
    // Composition Tests
    // =========================================================

    [Fact]
    public void AddSection_And_RenderComposed_Should_Assemble_In_Order()
    {
        var prompt = CreateDraftPrompt();
        prompt.AddSection(PromptSectionType.System, "You are a helpful assistant.", 1);
        prompt.AddSection(PromptSectionType.Role, "You specialize in insurance claims.", 2);
        prompt.AddSection(PromptSectionType.UserMessage, "Hello {{CustomerName}}", 3);

        var variables = new Dictionary<string, string> { { "CustomerName", "Jane" } };
        var rendered = prompt.RenderComposed(variables);

        Assert.Contains("You are a helpful assistant.", rendered);
        Assert.Contains("You specialize in insurance claims.", rendered);
        Assert.Contains("Hello Jane", rendered);
    }

    [Fact]
    public void RenderComposed_With_No_Sections_Should_Throw()
    {
        var prompt = CreateDraftPrompt();

        Assert.Throws<InvalidOperationException>(() =>
            prompt.RenderComposed(new Dictionary<string, string>()));
    }

    // =========================================================
    // Variant Tests
    // =========================================================

    [Fact]
    public void AddVariant_Exceeding_100_Weight_Should_Throw()
    {
        var prompt = CreateDraftPrompt();
        var versionId = prompt.Versions.First().Id;

        prompt.AddVariant("Control", versionId, 60);

        Assert.Throws<InvalidOperationException>(() =>
            prompt.AddVariant("Treatment", versionId, 50));
    }

    [Fact]
    public void AddVariant_Within_Weight_Should_Succeed()
    {
        var prompt = CreateDraftPrompt();
        var versionId = prompt.Versions.First().Id;

        prompt.AddVariant("Control", versionId, 50);
        prompt.AddVariant("Treatment", versionId, 50);

        Assert.Equal(2, prompt.Variants.Count);
    }
}
