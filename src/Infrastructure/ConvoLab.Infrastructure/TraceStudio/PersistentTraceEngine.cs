using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Application.TraceStudio;
using ConvoLab.Domain.Tracing.Aggregates;
using ConvoLab.Domain.Tracing.Entities;
using ConvoLab.Domain.Tracing.Enums;
using ConvoLab.Domain.Tracing.ValueObjects;

namespace ConvoLab.Infrastructure.TraceStudio;

public sealed class PersistentTraceEngine(ITraceStudioRepository repository) : ITraceEngine
{
    public async Task<TraceId> StartTraceAsync(string operationName, CancellationToken cancellationToken = default)
    {
        var trace = Trace.Start(operationName, Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;
        await repository.AddAsync(new TraceState(
            trace.Id.Value, trace.CorrelationId, trace.OperationName, "Runtime", "InProgress", null, null, null, null,
            null, null, null, null, null, null, 0, 0, 0, "ZAR", null, now, null, [],
            [new TraceEventState(Guid.NewGuid(), trace.Id.Value, null, "trace.started", "Information", $"Trace '{operationName}' started.", now, new Dictionary<string, string> { ["correlation.id"] = trace.CorrelationId.ToString() })],
            []), cancellationToken);
        return trace.Id;
    }

    public async Task AddSpanToTraceAsync(
        TraceId traceId,
        string spanName,
        string? parentSpanId = null,
        Dictionary<string, string>? attributes = null,
        CancellationToken cancellationToken = default)
    {
        var trace = await repository.GetAsync(traceId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Trace '{traceId.Value}' was not found.");
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        Guid? parent = Guid.TryParse(parentSpanId, out var parsed) ? parsed : null;
        var sequence = trace.Spans.Count == 0 ? 1 : trace.Spans.Max(item => item.Sequence) + 1;
        var span = new TraceSpanState(id, traceId.Value, parent, spanName, attributes?.GetValueOrDefault("capability") ?? "Platform",
            "Completed", attributes?.GetValueOrDefault("detail") ?? spanName, sequence, now, now, 0,
            attributes ?? new Dictionary<string, string>());
        var traceEvent = new TraceEventState(Guid.NewGuid(), traceId.Value, id, "span.completed", "Information", span.Detail, now, span.Attributes);
        await repository.AddSpanAsync(span, traceEvent, cancellationToken);
    }

    public async Task EndTraceAsync(TraceId traceId, CancellationToken cancellationToken = default)
    {
        var trace = await repository.GetAsync(traceId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Trace '{traceId.Value}' was not found.");
        var completedAt = DateTimeOffset.UtcNow;
        await repository.CompleteAsync(traceId.Value, "Completed", completedAt, Math.Max(0, (completedAt - trace.StartedAt).TotalMilliseconds), null, cancellationToken);
    }

    public async Task FailTraceAsync(TraceId traceId, string errorMessage, CancellationToken cancellationToken = default)
    {
        var trace = await repository.GetAsync(traceId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Trace '{traceId.Value}' was not found.");
        var completedAt = DateTimeOffset.UtcNow;
        await repository.CompleteAsync(traceId.Value, "Failed", completedAt, Math.Max(0, (completedAt - trace.StartedAt).TotalMilliseconds), errorMessage, cancellationToken);
    }

    public async Task<Trace> GetTraceAsync(TraceId traceId, CancellationToken cancellationToken = default)
    {
        var state = await repository.GetAsync(traceId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Trace '{traceId.Value}' was not found.");
        var spans = state.Spans.Select(item => TraceSpan.Restore(
            item.Id,
            item.TraceId,
            item.Name,
            item.StartedAt.UtcDateTime,
            item.CompletedAt?.UtcDateTime,
            Enum.TryParse<SpanStatus>(item.Status, true, out var spanStatus) ? spanStatus : SpanStatus.Completed,
            item.ParentSpanId,
            item.Attributes)).ToList();
        return Trace.Restore(
            TraceId.FromGuid(state.Id),
            state.OperationName,
            state.CorrelationId,
            state.StartedAt.UtcDateTime,
            state.CompletedAt?.UtcDateTime,
            Enum.TryParse<TraceStatus>(state.Status, true, out var traceStatus) ? traceStatus : TraceStatus.InProgress,
            spans);
    }
}
