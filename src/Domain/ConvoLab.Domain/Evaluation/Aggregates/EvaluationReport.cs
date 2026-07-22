using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Evaluation.Entities;
using ConvoLab.Domain.Evaluation.Enums;
using ConvoLab.Domain.Evaluation.Events;
using ConvoLab.Domain.Evaluation.ValueObjects;

namespace ConvoLab.Domain.Evaluation.Aggregates;

public class EvaluationReport : BaseAggregateRoot<EvaluationId>
{
    public ConversationId ConversationId { get; private set; } = default!;
    public EvaluationStatus Status { get; private set; }
    public DateTime EvaluatedAt { get; private set; }
    private readonly List<EvaluationMetric> _metrics = [];
    public IReadOnlyCollection<EvaluationMetric> Metrics => _metrics.AsReadOnly();
    private readonly List<EvaluationResult> _results = [];
    public IReadOnlyCollection<EvaluationResult> Results => _results.AsReadOnly();

    private EvaluationReport() : base() { }

    private EvaluationReport(EvaluationId id, ConversationId conversationId) : base(id)
    {
        ConversationId = conversationId;
        Status = EvaluationStatus.Pending;
        EvaluatedAt = DateTime.UtcNow;
        AddDomainEvent(new EvaluationStartedEvent(id, conversationId));
    }

    public static EvaluationReport Create(ConversationId conversationId)
        => new(EvaluationId.CreateUnique(), conversationId);

    public void Begin()
    {
        if (Status != EvaluationStatus.Pending)
            throw new InvalidOperationException("Only a pending evaluation can be started.");
        Status = EvaluationStatus.InProgress;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void AddMetric(string name, double value, string unit)
    {
        EnsureInProgress();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Metric name is required.", nameof(name));
        _metrics.Add(EvaluationMetric.Create(name.Trim(), value, string.IsNullOrWhiteSpace(unit) ? "score" : unit.Trim()));
        LastModifiedAt = DateTime.UtcNow;
    }

    public void AddResult(string aspect, string result, string? details = null)
    {
        EnsureInProgress();
        if (string.IsNullOrWhiteSpace(aspect)) throw new ArgumentException("Evaluation aspect is required.", nameof(aspect));
        if (string.IsNullOrWhiteSpace(result)) throw new ArgumentException("Evaluation result is required.", nameof(result));
        _results.Add(EvaluationResult.Create(aspect.Trim(), result.Trim(), details?.Trim()));
        LastModifiedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        EnsureInProgress();
        Status = EvaluationStatus.Completed;
        EvaluatedAt = DateTime.UtcNow;
        LastModifiedAt = EvaluatedAt;
        AddDomainEvent(new EvaluationCompletedEvent(Id, ConversationId));
    }

    public void Fail(string? details = null)
    {
        if (Status is EvaluationStatus.Completed or EvaluationStatus.Failed)
            throw new InvalidOperationException("A terminal evaluation cannot be failed again.");
        Status = EvaluationStatus.Failed;
        EvaluatedAt = DateTime.UtcNow;
        LastModifiedAt = EvaluatedAt;
        if (!string.IsNullOrWhiteSpace(details))
            _results.Add(EvaluationResult.Create("execution", "Failed", details.Trim()));
    }

    private void EnsureInProgress()
    {
        if (Status != EvaluationStatus.InProgress)
            throw new InvalidOperationException("Evaluation metrics and results can only be changed while the evaluation is in progress.");
    }
}
