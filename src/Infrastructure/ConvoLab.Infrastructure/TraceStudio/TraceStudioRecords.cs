namespace ConvoLab.Infrastructure.TraceStudio;

public sealed class TraceRecord
{
    public Guid Id { get; set; }
    public Guid CorrelationId { get; set; }
    public string OperationName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? SimulationId { get; set; }
    public string? SimulationTitle { get; set; }
    public Guid? SourceRunId { get; set; }
    public Guid? ReplayedFromRunId { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? Workflow { get; set; }
    public string? PromptVersion { get; set; }
    public string? KnowledgeCollection { get; set; }
    public string? EvaluationVerdict { get; set; }
    public double DurationMs { get; set; }
    public int TotalTokens { get; set; }
    public decimal ActualCost { get; set; }
    public string Currency { get; set; } = "ZAR";
    public string? FailureReason { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class TraceSpanRecord
{
    public Guid Id { get; set; }
    public Guid TraceId { get; set; }
    public Guid? ParentSpanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Capability { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public double DurationMs { get; set; }
    public string AttributesJson { get; set; } = "{}";
}

public sealed class TraceEventRecord
{
    public Guid Id { get; set; }
    public Guid TraceId { get; set; }
    public Guid? SpanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string AttributesJson { get; set; } = "{}";
}

public sealed class TraceArtifactRecord
{
    public Guid Id { get; set; }
    public Guid TraceId { get; set; }
    public Guid? SpanId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsSensitive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
