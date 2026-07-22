namespace ConvoLab.Infrastructure.EvaluationStudio;

public sealed class EvaluationMetricDefinitionRecord
{
    public Guid Id { get; set; }
    public Guid ScorecardId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Weight { get; set; }
    public double Threshold { get; set; }
    public bool Required { get; set; }
}

public sealed class EvaluationRunRecord
{
    public Guid Id { get; set; }
    public Guid SimulationId { get; set; }
    public string SimulationTitle { get; set; } = string.Empty;
    public Guid SourceRunId { get; set; }
    public Guid ScorecardId { get; set; }
    public string ScorecardName { get; set; } = string.Empty;
    public string ScorecardVersion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Verdict { get; set; } = string.Empty;
    public double OverallScore { get; set; }
    public string? FailureReason { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public string? ReviewNotes { get; set; }
    public string? Reviewer { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class EvaluationMetricResultRecord
{
    public Guid Id { get; set; }
    public Guid EvaluationRunId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public double Score { get; set; }
    public double Threshold { get; set; }
    public double Weight { get; set; }
    public bool Passed { get; set; }
    public string Detail { get; set; } = string.Empty;
}

public sealed class EvaluationTestCaseRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid SimulationId { get; set; }
    public Guid SourceRunId { get; set; }
    public Guid? ScorecardId { get; set; }
    public string ExpectedVerdict { get; set; } = string.Empty;
    public string TagsJson { get; set; } = "[]";
    public string Status { get; set; } = string.Empty;
    public long Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class EvaluationBatchRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid ScorecardId { get; set; }
    public string ScorecardName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class EvaluationBatchItemRecord
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public Guid TestCaseId { get; set; }
    public string TestCaseName { get; set; } = string.Empty;
    public Guid? EvaluationRunId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ActualVerdict { get; set; } = string.Empty;
    public string ExpectedVerdict { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Detail { get; set; } = string.Empty;
}
