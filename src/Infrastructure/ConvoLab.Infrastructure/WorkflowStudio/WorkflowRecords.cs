namespace ConvoLab.Infrastructure.WorkflowStudio;

public sealed class WorkflowRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string TagsJson { get; set; } = "[]";
    public bool IsActive { get; set; }
    public long Revision { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class WorkflowVersionRecord
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public int Major { get; set; }
    public int Minor { get; set; }
    public int Patch { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ChangeSummary { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public long Revision { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class WorkflowNodeRecord
{
    public Guid Id { get; set; }
    public Guid WorkflowVersionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public string ConfigurationJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class WorkflowTransitionRecord
{
    public Guid Id { get; set; }
    public Guid WorkflowVersionId { get; set; }
    public Guid FromNodeId { get; set; }
    public Guid ToNodeId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Condition { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class WorkflowAuditRecord
{
    public Guid Id { get; set; }
    public Guid WorkflowVersionId { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
