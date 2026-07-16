using ConvoLab.Domain.Common;
using ConvoLab.Domain.Prompt.Entities;
using ConvoLab.Domain.Prompt.Enums;
using ConvoLab.Domain.Prompt.Events;
using ConvoLab.Domain.Prompt.ValueObjects;

namespace ConvoLab.Domain.Prompt.Aggregates;

/// <summary>
/// The PromptExperiment aggregate. Represents an A/B test or comparison experiment
/// across multiple prompt versions, models, or knowledge strategies.
/// Experiments are reusable assets and produce evaluation references.
/// </summary>
public class PromptExperiment : BaseAggregateRoot<PromptExperimentId>
{
    public string Name { get; private set; }
    public string Description { get; private set; }
    public ExperimentStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private readonly List<PromptVariant> _variants = new();
    private readonly List<string> _evaluationReferences = new();

    public IReadOnlyCollection<PromptVariant> Variants => _variants.AsReadOnly();
    public IReadOnlyCollection<string> EvaluationReferences => _evaluationReferences.AsReadOnly();

    private PromptExperiment() { Name = null!; Description = null!; }

    private PromptExperiment(
        PromptExperimentId id,
        string name,
        string description,
        Guid createdByUserId) : base(id)
    {
        Name = name;
        Description = description;
        Status = ExperimentStatus.Pending;
        CreatedByUserId = createdByUserId;
    }

    public static PromptExperiment Create(string name, string description, Guid createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Experiment name cannot be empty.", nameof(name));

        return new PromptExperiment(PromptExperimentId.CreateUnique(), name, description, createdByUserId);
    }

    public void AddVariant(PromptVariant variant)
    {
        if (Status != ExperimentStatus.Pending)
            throw new InvalidOperationException("Variants can only be added to a Pending experiment.");

        _variants.Add(variant);
    }

    public void Start()
    {
        if (Status != ExperimentStatus.Pending)
            throw new InvalidOperationException("Only a Pending experiment can be started.");
        if (!_variants.Any())
            throw new InvalidOperationException("An experiment must have at least one variant before it can be started.");

        Status = ExperimentStatus.Running;
        StartedAt = DateTime.UtcNow;

        AddDomainEvent(new ExperimentStartedEvent(Id, Name, CreatedByUserId));
    }

    public void Complete(IEnumerable<string>? evaluationReferences = null)
    {
        if (Status != ExperimentStatus.Running)
            throw new InvalidOperationException("Only a Running experiment can be completed.");

        if (evaluationReferences != null)
        {
            _evaluationReferences.AddRange(evaluationReferences);
        }

        Status = ExperimentStatus.Completed;
        CompletedAt = DateTime.UtcNow;

        AddDomainEvent(new ExperimentCompletedEvent(Id, Name));
    }

    public void Cancel()
    {
        if (Status == ExperimentStatus.Completed)
            throw new InvalidOperationException("A completed experiment cannot be cancelled.");

        Status = ExperimentStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
    }
}
