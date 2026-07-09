using ConvoLab.Domain.Common;
using ConvoLab.Domain.Evaluation.ValueObjects;
using ConvoLab.Domain.Evaluation.Entities;
using ConvoLab.Domain.Evaluation.Enums;
using ConvoLab.Domain.Evaluation.Events;
using ConvoLab.Domain.Conversation.ValueObjects;
namespace ConvoLab.Domain.Evaluation.Aggregates;
public class EvaluationReport : BaseAggregateRoot<EvaluationId> {
    public ConversationId ConversationId { get; private set; }
    public EvaluationStatus Status { get; private set; }
    public DateTime EvaluatedAt { get; private set; }
    private readonly List<EvaluationMetric> _metrics = new();
    public IReadOnlyCollection<EvaluationMetric> Metrics => _metrics.AsReadOnly();
    private readonly List<EvaluationResult> _results = new();
    public IReadOnlyCollection<EvaluationResult> Results => _results.AsReadOnly();
    private EvaluationReport() : base() { }
    private EvaluationReport(EvaluationId id, ConversationId conversationId) : base(id) {
        ConversationId = conversationId; Status = EvaluationStatus.Pending; EvaluatedAt = DateTime.UtcNow;
        AddDomainEvent(new EvaluationStartedEvent(id, conversationId));
    }
    public static EvaluationReport Create(ConversationId conversationId) => new EvaluationReport(new EvaluationId(Guid.NewGuid()), conversationId);
}
