using ConvoLab.Domain.Prompt.Enums;

namespace ConvoLab.Domain.Prompt.Policies;

/// <summary>
/// Canonical lifecycle rules for prompt versions consumed by Prompt Studio and runtime execution.
/// Infrastructure persists the result but never decides which transition is legal.
/// </summary>
public static class PromptReleasePolicy
{
    public static PromptStatus Transition(PromptStatus current, PromptReleaseAction action)
        => (current, action) switch
        {
            (PromptStatus.Draft, PromptReleaseAction.Submit) => PromptStatus.InReview,
            (PromptStatus.InReview, PromptReleaseAction.Approve) => PromptStatus.Approved,
            (PromptStatus.InReview, PromptReleaseAction.Reject) => PromptStatus.Draft,
            (PromptStatus.Approved, PromptReleaseAction.Publish) => PromptStatus.Active,
            (PromptStatus.Active, PromptReleaseAction.Deprecate) => PromptStatus.Deprecated,
            (PromptStatus.Draft, PromptReleaseAction.Archive) => PromptStatus.Archived,
            (PromptStatus.Approved, PromptReleaseAction.Archive) => PromptStatus.Archived,
            (PromptStatus.Deprecated, PromptReleaseAction.Archive) => PromptStatus.Archived,
            (PromptStatus.Archived, PromptReleaseAction.Restore) => PromptStatus.Draft,
            _ => throw new InvalidOperationException(
                $"Cannot {action.ToString().ToLowerInvariant()} a prompt version in {current} state.")
        };

    public static void EnsureDraftIsEditable(PromptStatus status)
    {
        if (status != PromptStatus.Draft)
            throw new InvalidOperationException(
                $"Prompt content is immutable in {status} state. Create a new version to make changes.");
    }

    public static bool IsExecutable(PromptStatus status)
        => status is PromptStatus.Active or PromptStatus.Deprecated;
}
