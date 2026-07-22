namespace ConvoLab.Infrastructure.EvaluationStudio;

public sealed class EvaluationScorecardRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Published";
    public string Version { get; set; } = "1.0";
    public double QualityGateThreshold { get; set; }
    public bool IsDefault { get; set; }
    public long Revision { get; set; } = 1;
    public double MinimumGroundedness { get; set; }
    public double MinimumRelevance { get; set; }
    public double MinimumSafety { get; set; }
    public double MinimumOverallScore { get; set; }
    public string FailureAction { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
