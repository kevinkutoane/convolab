using System.Text.Json;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Simulation;

namespace ConvoLab.Application.TraceStudio;

public sealed class TraceStudioService(
    ITraceStudioRepository repository,
    IConversationSimulationStore simulations) : ITraceStudioService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<TraceOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        await SynchronizeSimulationRunsAsync(cancellationToken);
        var traces = await repository.ListAsync(1000, cancellationToken);
        var spans = traces.SelectMany(item => item.Spans).ToList();
        var completed = traces.Count(item => item.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase));
        var failed = traces.Count(item => item.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase));
        var durations = traces.Where(item => item.DurationMs >= 0).Select(item => item.DurationMs).Order().ToList();
        var currency = traces.Select(item => item.Currency).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? "ZAR";
        var metrics = new TraceMetricsDto(
            traces.Count,
            completed,
            failed,
            traces.Count == 0 ? 0 : (double)completed / traces.Count,
            spans.Count,
            durations.Count == 0 ? 0 : durations.Average(),
            Percentile(durations, .95),
            traces.Sum(item => (long)item.TotalTokens),
            traces.Sum(item => item.ActualCost),
            currency);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-6));
        var activity = Enumerable.Range(0, 7)
            .Select(offset => startDate.AddDays(offset))
            .Select(date =>
            {
                var day = traces.Where(item => DateOnly.FromDateTime(item.StartedAt.UtcDateTime) == date).ToList();
                return new TraceDailyActivityDto(
                    date,
                    day.Count,
                    day.Count(item => item.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase)),
                    day.Count == 0 ? 0 : day.Average(item => item.DurationMs));
            })
            .ToList();

        var capabilityGroups = spans
            .GroupBy(item => string.IsNullOrWhiteSpace(item.Capability) ? "Platform" : item.Capability, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Select(group => new TraceCapabilityMetricDto(
                group.Key,
                group.Count(),
                group.Count(item => item.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase)),
                group.Average(item => item.DurationMs),
                spans.Count == 0 ? 0 : (double)group.Count() / spans.Count))
            .ToList();

        return new TraceOverviewDto(
            metrics,
            activity,
            capabilityGroups,
            traces.OrderByDescending(item => item.StartedAt).Take(100).Select(MapSummary).ToList(),
            traces.Select(item => item.Provider).Where(item => !string.IsNullOrWhiteSpace(item)).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList(),
            traces.Select(item => item.Status).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList(),
            DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<TraceSummaryDto>> ListAsync(TraceSearchQuery query, CancellationToken cancellationToken = default)
    {
        await SynchronizeSimulationRunsAsync(cancellationToken);
        var traces = await repository.ListAsync(Math.Clamp(query.Limit, 1, 1000), cancellationToken);
        IEnumerable<TraceState> filtered = traces;

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            var term = query.Query.Trim();
            filtered = filtered.Where(item =>
                Contains(item.OperationName, term) || Contains(item.SimulationTitle, term) || Contains(item.Provider, term) ||
                Contains(item.Model, term) || Contains(item.Workflow, term) || Contains(item.PromptVersion, term) ||
                Contains(item.KnowledgeCollection, term) || item.CorrelationId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(query.Status))
            filtered = filtered.Where(item => item.Status.Equals(query.Status, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(query.Provider))
            filtered = filtered.Where(item => string.Equals(item.Provider, query.Provider, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(query.Capability))
            filtered = filtered.Where(item => item.Spans.Any(span => span.Capability.Equals(query.Capability, StringComparison.OrdinalIgnoreCase)));
        if (query.From.HasValue)
            filtered = filtered.Where(item => item.StartedAt >= query.From.Value);
        if (query.To.HasValue)
            filtered = filtered.Where(item => item.StartedAt <= query.To.Value);

        return filtered.OrderByDescending(item => item.StartedAt).Take(Math.Clamp(query.Limit, 1, 1000)).Select(MapSummary).ToList();
    }

    public async Task<TraceDetailDto> GetAsync(Guid id, bool includeSensitive = false, CancellationToken cancellationToken = default)
    {
        var trace = await repository.GetAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("trace.not_found", $"Trace '{id}' was not found.");
        return MapDetail(trace, includeSensitive);
    }

    public async Task<TraceDetailDto> RecordSimulationRunAsync(
        Guid simulationId,
        string simulationTitle,
        SimulationRun run,
        string? responseText = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetBySourceRunAsync(run.Id, cancellationToken);
        if (existing is not null) return MapDetail(existing, false);

        var traceId = run.Id;
        var rootSpanId = Guid.NewGuid();
        var completedAt = run.Timeline.Count == 0
            ? run.CreatedAt
            : run.Timeline.Max(item => item.StartedAt.AddMilliseconds(Math.Max(0, item.DurationMs)));
        var durationMs = Math.Max(0, (completedAt - run.CreatedAt).TotalMilliseconds);
        var status = run.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase) ? "Failed" : "Completed";
        var provider = run.ExecutionPlan?.Provider;
        var model = run.ExecutionPlan?.Model;

        var rootAttributes = new Dictionary<string, string>
        {
            ["simulation.id"] = simulationId.ToString(),
            ["run.id"] = run.Id.ToString(),
            ["run.mode"] = run.Mode.ToString(),
            ["run.replay"] = (run.ReplayedFromRunId.HasValue).ToString(),
            ["correlation.id"] = run.Id.ToString()
        };
        if (!string.IsNullOrWhiteSpace(provider)) rootAttributes["ai.provider"] = provider;
        if (!string.IsNullOrWhiteSpace(model)) rootAttributes["ai.model"] = model;

        var spans = new List<TraceSpanState>
        {
            new(rootSpanId, traceId, null, "Conversation simulation", "Platform", status,
                $"{simulationTitle} execution run", 0, run.CreatedAt, completedAt, durationMs, rootAttributes)
        };

        spans.AddRange(run.Timeline.Select((step, index) => new TraceSpanState(
            step.Id,
            traceId,
            rootSpanId,
            step.Name,
            step.Capability,
            NormalizeSpanStatus(step.Status),
            step.Detail,
            index + 1,
            step.StartedAt,
            step.StartedAt.AddMilliseconds(Math.Max(0, step.DurationMs)),
            Math.Max(0, step.DurationMs),
            BuildSpanAttributes(run, step))));

        var events = new List<TraceEventState>
        {
            new(Guid.NewGuid(), traceId, rootSpanId, "trace.started", "Information",
                $"Trace started for simulation '{simulationTitle}'.", run.CreatedAt, rootAttributes)
        };
        events.AddRange(run.Timeline.Select(step => new TraceEventState(
            Guid.NewGuid(), traceId, step.Id, $"span.{step.Status.ToLowerInvariant()}", EventLevel(step.Status), step.Detail,
            step.StartedAt.AddMilliseconds(Math.Max(0, step.DurationMs)),
            new Dictionary<string, string> { ["capability"] = step.Capability, ["span.name"] = step.Name })));
        events.Add(new TraceEventState(
            Guid.NewGuid(), traceId, rootSpanId, status == "Failed" ? "trace.failed" : "trace.completed",
            status == "Failed" ? "Error" : "Information",
            run.FailureReason ?? $"Trace completed in {durationMs:F1} ms.", completedAt,
            new Dictionary<string, string> { ["duration.ms"] = durationMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) }));

        var artifacts = BuildArtifacts(traceId, rootSpanId, run, responseText);
        var state = new TraceState(
            traceId,
            run.Id,
            "ConversationSimulation.Execute",
            run.ReplayedFromRunId.HasValue ? "Replay" : "Simulation",
            status,
            simulationId,
            simulationTitle,
            run.Id,
            run.ReplayedFromRunId,
            provider,
            model,
            run.Workflow is null ? null : $"{run.Workflow.Name} v{run.Workflow.Version}",
            run.Configuration?.PromptVersion ?? ExtractPromptVersion(run.RenderedPrompt),
            run.KnowledgePackage.Collection,
            run.Evaluation.Verdict,
            durationMs,
            run.Metrics?.TotalTokens ?? 0,
            run.Metrics?.ActualCost ?? 0,
            run.Metrics?.Currency ?? "ZAR",
            run.FailureReason,
            run.CreatedAt,
            completedAt,
            spans,
            events,
            artifacts);

        await repository.AddAsync(state, cancellationToken);
        return MapDetail(await repository.GetAsync(traceId, cancellationToken) ?? state, false);
    }

    private async Task SynchronizeSimulationRunsAsync(CancellationToken cancellationToken)
    {
        var states = await simulations.ListAsync(cancellationToken);
        foreach (var state in states)
        {
            var snapshot = state.Snapshot();
            foreach (var run in snapshot.Runs)
            {
                var response = run.AssistantMessageId.HasValue ? state.FindMessage(run.AssistantMessageId.Value)?.Content : null;
                await RecordSimulationRunAsync(snapshot.Id, snapshot.Title, run, response, cancellationToken);
            }
        }
    }

    private static IReadOnlyList<TraceArtifactState> BuildArtifacts(Guid traceId, Guid rootSpanId, SimulationRun run, string? responseText)
    {
        var now = DateTimeOffset.UtcNow;
        var artifacts = new List<TraceArtifactState>
        {
            new(Guid.NewGuid(), traceId, rootSpanId, "Prompt", "Rendered prompt", "text/plain", run.RenderedPrompt, true, now),
            new(Guid.NewGuid(), traceId, rootSpanId, "Knowledge", "Knowledge package", "application/json", Serialize(run.KnowledgePackage), false, now),
            new(Guid.NewGuid(), traceId, rootSpanId, "Evaluation", "Evaluation result", "application/json", Serialize(run.Evaluation), false, now),
            new(Guid.NewGuid(), traceId, rootSpanId, "Timeline", "Execution timeline", "application/json", Serialize(run.Timeline), false, now)
        };
        if (run.Workflow is not null)
            artifacts.Add(new TraceArtifactState(Guid.NewGuid(), traceId, rootSpanId, "Workflow", "Workflow snapshot", "application/json", Serialize(run.Workflow), false, now));
        if (run.ExecutionPlan is not null)
            artifacts.Add(new TraceArtifactState(Guid.NewGuid(), traceId, rootSpanId, "ExecutionPlan", "Execution plan", "application/json", Serialize(run.ExecutionPlan), false, now));
        if (run.Metrics is not null)
            artifacts.Add(new TraceArtifactState(Guid.NewGuid(), traceId, rootSpanId, "Metrics", "Execution metrics", "application/json", Serialize(run.Metrics), false, now));
        if (!string.IsNullOrWhiteSpace(responseText))
            artifacts.Add(new TraceArtifactState(Guid.NewGuid(), traceId, rootSpanId, "Response", "Assistant response", "text/plain", responseText, true, now));
        if (!string.IsNullOrWhiteSpace(run.FailureReason))
            artifacts.Add(new TraceArtifactState(Guid.NewGuid(), traceId, rootSpanId, "Error", "Failure detail", "text/plain", run.FailureReason, true, now));
        return artifacts;
    }

    private static IReadOnlyDictionary<string, string> BuildSpanAttributes(SimulationRun run, SimulationTimelineStep step)
    {
        var values = new Dictionary<string, string>
        {
            ["capability"] = step.Capability,
            ["status"] = step.Status,
            ["duration.ms"] = step.DurationMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
        };
        if (step.Capability.Equals("Intelligence", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(run.ExecutionPlan?.Provider)) values["ai.provider"] = run.ExecutionPlan.Provider;
            if (!string.IsNullOrWhiteSpace(run.ExecutionPlan?.Model)) values["ai.model"] = run.ExecutionPlan.Model;
            if (run.Metrics is not null) values["ai.tokens.total"] = run.Metrics.TotalTokens.ToString();
        }
        if (step.Capability.Equals("Knowledge", StringComparison.OrdinalIgnoreCase))
            values["knowledge.citations"] = run.KnowledgePackage.Citations.Count.ToString();
        return values;
    }

    private static TraceSummaryDto MapSummary(TraceState trace) => new(
        trace.Id, trace.CorrelationId, trace.OperationName, trace.Source, trace.Status, trace.SimulationId,
        trace.SimulationTitle, trace.SourceRunId, trace.ReplayedFromRunId, trace.Provider, trace.Model, trace.Workflow,
        trace.PromptVersion, trace.KnowledgeCollection, trace.EvaluationVerdict, trace.DurationMs, trace.Spans.Count,
        trace.Spans.Count(item => item.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase)), trace.TotalTokens,
        trace.ActualCost, trace.Currency, trace.FailureReason, trace.StartedAt, trace.CompletedAt);

    private static TraceDetailDto MapDetail(TraceState trace, bool includeSensitive) => new(
        MapSummary(trace),
        trace.Spans.OrderBy(item => item.Sequence).Select(item => new TraceSpanDto(
            item.Id, item.TraceId, item.ParentSpanId, item.Name, item.Capability, item.Status, item.Detail, item.Sequence,
            item.StartedAt, item.CompletedAt, item.DurationMs, item.Attributes)).ToList(),
        trace.Events.OrderBy(item => item.OccurredAt).Select(item => new TraceEventDto(
            item.Id, item.TraceId, item.SpanId, item.Name, item.Level, item.Message, item.OccurredAt, item.Attributes)).ToList(),
        trace.Artifacts.OrderBy(item => item.CreatedAt).Select(item => new TraceArtifactDto(
            item.Id, item.TraceId, item.SpanId, item.Kind, item.Name, item.ContentType,
            item.IsSensitive && !includeSensitive ? "[redacted — request includeSensitive=true to reveal]" : item.Content,
            item.IsSensitive, item.IsSensitive && !includeSensitive, item.CreatedAt)).ToList());

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    private static bool Contains(string? value, string term) => !string.IsNullOrWhiteSpace(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase);
    private static string NormalizeSpanStatus(string status) => status.Equals("Failed", StringComparison.OrdinalIgnoreCase) ? "Failed" : status.Equals("Warning", StringComparison.OrdinalIgnoreCase) ? "Warning" : "Completed";
    private static string EventLevel(string status) => status.Equals("Failed", StringComparison.OrdinalIgnoreCase) ? "Error" : status.Equals("Warning", StringComparison.OrdinalIgnoreCase) ? "Warning" : "Information";
    private static string? ExtractPromptVersion(string renderedPrompt)
    {
        if (string.IsNullOrWhiteSpace(renderedPrompt)) return null;
        var marker = "Prompt version:";
        var index = renderedPrompt.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return null;
        var line = renderedPrompt[(index + marker.Length)..].Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return line;
    }

    private static double Percentile(IReadOnlyList<double> ordered, double percentile)
    {
        if (ordered.Count == 0) return 0;
        var position = (ordered.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper) return ordered[lower];
        var weight = position - lower;
        return ordered[lower] * (1 - weight) + ordered[upper] * weight;
    }
}
