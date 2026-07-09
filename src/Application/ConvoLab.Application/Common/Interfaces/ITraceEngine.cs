using ConvoLab.Domain.Tracing.ValueObjects;
using ConvoLab.Domain.Tracing.Entities;
using ConvoLab.Domain.Tracing.Aggregates;
namespace ConvoLab.Application.Common.Interfaces;
public interface ITraceEngine {
    Task<TraceId> StartTraceAsync(string operationName, CancellationToken cancellationToken = default);
    Task AddSpanToTraceAsync(TraceId traceId, string spanName, string? parentSpanId = null, Dictionary<string, string>? attributes = null, CancellationToken cancellationToken = default);
    Task EndTraceAsync(TraceId traceId, CancellationToken cancellationToken = default);
    Task FailTraceAsync(TraceId traceId, string errorMessage, CancellationToken cancellationToken = default);
    Task<Trace> GetTraceAsync(TraceId traceId, CancellationToken cancellationToken = default);
}
