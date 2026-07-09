using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Domain.Tracing.ValueObjects;
using ConvoLab.Domain.Tracing.Entities;
using ConvoLab.Domain.Tracing.Enums;
using ConvoLab.Domain.Tracing.Aggregates;
namespace ConvoLab.Application.Services;
public class PlaceholderTraceEngine : ITraceEngine {
    public Task<TraceId> StartTraceAsync(string operationName, CancellationToken cancellationToken = default) => Task.FromResult(new TraceId(Guid.NewGuid()));
    public Task AddSpanToTraceAsync(TraceId traceId, string spanName, string? parentSpanId = null, Dictionary<string, string>? attributes = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task EndTraceAsync(TraceId traceId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task FailTraceAsync(TraceId traceId, string errorMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<Trace> GetTraceAsync(TraceId traceId, CancellationToken cancellationToken = default) => Task.FromResult(Trace.Start("Dummy"));
}
