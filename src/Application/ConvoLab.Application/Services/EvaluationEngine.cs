using System.Collections.Concurrent;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Evaluation.Aggregates;
using ConvoLab.Domain.Evaluation.ValueObjects;

namespace ConvoLab.Application.Services;

public sealed class EvaluationEngine : IEvaluationEngine
{
    private readonly ConcurrentDictionary<EvaluationId, EvaluationReport> _reports = new();

    public Task<EvaluationId> StartEvaluationAsync(ConversationId conversationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var report = EvaluationReport.Create(conversationId);
        report.Begin();
        _reports[report.Id] = report;
        return Task.FromResult(report.Id);
    }

    public Task AddEvaluationMetricAsync(EvaluationId evaluationId, string name, double value, string unit, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (GetReport(evaluationId)) GetReport(evaluationId).AddMetric(name, value, unit);
        return Task.CompletedTask;
    }

    public Task AddEvaluationResultAsync(EvaluationId evaluationId, string aspect, string result, string? details = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (GetReport(evaluationId)) GetReport(evaluationId).AddResult(aspect, result, details);
        return Task.CompletedTask;
    }

    public Task CompleteEvaluationAsync(EvaluationId evaluationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (GetReport(evaluationId)) GetReport(evaluationId).Complete();
        return Task.CompletedTask;
    }

    public Task<EvaluationReport> GetEvaluationReportAsync(EvaluationId evaluationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetReport(evaluationId));
    }

    private EvaluationReport GetReport(EvaluationId id)
        => _reports.TryGetValue(id, out var report)
            ? report
            : throw new ResourceNotFoundException("evaluation.report.not_found", $"Evaluation report '{id}' was not found.");
}
