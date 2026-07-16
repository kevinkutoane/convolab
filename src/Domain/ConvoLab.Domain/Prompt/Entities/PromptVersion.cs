using ConvoLab.Domain.Common;
using ConvoLab.Domain.Prompt.Enums;
using ConvoLab.Domain.Prompt.ValueObjects;

namespace ConvoLab.Domain.Prompt.Entities;

/// <summary>
/// Represents an immutable version of a prompt's content.
/// Every edit to a prompt creates a new PromptVersion. Existing versions are never modified.
/// </summary>
public class PromptVersion : BaseEntity<PromptVersionId>
{
    public PromptId PromptId { get; private set; }
    public string Content { get; private set; }
    public SemanticVersion Version { get; private set; }
    public PromptStatus Status { get; private set; }
    public Guid AuthorId { get; private set; }
    public string AuthorName { get; private set; }
    public string? ChangeReason { get; private set; }
    public PromptApproval? Approval { get; private set; }
    public IReadOnlyList<PromptVariable> Variables { get; private set; }

    private PromptVersion() { PromptId = null!; Content = null!; Version = null!; AuthorName = null!; Variables = null!; }

    private PromptVersion(
        PromptVersionId id,
        PromptId promptId,
        string content,
        SemanticVersion version,
        Guid authorId,
        string authorName,
        string? changeReason,
        IEnumerable<PromptVariable> variables) : base(id)
    {
        PromptId = promptId;
        Content = content;
        Version = version;
        Status = PromptStatus.Draft;
        AuthorId = authorId;
        AuthorName = authorName;
        ChangeReason = changeReason;
        Variables = variables.ToList().AsReadOnly();
    }

    public static PromptVersion Create(
        PromptId promptId,
        string content,
        SemanticVersion version,
        Guid authorId,
        string authorName,
        string? changeReason,
        IEnumerable<PromptVariable>? variables = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Prompt content cannot be empty.", nameof(content));

        return new PromptVersion(
            PromptVersionId.CreateUnique(),
            promptId,
            content,
            version,
            authorId,
            authorName,
            changeReason,
            variables ?? Enumerable.Empty<PromptVariable>());
    }

    internal void SubmitForReview()
    {
        if (Status != PromptStatus.Draft)
            throw new InvalidOperationException($"Cannot submit a prompt version in '{Status}' status for review. Only Draft versions can be submitted.");
        Status = PromptStatus.InReview;
    }

    internal void Approve(PromptApproval approval)
    {
        if (Status != PromptStatus.InReview)
            throw new InvalidOperationException($"Cannot approve a prompt version in '{Status}' status. Only InReview versions can be approved.");
        Approval = approval;
        Status = PromptStatus.Approved;
    }

    internal void Reject(PromptApproval rejection)
    {
        if (Status != PromptStatus.InReview)
            throw new InvalidOperationException($"Cannot reject a prompt version in '{Status}' status. Only InReview versions can be rejected.");
        Approval = rejection;
        Status = PromptStatus.Draft;
    }

    internal void Activate()
    {
        if (Status != PromptStatus.Approved)
            throw new InvalidOperationException($"Cannot activate a prompt version in '{Status}' status. Only Approved versions can be activated.");
        Status = PromptStatus.Active;
    }

    internal void Deprecate()
    {
        if (Status != PromptStatus.Active)
            throw new InvalidOperationException($"Cannot deprecate a prompt version in '{Status}' status. Only Active versions can be deprecated.");
        Status = PromptStatus.Deprecated;
    }

    internal void Archive()
    {
        if (Status == PromptStatus.Active)
            throw new InvalidOperationException("Cannot archive an Active prompt version. Deprecate it first.");
        Status = PromptStatus.Archived;
    }
}
