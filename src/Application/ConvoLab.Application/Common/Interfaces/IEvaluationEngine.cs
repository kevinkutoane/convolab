using ConvoLab.Domain.Evaluation.ValueObjects;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Evaluation.Entities;
using ConvoLab.Domain.Evaluation.Aggregates;
namespace ConvoLab.Application.Common.Interfaces;
public interface IEvaluationEngine {
    Task<EvaluationId> StartEvaluationAsync(ConversationId conversationId, CancellationToken cancellationToken = default);
    Task AddEvaluationMetricAsync(EvaluationId evaluationId, string name, double value, string unit, CancellationToken cancellationToken = default);
    Task AddEvaluationResultAsync(EvaluationId evaluationId, string aspect, string result, string? details = null, CancellationToken cancellationToken = default);
    Task CompleteEvaluationAsync(EvaluationId evaluationId, CancellationToken cancellationToken = default);
    Task<EvaluationReport> GetEvaluationReportAsync(EvaluationId evaluationId, CancellationToken cancellationToken = default);
}
