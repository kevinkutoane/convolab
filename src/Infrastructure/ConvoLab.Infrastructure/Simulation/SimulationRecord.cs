namespace ConvoLab.Infrastructure.Simulation;

public sealed class SimulationRecord
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}
