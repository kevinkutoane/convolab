using ConvoLab.Domain.Common;
using ConvoLab.Domain.Prompt.Enums;

namespace ConvoLab.Domain.Prompt.ValueObjects;

/// <summary>
/// Records the approval or rejection decision for a prompt version.
/// Approvals are immutable records of governance decisions.
/// </summary>
public class PromptApproval : ValueObject
{
    public Guid ReviewerId { get; private set; }
    public string ReviewerName { get; private set; }
    public ApprovalStatus Status { get; private set; }
    public string? Reason { get; private set; }
    public DateTime ReviewedAt { get; private set; }

    private PromptApproval(Guid reviewerId, string reviewerName, ApprovalStatus status, string? reason, DateTime reviewedAt)
    {
        ReviewerId = reviewerId;
        ReviewerName = reviewerName;
        Status = status;
        Reason = reason;
        ReviewedAt = reviewedAt;
    }

    public static PromptApproval Approve(Guid reviewerId, string reviewerName, string? reason = null)
        => new(reviewerId, reviewerName, ApprovalStatus.Approved, reason, DateTime.UtcNow);

    public static PromptApproval Reject(Guid reviewerId, string reviewerName, string reason)
        => new(reviewerId, reviewerName, ApprovalStatus.Rejected, reason, DateTime.UtcNow);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return ReviewerId;
        yield return ReviewedAt;
    }

    private PromptApproval() { ReviewerName = null!; }
}
