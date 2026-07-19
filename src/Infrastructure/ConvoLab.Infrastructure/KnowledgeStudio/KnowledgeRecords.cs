using ConvoLab.Application.KnowledgeStudio;
using ConvoLab.Domain.Knowledge.Enums;

namespace ConvoLab.Infrastructure.KnowledgeStudio;

public sealed class KnowledgeCollectionRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public KnowledgeClassification Classification { get; set; }
    public KnowledgeCollectionStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Revision { get; set; } = 1;
}

public sealed class KnowledgeDocumentRecord
{
    public Guid Id { get; set; }
    public Guid CollectionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public KnowledgeDocumentStage Status { get; set; }
    public KnowledgeClassification Classification { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string TagsJson { get; set; } = "[]";
    public int Version { get; set; } = 1;
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public long Revision { get; set; } = 1;
}

public sealed class KnowledgeChunkRecord
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid CollectionId { get; set; }
    public int Sequence { get; set; }
    public string Text { get; set; } = string.Empty;
    public int? PageNumber { get; set; }
    public string? Section { get; set; }
    public int CharacterCount { get; set; }
    public int EstimatedTokens { get; set; }
    public KnowledgeClassification Classification { get; set; }
    public bool Published { get; set; }
}

public sealed class KnowledgeLifecycleRecord
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public KnowledgeDocumentStage PreviousStatus { get; set; }
    public KnowledgeDocumentStage NewStatus { get; set; }
    public DateTimeOffset At { get; set; }
}
