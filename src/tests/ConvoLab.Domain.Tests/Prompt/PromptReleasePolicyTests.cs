using ConvoLab.Domain.Prompt.Enums;
using ConvoLab.Domain.Prompt.Policies;

namespace ConvoLab.Domain.Tests.Prompt;

public sealed class PromptReleasePolicyTests
{
    [Theory]
    [InlineData(PromptStatus.Draft, PromptReleaseAction.Submit, PromptStatus.InReview)]
    [InlineData(PromptStatus.InReview, PromptReleaseAction.Approve, PromptStatus.Approved)]
    [InlineData(PromptStatus.InReview, PromptReleaseAction.Reject, PromptStatus.Draft)]
    [InlineData(PromptStatus.Approved, PromptReleaseAction.Publish, PromptStatus.Active)]
    [InlineData(PromptStatus.Active, PromptReleaseAction.Deprecate, PromptStatus.Deprecated)]
    [InlineData(PromptStatus.Deprecated, PromptReleaseAction.Archive, PromptStatus.Archived)]
    [InlineData(PromptStatus.Archived, PromptReleaseAction.Restore, PromptStatus.Draft)]
    public void Transition_Allows_Canonical_Lifecycle(
        PromptStatus current,
        PromptReleaseAction action,
        PromptStatus expected)
        => Assert.Equal(expected, PromptReleasePolicy.Transition(current, action));

    [Fact]
    public void Publish_Rejects_Unapproved_Version()
        => Assert.Throws<InvalidOperationException>(() =>
            PromptReleasePolicy.Transition(PromptStatus.Draft, PromptReleaseAction.Publish));

    [Theory]
    [InlineData(PromptStatus.Active)]
    [InlineData(PromptStatus.Deprecated)]
    [InlineData(PromptStatus.Archived)]
    public void Non_Draft_Versions_Are_Immutable(PromptStatus status)
        => Assert.Throws<InvalidOperationException>(() => PromptReleasePolicy.EnsureDraftIsEditable(status));
}
