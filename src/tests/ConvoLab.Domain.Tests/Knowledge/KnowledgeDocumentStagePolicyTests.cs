using ConvoLab.Domain.Knowledge.Enums;
using ConvoLab.Domain.Knowledge.Policies;

namespace ConvoLab.Domain.Tests.Knowledge;

public sealed class KnowledgeDocumentStagePolicyTests
{
    [Fact]
    public void Confidential_Document_Requires_Approval()
        => Assert.Throws<InvalidOperationException>(() =>
            KnowledgeDocumentStagePolicy.Transition(
                KnowledgeDocumentStage.Processed,
                KnowledgeDocumentAction.Publish,
                requiresApproval: true));

    [Fact]
    public void Approved_Confidential_Document_Can_Be_Published()
        => Assert.Equal(
            KnowledgeDocumentStage.Published,
            KnowledgeDocumentStagePolicy.Transition(
                KnowledgeDocumentStage.Approved,
                KnowledgeDocumentAction.Publish,
                requiresApproval: true));

    [Fact]
    public void Published_Document_Is_Immutable()
        => Assert.Throws<InvalidOperationException>(() =>
            KnowledgeDocumentStagePolicy.EnsureMutable(KnowledgeDocumentStage.Published));

    [Theory]
    [InlineData(KnowledgeDocumentStage.Uploaded)]
    [InlineData(KnowledgeDocumentStage.Deprecated)]
    [InlineData(KnowledgeDocumentStage.Archived)]
    public void Only_Published_Documents_Are_Retrievable(KnowledgeDocumentStage stage)
        => Assert.False(KnowledgeDocumentStagePolicy.IsRetrievable(stage));
}
