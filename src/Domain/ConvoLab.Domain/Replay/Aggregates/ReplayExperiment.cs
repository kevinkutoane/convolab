namespace ConvoLab.Domain.Replay.Aggregates;

public enum ReplayExperimentStatus
{
    Active,
    Completed,
    Archived
}

public sealed class ReplayExperiment
{
    private readonly List<Guid> _candidateRunIds = [];

    private ReplayExperiment(Guid id, Guid simulationId, Guid sourceRunId, string name)
    {
        Id = id;
        SimulationId = simulationId;
        SourceRunId = sourceRunId;
        Name = name;
        Status = ReplayExperimentStatus.Active;
    }

    public Guid Id { get; }
    public Guid SimulationId { get; }
    public Guid SourceRunId { get; }
    public string Name { get; }
    public ReplayExperimentStatus Status { get; private set; }
    public IReadOnlyList<Guid> CandidateRunIds => _candidateRunIds;

    public static ReplayExperiment Create(Guid simulationId, Guid sourceRunId, string name)
    {
        if (simulationId == Guid.Empty) throw new ArgumentException("Simulation is required.", nameof(simulationId));
        if (sourceRunId == Guid.Empty) throw new ArgumentException("Source run is required.", nameof(sourceRunId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Experiment name is required.", nameof(name));
        return new ReplayExperiment(Guid.NewGuid(), simulationId, sourceRunId, name.Trim());
    }

    public void AddCandidate(Guid runId)
    {
        EnsureActive();
        if (runId == Guid.Empty) throw new ArgumentException("Candidate run is required.", nameof(runId));
        if (_candidateRunIds.Contains(runId)) return;
        _candidateRunIds.Add(runId);
    }

    public void Complete()
    {
        EnsureActive();
        if (_candidateRunIds.Count == 0) throw new InvalidOperationException("A replay experiment requires at least one candidate before completion.");
        Status = ReplayExperimentStatus.Completed;
    }

    public void Archive() => Status = ReplayExperimentStatus.Archived;

    private void EnsureActive()
    {
        if (Status != ReplayExperimentStatus.Active)
            throw new InvalidOperationException("Only active replay experiments can be modified.");
    }
}
