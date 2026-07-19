using ConvoLab.Domain.Knowledge.Enums;

namespace ConvoLab.Domain.Knowledge.Policies;

public static class KnowledgeDocumentStagePolicy
{
    public static KnowledgeDocumentStage Transition(
        KnowledgeDocumentStage current,
        KnowledgeDocumentAction action,
        bool requiresApproval)
    {
        if (action == KnowledgeDocumentAction.Publish && requiresApproval && current != KnowledgeDocumentStage.Approved)
            throw new InvalidOperationException("This document requires approval before publication.");

        return (current, action) switch
        {
            (KnowledgeDocumentStage.Processed, KnowledgeDocumentAction.Submit) => KnowledgeDocumentStage.PendingApproval,
            (KnowledgeDocumentStage.PendingApproval, KnowledgeDocumentAction.Approve) => KnowledgeDocumentStage.Approved,
            (KnowledgeDocumentStage.PendingApproval, KnowledgeDocumentAction.Reject) => KnowledgeDocumentStage.Processed,
            (KnowledgeDocumentStage.Processed, KnowledgeDocumentAction.Publish) when !requiresApproval => KnowledgeDocumentStage.Published,
            (KnowledgeDocumentStage.Approved, KnowledgeDocumentAction.Publish) => KnowledgeDocumentStage.Published,
            (KnowledgeDocumentStage.Published, KnowledgeDocumentAction.Deprecate) => KnowledgeDocumentStage.Deprecated,
            (KnowledgeDocumentStage.Uploaded, KnowledgeDocumentAction.Archive) => KnowledgeDocumentStage.Archived,
            (KnowledgeDocumentStage.Processed, KnowledgeDocumentAction.Archive) => KnowledgeDocumentStage.Archived,
            (KnowledgeDocumentStage.Approved, KnowledgeDocumentAction.Archive) => KnowledgeDocumentStage.Archived,
            (KnowledgeDocumentStage.Deprecated, KnowledgeDocumentAction.Archive) => KnowledgeDocumentStage.Archived,
            (KnowledgeDocumentStage.Archived, KnowledgeDocumentAction.Restore) => KnowledgeDocumentStage.Processed,
            _ => throw new InvalidOperationException(
                $"Cannot {action.ToString().ToLowerInvariant()} a document in {current} state.")
        };
    }

    public static void EnsureMutable(KnowledgeDocumentStage stage)
    {
        if (stage is KnowledgeDocumentStage.Published or KnowledgeDocumentStage.Archived)
            throw new InvalidOperationException(
                $"Document content is immutable in {stage} state. Create a new version to change it.");
    }

    public static bool IsRetrievable(KnowledgeDocumentStage stage)
        => stage == KnowledgeDocumentStage.Published;
}
