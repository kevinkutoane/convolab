using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Domain.Evaluation.ValueObjects;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Evaluation.Entities;
using ConvoLab.Domain.Evaluation.Enums;
using ConvoLab.Domain.Evaluation.Aggregates;
namespace ConvoLab.Application.Services;
public class PlaceholderEvaluationEngine : IEvaluationEngine {
    public Task<EvaluationId> StartEvaluationAsync(ConversationId conversationId, CancellationToken cancellationToken = default) => Task.FromResult(new EvaluationId(Guid.NewGuid()));
    public Task AddEvaluationMetricAsync(EvaluationId evaluationId, string name, double value, string unit, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task AddEvaluationResultAsync(EvaluationId evaluationId, string aspect, string result, string? details = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CompleteEvaluationAsync(EvaluationId evaluationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<EvaluationReport> GetEvaluationReportAsync(EvaluationId evaluationId, CancellationToken cancellationToken = default) => Task.FromResult(EvaluationReport.Create(new ConversationId(Guid.NewGuid())));
}
