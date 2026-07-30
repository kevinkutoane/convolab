using System.Globalization;
using System.Text;
using System.Text.Json;
using ConvoLab.Application.Analytics;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Settings;
using ConvoLab.Domain.Analytics;
using ConvoLab.Domain.Settings;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.Analytics;

public sealed class AnalyticsService(
    ApplicationDbContext db,
    IEffectiveConfigurationResolver effectiveConfiguration) : IAnalyticsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AnalyticsFilterOptionsDto> FilterOptionsAsync(
        AnalyticsQuery query,
        CancellationToken ct = default)
    {
        ValidateQuery(query, 366);
        await RequireEnvironmentAsync(query.WorkspaceId, query.EnvironmentId, ct);
        var rows = BuildQuery(query);

        return new AnalyticsFilterOptionsDto(
            await rows.Where(item => item.Provider != null && item.Provider != "")
                .Select(item => item.Provider!).Distinct().OrderBy(value => value).Take(200).ToListAsync(ct),
            await rows.Where(item => item.Model != null && item.Model != "")
                .Select(item => item.Model!).Distinct().OrderBy(value => value).Take(200).ToListAsync(ct),
            await rows.Where(item => item.Capability != "")
                .Select(item => item.Capability).Distinct().OrderBy(value => value).Take(200).ToListAsync(ct),
            await rows.Where(item => item.Outcome != "")
                .Select(item => item.Outcome).Distinct().OrderBy(value => value).Take(200).ToListAsync(ct),
            await rows.Where(item => item.PromptName != null && item.PromptName != "")
                .Select(item => item.PromptName!).Distinct().OrderBy(value => value).Take(200).ToListAsync(ct),
            await rows.Where(item => item.WorkflowName != null && item.WorkflowName != "")
                .Select(item => item.WorkflowName!).Distinct().OrderBy(value => value).Take(200).ToListAsync(ct),
            await rows.Where(item => item.KnowledgeCollectionName != null && item.KnowledgeCollectionName != "")
                .Select(item => item.KnowledgeCollectionName!).Distinct().OrderBy(value => value).Take(200).ToListAsync(ct),
            await rows.Where(item => item.ConfigurationRevision != "")
                .Select(item => item.ConfigurationRevision).Distinct().OrderBy(value => value).Take(100).ToListAsync(ct),
            await rows.Where(item => item.EventType != "")
                .Select(item => item.EventType).Distinct().OrderBy(value => value).Take(200).ToListAsync(ct),
            await rows.Where(item => item.CostType != "")
                .Select(item => item.CostType).Distinct().OrderBy(value => value).Take(20).ToListAsync(ct));
    }

    public async Task<AnalyticsDashboardDto> DashboardAsync(
        string category,
        AnalyticsQuery query,
        AnalyticsFieldVisibility visibility,
        CancellationToken ct = default)
    {
        ValidateCategory(category);
        ValidateQuery(query, query.Granularity.Equals("hour", StringComparison.OrdinalIgnoreCase) ? 31 : 366);
        await RequireEnvironmentAsync(query.WorkspaceId, query.EnvironmentId, ct);

        if (query.Granularity == "day"
            && query.From < DateTimeOffset.UtcNow.AddDays(-90)
            && query.ConfigurationRevision is null
            && query.Prompt is null
            && query.Workflow is null
            && query.KnowledgeCollection is null
            && query.ActorId is null
            && query.EventType is null
            && query.CostType is null)
            return await AggregateDashboardAsync(category, query, visibility, ct);

        var events = await LoadAsync(query, ct);
        var series = events
            .GroupBy(item => Bucket(item.OccurredAt, query.Granularity))
            .OrderBy(group => group.Key)
            .Select(group => Point(group.Key, group.ToList(), visibility))
            .ToList();
        var metrics = await BuildMetricsAsync(category, events, query, visibility, ct);
        var indicators = BuildIndicators(events, query);

        return new AnalyticsDashboardDto(
            category,
            Scope(query),
            metrics,
            series,
            indicators,
            false,
            DateTimeOffset.UtcNow);
    }

    public async Task<AnalyticsEventPageDto> EventsAsync(
        AnalyticsQuery query,
        int take,
        string? cursor,
        AnalyticsFieldVisibility visibility,
        CancellationToken ct = default)
    {
        ValidateQuery(query, 31);
        await RequireEnvironmentAsync(query.WorkspaceId, query.EnvironmentId, ct);
        if (take is < 1 or > 200)
            throw new RequestValidationException(
                "analytics.page_size_invalid",
                "Analytics event page size must be between 1 and 200.");

        var hasCursor = TryDecodeCursor(cursor, out var occurredAt, out var id);
        List<AnalyticsEventRecord> page;
        long total;

        if (db.Database.IsSqlite())
        {
            var rows = await LoadAsync(query, ct);
            total = rows.Count;
            if (hasCursor)
                rows = rows.Where(item =>
                    item.OccurredAt < occurredAt
                    || item.OccurredAt == occurredAt && item.Id.CompareTo(id) < 0).ToList();
            page = rows.OrderByDescending(item => item.OccurredAt)
                .ThenByDescending(item => item.Id).Take(take + 1).ToList();
        }
        else
        {
            var filtered = BuildQuery(query);
            total = await filtered.LongCountAsync(ct);
            if (hasCursor)
                filtered = filtered.Where(item =>
                    item.OccurredAt < occurredAt
                    || item.OccurredAt == occurredAt && item.Id.CompareTo(id) < 0);
            page = await filtered.OrderByDescending(item => item.OccurredAt)
                .ThenByDescending(item => item.Id).Take(take + 1).ToListAsync(ct);
        }

        var hasMore = page.Count > take;
        if (hasMore) page.RemoveAt(page.Count - 1);
        var next = hasMore && page.Count > 0 ? EncodeCursor(page[^1]) : null;
        return new AnalyticsEventPageDto(
            Scope(query),
            page.Select(item => Map(item, visibility)).ToList(),
            next,
            total);
    }

    public async Task<AnalyticsEventDto> EventAsync(
        Guid workspaceId,
        Guid eventId,
        AnalyticsFieldVisibility visibility,
        CancellationToken ct = default)
    {
        var item = await db.AnalyticsEvents.AsNoTracking()
            .SingleOrDefaultAsync(value => value.WorkspaceId == workspaceId && value.Id == eventId, ct)
            ?? throw new ResourceNotFoundException(
                "analytics.event_not_found",
                $"Analytics event '{eventId}' was not found.");
        return Map(item, visibility);
    }

    public async Task<IReadOnlyList<AnalyticsEventDto>> CorrelationAsync(
        Guid workspaceId,
        string correlationId,
        AnalyticsFieldVisibility visibility,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 100)
            throw new RequestValidationException(
                "analytics.correlation_invalid",
                "A valid correlation id is required.");

        var query = db.AnalyticsEvents.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.CorrelationId == correlationId);
        var items = db.Database.IsNpgsql()
            ? await query.OrderBy(item => item.OccurredAt).ThenBy(item => item.Id).ToListAsync(ct)
            : (await query.ToListAsync(ct))
                .OrderBy(item => item.OccurredAt).ThenBy(item => item.Id).ToList();
        return items.Select(item => Map(item, visibility)).ToList();
    }

    public async Task<AnalyticsExportDto> CreateExportAsync(
        Guid workspaceId,
        Guid actorId,
        CreateAnalyticsExportRequest request,
        AnalyticsFieldVisibility visibility,
        CancellationToken ct = default)
    {
        var query = ToQuery(workspaceId, request);
        ValidateQuery(query, 90);
        await RequireEnvironmentAsync(workspaceId, request.EnvironmentId, ct);
        if (request.ActorId.HasValue && !visibility.IncludeActor)
            throw new RequestValidationException(
                "analytics.actor_filter_forbidden",
                "Actor filters require actor-level Analytics permission.");

        var retentionDays = await ExportRetentionDaysAsync(
            workspaceId, request.EnvironmentId, ct);
        var now = DateTimeOffset.UtcNow;
        var record = new AnalyticsExportRecord
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            CreatedBy = actorId,
            Status = "Pending",
            FileName = $"convolab-analytics-{now:yyyyMMdd-HHmmss}.csv",
            FiltersJson = JsonSerializer.Serialize(request, JsonOptions),
            IncludeActor = visibility.IncludeActor,
            IncludeCost = visibility.IncludeCost,
            IncludeTokenUsage = visibility.IncludeTokenUsage,
            IncludeProviderDetails = visibility.IncludeProviderDetails,
            IncludeSensitiveSource = visibility.IncludeSensitiveSource,
            RetentionDays = retentionDays,
            CreatedAt = now,
            ExpiresAt = now.AddDays(retentionDays)
        };
        db.AnalyticsExports.Add(record);
        var organisationId = await db.Workspaces.AsNoTracking()
            .Where(item => item.Id == workspaceId)
            .Select(item => item.OrganisationId)
            .SingleAsync(ct);
        var exportEnvironmentId = request.EnvironmentId
            ?? await db.RuntimeEnvironments.AsNoTracking()
                .Where(item => item.WorkspaceId == workspaceId
                    && item.IsDefault
                    && item.Status == "Active")
                .Select(item => (Guid?)item.Id)
                .SingleOrDefaultAsync(ct);
        if (exportEnvironmentId.HasValue)
        {
            AnalyticsOutboxFactory.Enqueue(db, new AnalyticsEventRecord
            {
                Id = Guid.NewGuid(),
                EventKey = AnalyticsKeys.Event(
                    "AnalyticsExport",
                    record.Id,
                    "AnalyticsExportCreated"),
                OrganisationId = organisationId,
                WorkspaceId = workspaceId,
                EnvironmentId = exportEnvironmentId.Value,
                ActorId = actorId,
                ActorType = "User",
                Capability = "Analytics",
                EventType = "AnalyticsExportCreated",
                Outcome = "Succeeded",
                CostType = "Unavailable",
                SourceType = "AnalyticsExport",
                SourceId = record.Id,
                ConfigurationRevision = "not-applicable",
                CorrelationId = $"analytics-export:{record.Id:N}",
                OccurredAt = now
            });
        }
        await db.SaveChangesAsync(ct);
        return Map(record);
    }

    public async Task<IReadOnlyList<AnalyticsExportDto>> ExportsAsync(
        Guid workspaceId,
        CancellationToken ct = default)
    {
        await RequireEnvironmentAsync(workspaceId, null, ct);
        return (await db.AnalyticsExports.AsNoTracking()
                .Where(item => item.WorkspaceId == workspaceId)
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync(ct))
            .Select(Map).ToList();
    }

    public async Task<AnalyticsExportDto> ExportAsync(
        Guid workspaceId,
        Guid exportId,
        CancellationToken ct = default) =>
        Map(await FindExportAsync(workspaceId, exportId, ct));

    public async Task<(byte[] Content, string FileName)> DownloadExportAsync(
        Guid workspaceId,
        Guid exportId,
        CancellationToken ct = default)
    {
        var export = await FindExportAsync(workspaceId, exportId, ct);
        if (export.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ResourceConflictException(
                "analytics.export_expired",
                "The analytics export has expired.");
        if (export.Status != "Completed" || export.Content is null)
            throw new ResourceConflictException(
                "analytics.export_not_ready",
                "The analytics export is not ready for download.");
        return (export.Content, export.FileName);
    }

    internal IQueryable<AnalyticsEventRecord> BuildQuery(AnalyticsQuery query)
    {
        var result = db.AnalyticsEvents.AsNoTracking()
            .Where(item => item.WorkspaceId == query.WorkspaceId);
        if (query.EnvironmentId.HasValue)
            result = result.Where(item => item.EnvironmentId == query.EnvironmentId.Value);
        if (!db.Database.IsSqlite())
            result = result.Where(item => item.OccurredAt >= query.From && item.OccurredAt < query.To);
        if (query.Provider is not null) result = result.Where(item => item.Provider == query.Provider);
        if (query.Model is not null) result = result.Where(item => item.Model == query.Model);
        if (query.Capability is not null) result = result.Where(item => item.Capability == query.Capability);
        if (query.Outcome is not null) result = result.Where(item => item.Outcome == query.Outcome);
        if (query.ConfigurationRevision is not null)
            result = result.Where(item => item.ConfigurationRevision == query.ConfigurationRevision);
        if (query.Prompt is not null) result = result.Where(item => item.PromptName == query.Prompt);
        if (query.Workflow is not null) result = result.Where(item => item.WorkflowName == query.Workflow);
        if (query.KnowledgeCollection is not null)
            result = result.Where(item => item.KnowledgeCollectionName == query.KnowledgeCollection);
        if (query.ActorId.HasValue) result = result.Where(item => item.ActorId == query.ActorId);
        if (query.EventType is not null) result = result.Where(item => item.EventType == query.EventType);
        if (query.CostType is not null) result = result.Where(item => item.CostType == query.CostType);
        return result;
    }

    private async Task<List<AnalyticsEventRecord>> LoadAsync(
        AnalyticsQuery query,
        CancellationToken ct)
    {
        var rows = await BuildQuery(query).ToListAsync(ct);
        return db.Database.IsSqlite()
            ? rows.Where(item => item.OccurredAt >= query.From && item.OccurredAt < query.To).ToList()
            : rows;
    }

    private async Task<AnalyticsDashboardDto> AggregateDashboardAsync(
        string category,
        AnalyticsQuery query,
        AnalyticsFieldVisibility visibility,
        CancellationToken ct)
    {
        var rowsQuery = db.AnalyticsDailyAggregates.AsNoTracking()
            .Where(item => item.WorkspaceId == query.WorkspaceId);
        if (query.EnvironmentId.HasValue)
            rowsQuery = rowsQuery.Where(item => item.EnvironmentId == query.EnvironmentId.Value);
        if (!db.Database.IsSqlite())
            rowsQuery = rowsQuery.Where(item => item.BucketStart >= query.From && item.BucketStart < query.To);
        if (query.Provider is not null) rowsQuery = rowsQuery.Where(item => item.Provider == query.Provider);
        if (query.Model is not null) rowsQuery = rowsQuery.Where(item => item.Model == query.Model);
        if (query.Capability is not null) rowsQuery = rowsQuery.Where(item => item.Capability == query.Capability);
        if (query.Outcome is not null) rowsQuery = rowsQuery.Where(item => item.Outcome == query.Outcome);

        var rows = await rowsQuery.ToListAsync(ct);
        if (db.Database.IsSqlite())
            rows = rows.Where(item => item.BucketStart >= query.From && item.BucketStart < query.To).ToList();

        var series = rows.GroupBy(item => item.BucketStart).OrderBy(group => group.Key)
            .Select(group =>
            {
                var durationCount = group.Sum(item => item.DurationCount);
                var qualityCount = group.Sum(item => item.QualityCount);
                return new AnalyticsPointDto(
                    group.Key,
                    group.Sum(item => item.EventCount),
                    group.Sum(item => item.ExecutionCount),
                    group.Sum(item => item.SimulationCount),
                    group.Sum(item => item.EvaluationCount),
                    group.Sum(item => item.ReplayCount),
                    group.Sum(item => item.ProviderInvocationCount),
                    group.Sum(item => item.ProviderInvocationPreventedCount),
                    group.Sum(item => item.PolicyEvaluationCount),
                    group.Sum(item => item.Succeeded),
                    group.Sum(item => item.Failed),
                    group.Sum(item => item.PolicyDeniedCount),
                    visibility.IncludeTokenUsage ? group.Sum(item => item.InputTokens) : null,
                    visibility.IncludeTokenUsage ? group.Sum(item => item.OutputTokens) : null,
                    visibility.IncludeCost ? group.Sum(item => item.ActualCostZar) : null,
                    visibility.IncludeCost ? group.Sum(item => item.EstimatedCostZar) : null,
                    visibility.IncludeCost ? group.Sum(item => item.UnknownCostCount) : null,
                    durationCount == 0 ? 0 : group.Sum(item => item.TotalDurationMs) / durationCount,
                    qualityCount == 0 ? null : group.Sum(item => item.TotalQualityScore) / qualityCount);
            })
            .ToList();

        var eventCount = rows.Sum(item => item.EventCount);
        var executionCount = rows.Sum(item => item.ExecutionCount);
        var metrics = new List<AnalyticsMetricDto>();
        void Count(string key, string label, long value) =>
            metrics.Add(new(key, label, value, "count"));
        void Percent(string key, string label, decimal? value) =>
            metrics.Add(new(key, label, value, "percent"));

        switch (category)
        {
            case "overview":
                Count("executionCount", "Executions", executionCount);
                Count("simulationCount", "Simulations", rows.Sum(item => item.SimulationCount));
                Count("evaluationCount", "Evaluations", rows.Sum(item => item.EvaluationCount));
                Count("replayCount", "Replays", rows.Sum(item => item.ReplayCount));
                Count("providerInvocationCount", "Provider invocations", rows.Sum(item => item.ProviderInvocationCount));
                Percent("policyDenialRate", "Policy denial rate",
                    Rate(rows.Sum(item => item.PolicyDeniedCount), rows.Sum(item => item.PolicyEvaluationCount)));
                AddCosts(
                    metrics,
                    rows.Sum(item => item.ActualCostZar),
                    rows.Sum(item => item.EstimatedCostZar),
                    rows.Sum(item => item.UnknownCostCount),
                    visibility);
                break;
            case "usage":
                Count("eventCount", "Operational events", eventCount);
                Count("executionCount", "Executions", executionCount);
                Count("simulationCount", "Simulations", rows.Sum(item => item.SimulationCount));
                Count("evaluationCount", "Evaluations", rows.Sum(item => item.EvaluationCount));
                Count("traceCount", "Traces", rows.Sum(item => item.TraceCount));
                Count("replayCount", "Replays", rows.Sum(item => item.ReplayCount));
                Count("pluginOperations", "Plugin operations", rows.Sum(item => item.PluginOperationCount));
                break;
            case "cost":
            case "budget":
                if (visibility.IncludeTokenUsage)
                {
                    Count("inputTokens", "Input tokens", rows.Sum(item => item.InputTokens));
                    Count("outputTokens", "Output tokens", rows.Sum(item => item.OutputTokens));
                }
                var actual = rows.Sum(item => item.ActualCostZar);
                var estimated = rows.Sum(item => item.EstimatedCostZar);
                AddCosts(metrics, actual, estimated, rows.Sum(item => item.UnknownCostCount), visibility);
                metrics.AddRange(await BudgetAsync(query, ct));
                break;
            case "quality":
                var qualityCount = rows.Sum(item => item.QualityCount);
                metrics.Add(new(
                    "averageOverall",
                    "Average overall score",
                    qualityCount == 0
                        ? null
                        : (decimal)(rows.Sum(item => item.TotalQualityScore) / qualityCount),
                    "score"));
                Count("evaluations", "Evaluations", rows.Sum(item => item.EvaluationCount));
                break;
            case "governance":
                Count("policyEvaluations", "Policy evaluations", rows.Sum(item => item.PolicyEvaluationCount));
                Count("policyAllowed", "Allowed decisions", rows.Sum(item => item.PolicyAllowedCount));
                Count("policyDenied", "Denied decisions", rows.Sum(item => item.PolicyDeniedCount));
                Count("policyWarnings", "Warning decisions", rows.Sum(item => item.PolicyWarningCount));
                Count("providerInvocationPrevented", "Provider invocations prevented",
                    rows.Sum(item => item.ProviderInvocationPreventedCount));
                break;
            case "performance":
                var durationCount = rows.Sum(item => item.DurationCount);
                metrics.Add(new(
                    "averageDuration",
                    "Average duration",
                    durationCount == 0
                        ? null
                        : (decimal)(rows.Sum(item => item.TotalDurationMs) / durationCount),
                    "ms"));
                metrics.Add(new(
                    "maximumDuration",
                    "Maximum duration",
                    (decimal?)rows.Max(item => (double?)item.MaximumDurationMs),
                    "ms"));
                Percent("successRate", "Success rate",
                    Rate(rows.Sum(item => item.Succeeded), rows.Sum(item => item.Succeeded + item.Failed)));
                break;
            case "adoption":
                metrics.Add(new(
                    "activeActors",
                    "Active users",
                    null,
                    "count",
                    "Unavailable",
                    "Actor-distinct measures require raw events and are not inferred from aggregate buckets."));
                Count("eventCount", "Recorded activity", eventCount);
                break;
        }

        return new AnalyticsDashboardDto(
            category,
            Scope(query),
            metrics,
            series,
            series.Count == 0
                ? [NoActivity(query)]
                : [],
            true,
            DateTimeOffset.UtcNow);
    }

    private async Task<IReadOnlyList<AnalyticsMetricDto>> BuildMetricsAsync(
        string category,
        IReadOnlyList<AnalyticsEventRecord> events,
        AnalyticsQuery query,
        AnalyticsFieldVisibility visibility,
        CancellationToken ct)
    {
        var terminal = events.Where(item => AnalyticsSemantics.IsExecutionTerminal(item.EventType)).ToList();
        var provider = events.Where(item => AnalyticsSemantics.IsProviderInvocation(item.EventType)).ToList();
        var evaluations = events.Where(item => item.EventType == "EvaluationCompleted").ToList();
        var policies = events.Where(item => AnalyticsSemantics.IsPolicyEvaluation(item.EventType)).ToList();
        var uniqueExecutions = terminal.Where(item => item.SourceExecutionId.HasValue)
            .Select(item => item.SourceExecutionId).Distinct().LongCount();
        var simulations = events.LongCount(item => AnalyticsSemantics.IsSimulationTerminal(item.EventType));
        var replays = events.LongCount(item => AnalyticsSemantics.IsReplayTerminal(item.EventType));
        var qualityPassed = events.LongCount(item => item.EventType == "QualityGatePassed");
        var qualityFailed = events.LongCount(item => item.EventType == "QualityGateFailed");
        var actualCost = provider.Where(item => item.CostType == "Actual").Sum(item => item.CostZar ?? 0);
        var estimatedCost = provider.Where(item => item.CostType == "Estimated").Sum(item => item.CostZar ?? 0);
        var unknownCost = provider.LongCount(item => item.CostType == "Unavailable");
        var actors = events.Where(item => item.ActorId.HasValue).Select(item => item.ActorId).Distinct().LongCount();
        var providerSucceeded = provider.LongCount(item => item.Outcome == "Succeeded");
        var providerFailed = provider.LongCount(item => item.Outcome == "Failed");
        var providerLatency = provider.Where(item => item.DurationMs.HasValue).Select(item => item.DurationMs!.Value).ToList();
        var metrics = new List<AnalyticsMetricDto>();

        void Count(string key, string label, decimal value) =>
            metrics.Add(new(key, label, value, "count"));
        void Percent(string key, string label, decimal? value) =>
            metrics.Add(new(key, label, value, "percent"));
        void Milliseconds(string key, string label, double? value) =>
            metrics.Add(new(key, label, value.HasValue ? (decimal)value.Value : null, "ms"));

        switch (category)
        {
            case "overview":
                Count("executionCount", "Executions", uniqueExecutions);
                Count("simulationCount", "Simulations", simulations);
                Count("evaluationCount", "Evaluations", evaluations.Count);
                Count("replayCount", "Replays", replays);
                Count("providerInvocationCount", "Provider invocations", provider.Count);
                Count("providerInvocationPreventedCount", "Provider calls prevented",
                    events.LongCount(item => item.EventType == "ProviderInvocationPrevented"));
                Percent("qualityPassRate", "Quality-gate pass rate",
                    Rate(qualityPassed, qualityPassed + qualityFailed));
                Percent("policyDenialRate", "Policy denial rate",
                    Rate(policies.LongCount(item => item.Outcome == "Denied"), policies.Count));
                Milliseconds("averageProviderLatency", "Average provider latency",
                    providerLatency.Count == 0 ? null : providerLatency.Average());
                Percent("providerSuccessRate", "Provider success rate",
                    Rate(providerSucceeded, providerSucceeded + providerFailed));
                Count("activeActors", "Active users", actors);
                AddCosts(metrics, actualCost, estimatedCost, unknownCost, visibility);
                break;

            case "usage":
                Count("eventCount", "Operational events", events.Count);
                Count("executionCount", "Executions", uniqueExecutions);
                Count("simulationCount", "Simulations", simulations);
                Count("evaluationCount", "Evaluations", evaluations.Count);
                Count("traceCount", "Traces", events.LongCount(item => AnalyticsSemantics.IsTraceTerminal(item.EventType)));
                Count("replayCount", "Replays", replays);
                Count("promptUsage", "Prompt usage", terminal.Count(item => !string.IsNullOrWhiteSpace(item.PromptName)));
                Count("workflowUsage", "Workflow usage", terminal.Count(item => !string.IsNullOrWhiteSpace(item.WorkflowName)));
                Count("knowledgeUsage", "Knowledge usage", terminal.Count(item => !string.IsNullOrWhiteSpace(item.KnowledgeCollectionName)));
                Count("pluginOperations", "Plugin operations", events.LongCount(item => AnalyticsSemantics.IsPluginOperation(item.EventType)));
                break;

            case "cost":
            case "budget":
                if (visibility.IncludeTokenUsage)
                {
                    Count("inputTokens", "Input tokens", provider.Sum(item => (long)(item.InputTokens ?? 0)));
                    Count("outputTokens", "Output tokens", provider.Sum(item => (long)(item.OutputTokens ?? 0)));
                    Count("totalTokens", "Total tokens", provider.Sum(item => (long)(item.InputTokens ?? 0) + (item.OutputTokens ?? 0)));
                }
                AddCosts(metrics, actualCost, estimatedCost, unknownCost, visibility);
                var budget = await BudgetAsync(query, ct);
                metrics.AddRange(budget);
                break;

            case "quality":
                Quality("averageGroundedness", "Average groundedness", evaluations.Select(item => item.Groundedness));
                Quality("averageRelevance", "Average relevance", evaluations.Select(item => item.Relevance));
                Quality("averageSafety", "Average safety", evaluations.Select(item => item.Safety));
                Quality("averageOverall", "Average overall score", evaluations.Select(item => item.OverallQuality));
                Percent("qualityPassRate", "Quality-gate pass rate", Rate(qualityPassed, qualityPassed + qualityFailed));
                Percent("qualityFailureRate", "Quality-gate failure rate", Rate(qualityFailed, qualityPassed + qualityFailed));
                Count("passedEvaluations", "Passed evaluations", qualityPassed);
                Count("failedEvaluations", "Failed evaluations", qualityFailed);
                break;

            case "governance":
                Count("policyEvaluations", "Policy evaluations", policies.Count);
                Count("policyAllowed", "Allowed decisions", policies.LongCount(item => item.Outcome == "Allowed"));
                Count("policyDenied", "Denied decisions", policies.LongCount(item => item.Outcome == "Denied"));
                Count("policyWarnings", "Warning decisions", policies.LongCount(item => item.Outcome == "Warning"));
                Count("providerInvocationPrevented", "Provider invocations prevented",
                    events.LongCount(item => item.EventType == "ProviderInvocationPrevented"));
                Count("sensitiveTraceReveals", "Sensitive trace reveals",
                    events.LongCount(item => item.EventType == "SensitiveTraceRevealed"));
                Count("configurationChanges", "Configuration changes",
                    events.LongCount(item => item.EventType is "ConfigurationChanged" or "ProductionConfigurationChanged"));
                Count("pluginActivationFailures", "Plugin activation failures",
                    events.LongCount(item => item.EventType == "PluginActivationFailed"));
                break;

            case "performance":
                Milliseconds("averageProviderLatency", "Average provider latency",
                    providerLatency.Count == 0 ? null : providerLatency.Average());
                Milliseconds("p50ProviderLatency", "P50 provider latency", Percentile(providerLatency, .50));
                Milliseconds("p95ProviderLatency", "P95 provider latency", Percentile(providerLatency, .95));
                Milliseconds("maximumProviderLatency", "Maximum provider latency",
                    providerLatency.Count == 0 ? null : providerLatency.Max());
                Milliseconds("averageSimulationDuration", "Average simulation duration",
                    AverageDuration(events, "SimulationCompleted"));
                Milliseconds("averageEvaluationDuration", "Average evaluation duration",
                    AverageDuration(events, "EvaluationCompleted"));
                Milliseconds("averageReplayDuration", "Average replay duration",
                    AverageDuration(events, "ReplayCompleted"));
                Percent("providerSuccessRate", "Provider success rate", Rate(providerSucceeded, provider.Count));
                Percent("providerFailureRate", "Provider failure rate", Rate(providerFailed, provider.Count));
                Count("timeouts", "Timeouts", events.LongCount(item => item.EventType == "ProviderInvocationTimedOut"));
                Count("retries", "Retries", events.LongCount(item => item.EventType == "ProviderInvocationRetried"));
                break;

            case "adoption":
                var now = query.To;
                Count("dailyActiveUsers", "Daily active users", ActiveActors(events, now.AddDays(-1), now));
                Count("weeklyActiveUsers", "Weekly active users", ActiveActors(events, now.AddDays(-7), now));
                Count("monthlyActiveUsers", "Monthly active users", ActiveActors(events, now.AddDays(-30), now));
                Count("activeAdministrators", "Administrator activity", ActiveActorsByRole(events, "Administrator"));
                Count("activeEngineers", "Engineer activity", ActiveActorsByRole(events, "Engineer"));
                Count("activeReviewers", "Reviewer activity", ActiveActorsByRole(events, "Reviewer"));
                Count("activeOperators", "Operator activity", ActiveActorsByRole(events, "Operator"));
                Count("approvalActivity", "Approval activity",
                    events.LongCount(item => item.EventType.Contains("Approved", StringComparison.OrdinalIgnoreCase)));
                Count("reviewActivity", "Review activity",
                    events.LongCount(item => item.EventType.Contains("Review", StringComparison.OrdinalIgnoreCase)));
                break;
        }

        return metrics;

        void Quality(string key, string label, IEnumerable<double?> values)
        {
            var selected = values.Where(value => value.HasValue).Select(value => value!.Value).ToList();
            metrics.Add(new(key, label,
                selected.Count == 0 ? null : (decimal)selected.Average(),
                "score"));
        }
    }

    private async Task<IReadOnlyList<AnalyticsMetricDto>> BudgetAsync(
        AnalyticsQuery query,
        CancellationToken ct)
    {
        var environmentId = query.EnvironmentId
            ?? await db.RuntimeEnvironments.AsNoTracking()
                .Where(item => item.WorkspaceId == query.WorkspaceId && item.IsDefault)
                .Select(item => (Guid?)item.Id)
                .SingleOrDefaultAsync(ct);
        if (!environmentId.HasValue)
            return [new("monthlyBudget", "Monthly budget", null, "ZAR", "Unavailable")];

        var organisationId = await db.Workspaces.AsNoTracking()
            .Where(item => item.Id == query.WorkspaceId)
            .Select(item => item.OrganisationId)
            .SingleAsync(ct);
        var settings = await effectiveConfiguration.ResolveAsync(
            organisationId, query.WorkspaceId, environmentId, ct);
        var monthStart = new DateTimeOffset(
            query.To.UtcDateTime.Year,
            query.To.UtcDateTime.Month,
            1,
            0,
            0,
            0,
            TimeSpan.Zero);
        var monthlyCostRows = await BuildQuery(query with
            {
                From = monthStart,
                To = query.To,
                EventType = null,
                CostType = null
            })
            .Where(item => item.EventType == "ProviderInvocationCompleted"
                || item.EventType == "ProviderInvocationFailed"
                || item.EventType == "ProviderInvocationTimedOut")
            .Select(item => new { item.CostZar, item.CostType })
            .ToListAsync(ct);
        var consumed = monthlyCostRows
            .Where(item => item.CostType is "Actual" or "Estimated")
            .Sum(item => item.CostZar ?? 0m);

        decimal Read(string key, decimal fallback)
        {
            var raw = settings.FirstOrDefault(item => item.Key == key)?.EffectiveValue?.Trim('"');
            return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                ? value : fallback;
        }

        var limit = Read(SettingKeys.MonthlyBudgetZar, 0);
        var warning = Read(SettingKeys.BudgetWarningThreshold, .8m);
        var hardStop = Read(SettingKeys.BudgetHardStopThreshold, 1m);
        var remaining = Math.Max(0, limit - consumed);
        var daysElapsed = Math.Max(1, query.To.UtcDateTime.Day);
        var daysInMonth = DateTime.DaysInMonth(query.To.UtcDateTime.Year, query.To.UtcDateTime.Month);
        var projected = consumed / daysElapsed * daysInMonth;
        return
        [
            new("monthlyBudget", "Monthly budget", limit, "ZAR"),
            new("budgetUsed", "Budget used", consumed, "ZAR"),
            new("budgetRemaining", "Budget remaining", remaining, "ZAR"),
            new("budgetWarningThreshold", "Warning threshold", warning * 100m, "percent"),
            new("budgetHardStopThreshold", "Hard-stop threshold", hardStop * 100m, "percent"),
            new("projectedMonthEndSpend", "Projected month-end spend", projected, "ZAR", "Estimated")
        ];
    }

    private static AnalyticsPointDto Point(
        DateTimeOffset bucket,
        IReadOnlyList<AnalyticsEventRecord> events,
        AnalyticsFieldVisibility visibility)
    {
        var terminal = events.Where(item => AnalyticsSemantics.IsExecutionTerminal(item.EventType)).ToList();
        var provider = events.Where(item => AnalyticsSemantics.IsProviderInvocation(item.EventType)).ToList();
        var durations = events.Where(item => item.DurationMs.HasValue).Select(item => item.DurationMs!.Value).ToList();
        var quality = events.Where(item => item.EventType == "EvaluationCompleted" && item.OverallQuality.HasValue)
            .Select(item => item.OverallQuality!.Value).ToList();
        return new AnalyticsPointDto(
            bucket,
            events.Count,
            terminal.Where(item => item.SourceExecutionId.HasValue).Select(item => item.SourceExecutionId).Distinct().LongCount(),
            events.LongCount(item => AnalyticsSemantics.IsSimulationTerminal(item.EventType)),
            events.LongCount(item => AnalyticsSemantics.IsEvaluationTerminal(item.EventType)),
            events.LongCount(item => AnalyticsSemantics.IsReplayTerminal(item.EventType)),
            provider.Count,
            events.LongCount(item => item.EventType == "ProviderInvocationPrevented"),
            events.LongCount(item => AnalyticsSemantics.IsPolicyEvaluation(item.EventType)),
            terminal.LongCount(item => item.Outcome == "Succeeded"),
            terminal.LongCount(item => item.Outcome == "Failed"),
            events.LongCount(item => item.EventType == "PolicyEvaluated" && item.Outcome == "Denied"),
            visibility.IncludeTokenUsage
                ? provider.Sum(item => (long)(item.InputTokens ?? 0))
                : null,
            visibility.IncludeTokenUsage
                ? provider.Sum(item => (long)(item.OutputTokens ?? 0))
                : null,
            visibility.IncludeCost
                ? provider.Where(item => item.CostType == "Actual").Sum(item => item.CostZar ?? 0)
                : null,
            visibility.IncludeCost
                ? provider.Where(item => item.CostType == "Estimated").Sum(item => item.CostZar ?? 0)
                : null,
            visibility.IncludeCost
                ? provider.LongCount(item => item.CostType == "Unavailable")
                : null,
            durations.Count == 0 ? 0 : durations.Average(),
            quality.Count == 0 ? null : quality.Average());
    }

    private static void AddCosts(
        ICollection<AnalyticsMetricDto> metrics,
        decimal actual,
        decimal estimated,
        long unknown,
        AnalyticsFieldVisibility visibility)
    {
        if (!visibility.IncludeCost) return;
        metrics.Add(new("actualCost", "Actual cost", actual, "ZAR", "Actual"));
        metrics.Add(new("estimatedCost", "Estimated cost", estimated, "ZAR", "Estimated"));
        metrics.Add(new("unknownCost", "Unknown-cost executions", unknown, "count", "Unavailable"));
    }

    private static IReadOnlyList<AnalyticsIndicatorDto> BuildIndicators(
        IReadOnlyList<AnalyticsEventRecord> events,
        AnalyticsQuery query)
    {
        var result = new List<AnalyticsIndicatorDto>();
        if (events.Count == 0 || events.Max(item => item.OccurredAt) < query.To.AddDays(-7))
            result.Add(NoActivity(query));

        var provider = events.Where(item => AnalyticsSemantics.IsProviderInvocation(item.EventType)).ToList();
        var providerFailureRate = Rate(
            provider.LongCount(item => item.Outcome == "Failed"), provider.Count);
        if (provider.Count >= 20 && providerFailureRate >= 20)
            result.Add(Indicator(
                "provider-failures", "Warning", "Provider failure rate increased",
                "Provider failures reached the deterministic 20% threshold.",
                "providerFailureRate >= 20%", 20, providerFailureRate, query, "providerFailureRate"));

        var latency = provider.Where(item => item.DurationMs.HasValue).Select(item => item.DurationMs!.Value).ToList();
        var p95 = Percentile(latency, .95);
        if (latency.Count >= 20 && p95 >= 3000)
            result.Add(Indicator(
                "provider-latency", "Warning", "Provider latency increased",
                "P95 provider latency exceeded three seconds.",
                "p95ProviderLatency >= 3000ms", 3000, (decimal?)p95, query, "p95ProviderLatency"));

        var policies = events.Where(item => item.EventType == "PolicyEvaluated").ToList();
        var denialRate = Rate(policies.LongCount(item => item.Outcome == "Denied"), policies.Count);
        if (policies.Count >= 20 && denialRate >= 15)
            result.Add(Indicator(
                "policy-denials", "Warning", "Policy denial rate increased",
                "Policy denials reached the deterministic 15% threshold.",
                "policyDenialRate >= 15%", 15, denialRate, query, "policyDenialRate"));

        var passed = events.LongCount(item => item.EventType == "QualityGatePassed");
        var failed = events.LongCount(item => item.EventType == "QualityGateFailed");
        var passRate = Rate(passed, passed + failed);
        if (passed + failed >= 20 && passRate < 80)
            result.Add(Indicator(
                "quality-pass-rate", "Warning", "Quality pass rate decreased",
                "Quality-gate pass rate fell below 80%.",
                "qualityPassRate < 80%", 80, passRate, query, "qualityPassRate"));

        if (events.Count(item => item.EventType is "PluginActivationFailed" or "PluginCompatibilityFailed") >= 3)
            result.Add(Indicator(
                "plugin-health", "Warning", "Plugin health failures increased",
                "At least three plugin activation or compatibility failures were recorded.",
                "pluginFailures >= 3", 3,
                events.Count(item => item.EventType is "PluginActivationFailed" or "PluginCompatibilityFailed"),
                query, "pluginFailures"));

        return result;
    }

    private static AnalyticsIndicatorDto NoActivity(AnalyticsQuery query) =>
        Indicator(
            "no-activity", "Info", "Expected workspace activity missing",
            "No governed runtime activity was recorded during the selected period.",
            "eventCount = 0 or lastActivity < to-7d", 1, 0, query, "eventCount");

    private static AnalyticsIndicatorDto Indicator(
        string key,
        string severity,
        string title,
        string detail,
        string rule,
        decimal? threshold,
        decimal? observed,
        AnalyticsQuery query,
        string sourceMetric) =>
        new(key, severity, title, detail, query.To, rule, threshold, observed,
            query.From, query.To, query.EnvironmentId, sourceMetric);

    private static AnalyticsEventDto Map(
        AnalyticsEventRecord item,
        AnalyticsFieldVisibility visibility) => new(
        item.Id,
        item.OrganisationId,
        item.WorkspaceId,
        item.EnvironmentId,
        visibility.IncludeActor ? item.ActorId : null,
        visibility.IncludeActor ? item.ActorType : "Redacted",
        visibility.IncludeActor ? item.ActorRole : null,
        item.Capability,
        item.EventType,
        item.Outcome,
        visibility.IncludeProviderDetails ? item.Provider : null,
        visibility.IncludeProviderDetails ? item.Model : null,
        visibility.IncludeTokenUsage ? item.InputTokens : null,
        visibility.IncludeTokenUsage ? item.OutputTokens : null,
        visibility.IncludeCost ? item.CostZar : null,
        visibility.IncludeCost ? item.CostType : "Restricted",
        visibility.IncludeCost ? item.PricingRevision : null,
        item.DurationMs,
        item.QualityScore,
        item.Groundedness,
        item.Relevance,
        item.Safety,
        item.OverallQuality,
        item.ProviderInvocationPrevented,
        item.SourceExecutionId,
        item.SourceType,
        visibility.IncludeSensitiveSource ? item.SourceId : null,
        visibility.IncludeSensitiveSource ? item.PromptName : null,
        visibility.IncludeSensitiveSource ? item.WorkflowName : null,
        visibility.IncludeSensitiveSource ? item.KnowledgeCollectionName : null,
        item.PolicyOutcome,
        item.EvaluationOutcome,
        item.ConfigurationRevision,
        item.CorrelationId,
        item.OccurredAt);

    private static AnalyticsExportDto Map(AnalyticsExportRecord item) => new(
        item.Id, item.Status, item.FileName, item.RowCount, item.SizeBytes, item.Checksum,
        item.FailureReason, item.CreatedAt, item.ExpiresAt, item.CompletedAt);

    private async Task<AnalyticsExportRecord> FindExportAsync(
        Guid workspaceId,
        Guid exportId,
        CancellationToken ct) =>
        await db.AnalyticsExports.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == exportId && item.WorkspaceId == workspaceId, ct)
        ?? throw new ResourceNotFoundException(
            "analytics.export_not_found",
            $"Analytics export '{exportId}' was not found.");

    private async Task RequireEnvironmentAsync(
        Guid workspaceId,
        Guid? environmentId,
        CancellationToken ct)
    {
        if (!await db.Workspaces.AsNoTracking().AnyAsync(
                item => item.Id == workspaceId,
                ct))
            throw new ResourceNotFoundException(
                "workspace.not_found",
                $"Workspace '{workspaceId}' was not found.");
        if (!environmentId.HasValue) return;
        if (!await db.RuntimeEnvironments.AsNoTracking().AnyAsync(
                item => item.Id == environmentId && item.WorkspaceId == workspaceId, ct))
            throw new ResourceNotFoundException(
                "environment.not_found",
                $"Environment '{environmentId}' was not found.");
    }

    private async Task<int> ExportRetentionDaysAsync(
        Guid workspaceId,
        Guid? environmentId,
        CancellationToken ct)
    {
        var organisationId = await db.Workspaces.AsNoTracking()
            .Where(item => item.Id == workspaceId)
            .Select(item => item.OrganisationId)
            .SingleAsync(ct);
        var setting = await effectiveConfiguration.ResolveOneAsync(
            organisationId, workspaceId, environmentId,
            SettingKeys.AnalyticsExportRetentionDays, ct);
        return int.TryParse(setting?.EffectiveValue?.Trim('"'), out var days)
            ? Math.Clamp(days, 1, 30)
            : 7;
    }

    private static AnalyticsQuery ToQuery(Guid workspaceId, CreateAnalyticsExportRequest request) =>
        new(
            workspaceId, request.EnvironmentId, request.From, request.To,
            Provider: request.Provider, Model: request.Model, Capability: request.Capability,
            Outcome: request.Outcome, ConfigurationRevision: request.ConfigurationRevision,
            Prompt: request.Prompt, Workflow: request.Workflow,
            KnowledgeCollection: request.KnowledgeCollection, ActorId: request.ActorId,
            EventType: request.EventType, CostType: request.CostType);

    private static AnalyticsScopeDto Scope(AnalyticsQuery query) => new(
        query.WorkspaceId, query.EnvironmentId, query.From, query.To, query.Granularity,
        new Dictionary<string, string?>
        {
            ["provider"] = query.Provider,
            ["model"] = query.Model,
            ["capability"] = query.Capability,
            ["outcome"] = query.Outcome,
            ["configurationRevision"] = query.ConfigurationRevision,
            ["prompt"] = query.Prompt,
            ["workflow"] = query.Workflow,
            ["knowledgeCollection"] = query.KnowledgeCollection,
            ["actorId"] = query.ActorId?.ToString(),
            ["eventType"] = query.EventType,
            ["costType"] = query.CostType
        });

    private static void ValidateCategory(string category)
    {
        if (category is not ("overview" or "usage" or "cost" or "budget"
            or "quality" or "governance" or "performance" or "adoption"))
            throw new RequestValidationException(
                "analytics.category_invalid",
                $"Analytics category '{category}' is not supported.");
    }

    private static void ValidateQuery(AnalyticsQuery query, int maxDays)
    {
        if (query.From >= query.To)
            throw new RequestValidationException(
                "analytics.period_invalid",
                "The analytics period must have a start before its end.");
        if (query.To - query.From > TimeSpan.FromDays(maxDays))
            throw new RequestValidationException(
                "analytics.period_too_large",
                $"This analytics query supports at most {maxDays} days.");
        if (query.To > DateTimeOffset.UtcNow.AddMinutes(5))
            throw new RequestValidationException(
                "analytics.period_invalid",
                "The analytics period cannot extend into the future.");
        if (query.Granularity is not ("day" or "hour"))
            throw new RequestValidationException(
                "analytics.granularity_invalid",
                "Granularity must be 'day' or 'hour'.");
    }

    private static DateTimeOffset Bucket(DateTimeOffset value, string granularity)
    {
        var utc = value.UtcDateTime;
        return granularity == "hour"
            ? new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero)
            : new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero);
    }

    private static decimal? Rate(long numerator, long denominator) =>
        denominator == 0 ? null : numerator * 100m / denominator;

    private static double? Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0) return null;
        var ordered = values.OrderBy(value => value).ToList();
        var rank = (ordered.Count - 1) * percentile;
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        return lower == upper
            ? ordered[lower]
            : ordered[lower] + (ordered[upper] - ordered[lower]) * (rank - lower);
    }

    private static double? AverageDuration(
        IEnumerable<AnalyticsEventRecord> events,
        string eventType)
    {
        var values = events.Where(item => item.EventType == eventType && item.DurationMs.HasValue)
            .Select(item => item.DurationMs!.Value).ToList();
        return values.Count == 0 ? null : values.Average();
    }

    private static long ActiveActors(
        IEnumerable<AnalyticsEventRecord> events,
        DateTimeOffset from,
        DateTimeOffset to) =>
        events.Where(item => item.ActorId.HasValue && item.OccurredAt >= from && item.OccurredAt < to)
            .Select(item => item.ActorId).Distinct().LongCount();

    private static long ActiveActorsByRole(
        IEnumerable<AnalyticsEventRecord> events,
        string role) =>
        events.Where(item => item.ActorId.HasValue
                && string.Equals(item.ActorRole, role, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.ActorId).Distinct().LongCount();

    private static string EncodeCursor(AnalyticsEventRecord item) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{item.OccurredAt.UtcTicks.ToString(CultureInfo.InvariantCulture)}|{item.Id:N}"));

    private static bool TryDecodeCursor(
        string? cursor,
        out DateTimeOffset occurredAt,
        out Guid id)
    {
        occurredAt = default;
        id = default;
        if (string.IsNullOrWhiteSpace(cursor)) return false;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|');
            if (parts.Length != 2
                || !long.TryParse(parts[0], CultureInfo.InvariantCulture, out var ticks)
                || !Guid.TryParseExact(parts[1], "N", out id))
                throw new FormatException();
            occurredAt = new DateTimeOffset(ticks, TimeSpan.Zero);
            return true;
        }
        catch (FormatException)
        {
            throw new RequestValidationException(
                "analytics.cursor_invalid",
                "The event cursor is invalid.");
        }
    }
}
