namespace ConvoLab.Infrastructure.ReplayStudio;

public sealed class ReplayExperimentRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid SimulationId { get; set; }
    public Guid SourceRunId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ReplayCandidateRecord
{
    public Guid Id { get; set; }
    public Guid ExperimentId { get; set; }
    public Guid RunId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Workflow { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string KnowledgeCollection { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public int MaxOutputTokens { get; set; }
    public string Mode { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
