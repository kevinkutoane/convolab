namespace ConvoLab.Domain.Knowledge.Enums;

/// <summary>Canonical ingestion and governance stage for a Knowledge Studio document.</summary>
public enum KnowledgeDocumentStage
{
    Uploaded = 0,
    Queued = 1,
    Extracting = 2,
    Chunking = 3,
    Processed = 4,
    PendingApproval = 5,
    Approved = 6,
    Published = 7,
    Failed = 8,
    Deprecated = 9,
    Archived = 10
}

public enum KnowledgeDocumentAction
{
    Submit,
    Approve,
    Reject,
    Publish,
    Deprecate,
    Archive,
    Restore
}
