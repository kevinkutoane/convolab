using System.Text.Json;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.TraceStudio;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.TraceStudio;

public sealed class EfTraceStudioRepository(ApplicationDbContext db) : ITraceStudioRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<TraceState>> ListAsync(int limit = 500, CancellationToken cancellationToken = default)
    {
        var records = (await db.Traces.AsNoTracking().ToListAsync(cancellationToken))
            .OrderByDescending(item => item.StartedAt).Take(limit).ToList();
        return await HydrateAsync(records, cancellationToken);
    }

    public async Task<TraceState?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await db.Traces.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (record is null) return null;
        return (await HydrateAsync([record], cancellationToken)).Single();
    }

    public async Task<TraceState?> GetBySourceRunAsync(Guid sourceRunId, CancellationToken cancellationToken = default)
    {
        var record = await db.Traces.AsNoTracking().SingleOrDefaultAsync(item => item.SourceRunId == sourceRunId, cancellationToken);
        if (record is null) return null;
        return (await HydrateAsync([record], cancellationToken)).Single();
    }

    public async Task AddAsync(TraceState trace, CancellationToken cancellationToken = default)
    {
        db.Traces.Add(MapRecord(trace));
        db.TraceSpans.AddRange(trace.Spans.Select(MapRecord));
        db.TraceEvents.AddRange(trace.Events.Select(MapRecord));
        db.TraceArtifacts.AddRange(trace.Artifacts.Select(MapRecord));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var exists = await db.Traces.AsNoTracking().AnyAsync(item => item.Id == trace.Id || (trace.SourceRunId.HasValue && item.SourceRunId == trace.SourceRunId), cancellationToken);
            if (!exists) throw;
            db.ChangeTracker.Clear();
        }
    }

    public async Task AddSpanAsync(TraceSpanState span, TraceEventState traceEvent, CancellationToken cancellationToken = default)
    {
        var traceExists = await db.Traces.AsNoTracking().AnyAsync(item => item.Id == span.TraceId, cancellationToken);
        if (!traceExists)
            throw new ResourceNotFoundException("trace.not_found", $"Trace '{span.TraceId}' was not found.");
        db.TraceSpans.Add(MapRecord(span));
        db.TraceEvents.Add(MapRecord(traceEvent));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(Guid id, string status, DateTimeOffset completedAt, double durationMs, string? failureReason, CancellationToken cancellationToken = default)
    {
        var record = await db.Traces.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ResourceNotFoundException("trace.not_found", $"Trace '{id}' was not found.");
        record.Status = status;
        record.CompletedAt = completedAt;
        record.DurationMs = durationMs;
        record.FailureReason = failureReason;
        db.TraceEvents.Add(new TraceEventRecord
        {
            Id = Guid.NewGuid(),
            TraceId = id,
            Name = status.Equals("Failed", StringComparison.OrdinalIgnoreCase) ? "trace.failed" : "trace.completed",
            Level = status.Equals("Failed", StringComparison.OrdinalIgnoreCase) ? "Error" : "Information",
            Message = failureReason ?? $"Trace completed in {durationMs:F1} ms.",
            OccurredAt = completedAt,
            AttributesJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["duration.ms"] = durationMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) }, JsonOptions)
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<TraceState>> HydrateAsync(IReadOnlyList<TraceRecord> records, CancellationToken cancellationToken)
    {
        if (records.Count == 0) return [];
        var ids = records.Select(item => item.Id).ToList();
        var spans = await db.TraceSpans.AsNoTracking().Where(item => ids.Contains(item.TraceId)).OrderBy(item => item.Sequence).ToListAsync(cancellationToken);
        var events = (await db.TraceEvents.AsNoTracking().Where(item => ids.Contains(item.TraceId)).ToListAsync(cancellationToken))
            .OrderBy(item => item.OccurredAt).ToList();
        var artifacts = (await db.TraceArtifacts.AsNoTracking().Where(item => ids.Contains(item.TraceId)).ToListAsync(cancellationToken))
            .OrderBy(item => item.CreatedAt).ToList();
        return records.Select(record => Map(
            record,
            spans.Where(item => item.TraceId == record.Id).ToList(),
            events.Where(item => item.TraceId == record.Id).ToList(),
            artifacts.Where(item => item.TraceId == record.Id).ToList())).ToList();
    }

    private static TraceState Map(TraceRecord record, IReadOnlyList<TraceSpanRecord> spans, IReadOnlyList<TraceEventRecord> events, IReadOnlyList<TraceArtifactRecord> artifacts) => new(
        record.Id, record.CorrelationId, record.OperationName, record.Source, record.Status, record.SimulationId, record.SimulationTitle,
        record.SourceRunId, record.ReplayedFromRunId, record.Provider, record.Model, record.Workflow, record.PromptVersion,
        record.KnowledgeCollection, record.EvaluationVerdict, record.DurationMs, record.TotalTokens, record.ActualCost,
        record.Currency, record.FailureReason, record.StartedAt, record.CompletedAt,
        spans.Select(item => new TraceSpanState(item.Id, item.TraceId, item.ParentSpanId, item.Name, item.Capability, item.Status,
            item.Detail, item.Sequence, item.StartedAt, item.CompletedAt, item.DurationMs, Deserialize(item.AttributesJson))).ToList(),
        events.Select(item => new TraceEventState(item.Id, item.TraceId, item.SpanId, item.Name, item.Level, item.Message,
            item.OccurredAt, Deserialize(item.AttributesJson))).ToList(),
        artifacts.Select(item => new TraceArtifactState(item.Id, item.TraceId, item.SpanId, item.Kind, item.Name, item.ContentType,
            item.Content, item.IsSensitive, item.CreatedAt)).ToList());

    private static TraceRecord MapRecord(TraceState state) => new()
    {
        Id = state.Id, CorrelationId = state.CorrelationId, OperationName = state.OperationName, Source = state.Source,
        Status = state.Status, SimulationId = state.SimulationId, SimulationTitle = state.SimulationTitle, SourceRunId = state.SourceRunId,
        ReplayedFromRunId = state.ReplayedFromRunId, Provider = state.Provider, Model = state.Model, Workflow = state.Workflow,
        PromptVersion = state.PromptVersion, KnowledgeCollection = state.KnowledgeCollection, EvaluationVerdict = state.EvaluationVerdict,
        DurationMs = state.DurationMs, TotalTokens = state.TotalTokens, ActualCost = state.ActualCost, Currency = state.Currency,
        FailureReason = state.FailureReason, StartedAt = state.StartedAt, CompletedAt = state.CompletedAt
    };

    private static TraceSpanRecord MapRecord(TraceSpanState state) => new()
    {
        Id = state.Id, TraceId = state.TraceId, ParentSpanId = state.ParentSpanId, Name = state.Name, Capability = state.Capability,
        Status = state.Status, Detail = state.Detail, Sequence = state.Sequence, StartedAt = state.StartedAt,
        CompletedAt = state.CompletedAt, DurationMs = state.DurationMs, AttributesJson = JsonSerializer.Serialize(state.Attributes, JsonOptions)
    };

    private static TraceEventRecord MapRecord(TraceEventState state) => new()
    {
        Id = state.Id, TraceId = state.TraceId, SpanId = state.SpanId, Name = state.Name, Level = state.Level,
        Message = state.Message, OccurredAt = state.OccurredAt, AttributesJson = JsonSerializer.Serialize(state.Attributes, JsonOptions)
    };

    private static TraceArtifactRecord MapRecord(TraceArtifactState state) => new()
    {
        Id = state.Id, TraceId = state.TraceId, SpanId = state.SpanId, Kind = state.Kind, Name = state.Name,
        ContentType = state.ContentType, Content = state.Content, IsSensitive = state.IsSensitive, CreatedAt = state.CreatedAt
    };

    private static IReadOnlyDictionary<string, string> Deserialize(string json)
        => JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? new Dictionary<string, string>();
}
