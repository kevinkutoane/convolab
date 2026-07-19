namespace ConvoLab.Infrastructure.PromptStudio;

public sealed class PromptRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string TagsJson { get; set; } = "[]";
    public string Status { get; set; } = "Draft";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Revision { get; set; } = 1;
}

public sealed class PromptVersionRecord
{
    public Guid Id { get; set; }
    public Guid PromptId { get; set; }
    public string Version { get; set; } = "1.0.0";
    public string Status { get; set; } = "Draft";
    public string ChangeSummary { get; set; } = string.Empty;
    public string SectionsJson { get; set; } = "[]";
    public string VariablesJson { get; set; } = "[]";
    public int EstimatedTokens { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public long Revision { get; set; } = 1;
}

public sealed class PromptLifecycleRecord
{
    public Guid Id { get; set; }
    public Guid PromptVersionId { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
