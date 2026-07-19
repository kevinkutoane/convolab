namespace ConvoLab.Infrastructure.EvaluationStudio;

public sealed class EvaluationScorecardRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double MinimumGroundedness { get; set; }
    public double MinimumRelevance { get; set; }
    public double MinimumSafety { get; set; }
    public double MinimumOverallScore { get; set; }
    public string FailureAction { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
