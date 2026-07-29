using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConvoLab.Application.Analytics;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.Analytics;

public sealed class AnalyticsService(ApplicationDbContext db) : IAnalyticsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AnalyticsFilterOptionsDto> FilterOptionsAsync(AnalyticsQuery query, CancellationToken ct = default)
    {
        ValidateQuery(query, 366);
        await RequireEnvironmentAsync(query.WorkspaceId, query.EnvironmentId, ct);
        var eventQuery = db.AnalyticsEvents.AsNoTracking()
            .Where(item => item.WorkspaceId == query.WorkspaceId);
        if (query.EnvironmentId.HasValue)
            eventQuery = eventQuery.Where(item => item.EnvironmentId == query.EnvironmentId.Value);
        if (!db.Database.IsSqlite())
            eventQuery = eventQuery.Where(item => item.OccurredAt >= query.From && item.OccurredAt < query.To);

        var rows = await eventQuery
            .Select(item => new
            {
                item.OccurredAt,
                item.Provider,
                item.Model,
                item.Capability,
                item.Outcome,
                item.PromptName,
                item.WorkflowName,
                item.ConfigurationRevision,
                item.EventType
            })
            .ToListAsync(ct);
        if (db.Database.IsSqlite())
            rows = rows.Where(item => item.OccurredAt >= query.From && item.OccurredAt < query.To).ToList();

        static IReadOnlyList<string> Options(IEnumerable<string?> values, int maximum = 200) =>
            values.Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .Take(maximum)
                .ToList();

        return new AnalyticsFilterOptionsDto(
            Options(rows.Select(item => item.Provider)),
            Options(rows.Select(item => item.Model)),
            Options(rows.Select(item => item.Capability)),
            Options(rows.Select(item => item.Outcome)),
            Options(rows.Select(item => item.PromptName)),
            Options(rows.Select(item => item.WorkflowName)),
            Options(rows.Select(item => item.ConfigurationRevision), 100),
            Options(rows.Select(item => item.EventType)));
    }

    public async Task<AnalyticsDashboardDto> DashboardAsync(string category, AnalyticsQuery query, CancellationToken ct = default)
    {
        ValidateQuery(query, query.Granularity.Equals("hour", StringComparison.OrdinalIgnoreCase) ? 31 : 366);
        if (query.Granularity == "day"
            && query.From < DateTimeOffset.UtcNow.AddDays(-90)
            && query.ConfigurationRevision is null && query.Prompt is null && query.Workflow is null)
            return await AggregateDashboardAsync(category, query, ct);
        var events = await LoadAsync(query, ct);
        var bucketed = events
            .GroupBy(item => Bucket(item.OccurredAt, query.Granularity))
            .OrderBy(group => group.Key)
            .Select(group => new AnalyticsPointDto(
                group.Key,
                group.LongCount(),
                group.LongCount(item => item.Outcome == "Succeeded" || item.Outcome == "Completed"),
                group.LongCount(item => item.Outcome == "Failed"),
                group.LongCount(item => item.Outcome == "Denied"),
                group.Sum(item => (long)(item.InputTokens ?? 0)),
                group.Sum(item => (long)(item.OutputTokens ?? 0)),
                group.Where(item => item.CostType == "Actual").Sum(item => item.CostZar ?? 0),
                group.Where(item => item.CostType == "Estimated").Sum(item => item.CostZar ?? 0),
                group.LongCount(item => item.CostType == "Unavailable"),
                group.Where(item => item.DurationMs.HasValue).Select(item => item.DurationMs!.Value).DefaultIfEmpty().Average(),
                group.Any(item => item.QualityScore.HasValue)
                    ? group.Where(item => item.QualityScore.HasValue).Average(item => item.QualityScore)
                    : null))
            .ToList();

        var total = events.Count;
        var actual = events.Where(item => item.CostType == "Actual").Sum(item => item.CostZar ?? 0);
        var estimated = events.Where(item => item.CostType == "Estimated").Sum(item => item.CostZar ?? 0);
        var unknown = events.LongCount(item => item.CostType == "Unavailable");
        var actors = events.Where(item => item.ActorId.HasValue).Select(item => item.ActorId).Distinct().LongCount();
        var metrics = new List<AnalyticsMetricDto>
        {
            new("executions", "Executions", total, "count"),
            new("successRate", "Success rate", total == 0 ? null : events.Count(item => item.Outcome is "Succeeded" or "Completed") * 100m / total, "percent"),
            new("tokens", "Tokens", events.Sum(item => (long)(item.InputTokens ?? 0) + (item.OutputTokens ?? 0)), "tokens"),
            new("actualCost", "Actual cost", actual, "ZAR", "Actual"),
            new("estimatedCost", "Estimated cost", estimated, "ZAR", "Estimated"),
            new("unknownCost", "Unavailable cost", unknown, "count", "Unavailable"),
            new("actors", "Active actors", actors, "count")
        };
        var indicators = BuildIndicators(events, query.To);
        return new AnalyticsDashboardDto(
            category,
            Scope(query),
            metrics,
            bucketed,
            indicators,
            false,
            DateTimeOffset.UtcNow);
    }

    private async Task<AnalyticsDashboardDto> AggregateDashboardAsync(string category, AnalyticsQuery query, CancellationToken ct)
    {
        await RequireEnvironmentAsync(query.WorkspaceId, query.EnvironmentId, ct);
        var rows = (await db.AnalyticsDailyAggregates.AsNoTracking()
                .Where(item => item.WorkspaceId == query.WorkspaceId).ToListAsync(ct))
            .Where(item => (!query.EnvironmentId.HasValue || item.EnvironmentId == query.EnvironmentId)
                && item.BucketStart >= query.From && item.BucketStart < query.To
                && (query.Provider == null || item.Provider == query.Provider)
                && (query.Model == null || item.Model == query.Model)
                && (query.Capability == null || item.Capability == query.Capability)
                && (query.Outcome == null || item.Outcome == query.Outcome))
            .ToList();
        var series = rows.GroupBy(item => item.BucketStart).OrderBy(group => group.Key)
            .Select(group => new AnalyticsPointDto(
                group.Key, group.Sum(item => item.Executions), group.Sum(item => item.Succeeded),
                group.Sum(item => item.Failed), group.Sum(item => item.Denied),
                group.Sum(item => item.InputTokens), group.Sum(item => item.OutputTokens),
                group.Sum(item => item.ActualCostZar), group.Sum(item => item.EstimatedCostZar),
                group.Sum(item => item.UnknownCostCount),
                group.Sum(item => item.Executions) == 0 ? 0 : group.Sum(item => item.TotalDurationMs) / group.Sum(item => item.Executions),
                group.Sum(item => item.QualityCount) == 0 ? null : group.Sum(item => item.TotalQualityScore) / group.Sum(item => item.QualityCount)))
            .ToList();
        var executions = rows.Sum(item => item.Executions);
        var metrics = new List<AnalyticsMetricDto>
        {
            new("executions", "Executions", executions, "count"),
            new("successRate", "Success rate", executions == 0 ? null : rows.Sum(item => item.Succeeded) * 100m / executions, "percent"),
            new("tokens", "Tokens", rows.Sum(item => item.InputTokens + item.OutputTokens), "tokens"),
            new("actualCost", "Actual cost", rows.Sum(item => item.ActualCostZar), "ZAR", "Actual"),
            new("estimatedCost", "Estimated cost", rows.Sum(item => item.EstimatedCostZar), "ZAR", "Estimated"),
            new("unknownCost", "Unavailable cost", rows.Sum(item => item.UnknownCostCount), "count", "Unavailable"),
            new("actors", "Active actors", null, "count", "Unavailable", "Actor cardinality is available only while raw events are retained.")
        };
        return new AnalyticsDashboardDto(
            category, Scope(query), metrics, series,
            series.Count == 0
                ? [new AnalyticsIndicatorDto("no-activity", "Info", "No activity", "No retained aggregate activity exists for this period.", query.To)]
                : [],
            true, DateTimeOffset.UtcNow);
    }

    public async Task<AnalyticsEventPageDto> EventsAsync(
        AnalyticsQuery query,
        int take,
        string? cursor,
        bool includeActor,
        CancellationToken ct = default)
    {
        ValidateQuery(query, 31);
        take = Math.Clamp(take, 1, 200);
        var events = await LoadAsync(query, ct);
        if (TryDecodeCursor(cursor, out var occurredAt, out var id))
            events = events.Where(item => item.OccurredAt < occurredAt || item.OccurredAt == occurredAt && item.Id.CompareTo(id) < 0).ToList();
        var page = events.OrderByDescending(item => item.OccurredAt).ThenByDescending(item => item.Id).Take(take + 1).ToList();
        var hasMore = page.Count > take;
        if (hasMore) page.RemoveAt(page.Count - 1);
        var next = hasMore && page.Count > 0 ? EncodeCursor(page[^1]) : null;
        return new AnalyticsEventPageDto(Scope(query), page.Select(item => Map(item, includeActor)).ToList(), next);
    }

    public async Task<AnalyticsEventDto> EventAsync(Guid workspaceId, Guid eventId, bool includeActor, CancellationToken ct = default)
    {
        var item = await db.AnalyticsEvents.AsNoTracking()
            .SingleOrDefaultAsync(value => value.WorkspaceId == workspaceId && value.Id == eventId, ct)
            ?? throw new ResourceNotFoundException("analytics.event_not_found", $"Analytics event '{eventId}' was not found.");
        return Map(item, includeActor);
    }

    public async Task<IReadOnlyList<AnalyticsEventDto>> CorrelationAsync(
        Guid workspaceId,
        string correlationId,
        bool includeActor,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 100)
            throw new RequestValidationException("analytics.correlation_invalid", "A valid correlation id is required.");
        return (await db.AnalyticsEvents.AsNoTracking()
                .Where(item => item.WorkspaceId == workspaceId && item.CorrelationId == correlationId)
                .ToListAsync(ct))
            .OrderBy(item => item.OccurredAt)
            .Select(item => Map(item, includeActor))
            .ToList();
    }

    public async Task<AnalyticsExportDto> CreateExportAsync(
        Guid workspaceId,
        Guid actorId,
        CreateAnalyticsExportRequest request,
        CancellationToken ct = default)
    {
        var query = new AnalyticsQuery(workspaceId, request.EnvironmentId, request.From, request.To,
            Provider: request.Provider, Model: request.Model, Capability: request.Capability, Outcome: request.Outcome);
        ValidateQuery(query, 90);
        await RequireEnvironmentAsync(workspaceId, request.EnvironmentId, ct);
        var now = DateTimeOffset.UtcNow;
        var record = new AnalyticsExportRecord
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            CreatedBy = actorId,
            Status = "Pending",
            FileName = $"convolab-analytics-{now:yyyyMMdd-HHmmss}.csv",
            FiltersJson = JsonSerializer.Serialize(request, JsonOptions),
            CreatedAt = now,
            ExpiresAt = now.AddDays(7)
        };
        db.AnalyticsExports.Add(record);
        await db.SaveChangesAsync(ct);
        return Map(record);
    }

    public async Task<IReadOnlyList<AnalyticsExportDto>> ExportsAsync(Guid workspaceId, CancellationToken ct = default) =>
        (await db.AnalyticsExports.AsNoTracking().Where(item => item.WorkspaceId == workspaceId).ToListAsync(ct))
        .OrderByDescending(item => item.CreatedAt).Select(Map).ToList();

    public async Task<AnalyticsExportDto> ExportAsync(Guid workspaceId, Guid exportId, CancellationToken ct = default) =>
        Map(await FindExportAsync(workspaceId, exportId, ct));

    public async Task<(byte[] Content, string FileName)> DownloadExportAsync(Guid workspaceId, Guid exportId, CancellationToken ct = default)
    {
        var export = await FindExportAsync(workspaceId, exportId, ct);
        if (export.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ResourceConflictException("analytics.export_expired", "The analytics export has expired.");
        if (export.Status != "Completed" || export.Content is null)
            throw new ResourceConflictException("analytics.export_not_ready", "The analytics export is not ready for download.");
        return (export.Content, export.FileName);
    }

    private async Task<List<AnalyticsEventRecord>> LoadAsync(AnalyticsQuery query, CancellationToken ct)
    {
        await RequireEnvironmentAsync(query.WorkspaceId, query.EnvironmentId, ct);
        var items = await db.AnalyticsEvents.AsNoTracking()
            .Where(item => item.WorkspaceId == query.WorkspaceId)
            .ToListAsync(ct);
        return items.Where(item =>
                (!query.EnvironmentId.HasValue || item.EnvironmentId == query.EnvironmentId)
                && item.OccurredAt >= query.From && item.OccurredAt < query.To
                && (query.Provider == null || item.Provider == query.Provider)
                && (query.Model == null || item.Model == query.Model)
                && (query.Capability == null || item.Capability == query.Capability)
                && (query.Outcome == null || item.Outcome == query.Outcome)
                && (query.ConfigurationRevision == null || item.ConfigurationRevision == query.ConfigurationRevision)
                && (query.Prompt == null || item.PromptName == query.Prompt)
                && (query.Workflow == null || item.WorkflowName == query.Workflow))
            .ToList();
    }

    private async Task RequireEnvironmentAsync(Guid workspaceId, Guid? environmentId, CancellationToken ct)
    {
        if (!environmentId.HasValue) return;
        if (!await db.RuntimeEnvironments.AsNoTracking().AnyAsync(item => item.Id == environmentId && item.WorkspaceId == workspaceId, ct))
            throw new ResourceNotFoundException("environment.not_found", $"Environment '{environmentId}' was not found.");
    }

    private async Task<AnalyticsExportRecord> FindExportAsync(Guid workspaceId, Guid exportId, CancellationToken ct) =>
        await db.AnalyticsExports.AsNoTracking().SingleOrDefaultAsync(item => item.Id == exportId && item.WorkspaceId == workspaceId, ct)
        ?? throw new ResourceNotFoundException("analytics.export_not_found", $"Analytics export '{exportId}' was not found.");

    private static void ValidateQuery(AnalyticsQuery query, int maxDays)
    {
        if (query.From >= query.To)
            throw new RequestValidationException("analytics.period_invalid", "The analytics period must have a start before its end.");
        if (query.To - query.From > TimeSpan.FromDays(maxDays))
            throw new RequestValidationException("analytics.period_too_large", $"This analytics query supports at most {maxDays} days.");
        if (query.To > DateTimeOffset.UtcNow.AddMinutes(5))
            throw new RequestValidationException("analytics.period_invalid", "The analytics period cannot extend into the future.");
        if (query.Granularity is not ("day" or "hour"))
            throw new RequestValidationException("analytics.granularity_invalid", "Granularity must be 'day' or 'hour'.");
    }

    private static AnalyticsScopeDto Scope(AnalyticsQuery query) => new(
        query.WorkspaceId, query.EnvironmentId, query.From, query.To, query.Granularity,
        new Dictionary<string, string?>
        {
            ["provider"] = query.Provider, ["model"] = query.Model, ["capability"] = query.Capability,
            ["outcome"] = query.Outcome, ["configurationRevision"] = query.ConfigurationRevision,
            ["prompt"] = query.Prompt, ["workflow"] = query.Workflow
        });

    private static DateTimeOffset Bucket(DateTimeOffset value, string granularity)
    {
        var utc = value.UtcDateTime;
        return granularity == "hour"
            ? new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero)
            : new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero);
    }

    private static IReadOnlyList<AnalyticsIndicatorDto> BuildIndicators(IReadOnlyList<AnalyticsEventRecord> events, DateTimeOffset to)
    {
        var result = new List<AnalyticsIndicatorDto>();
        if (events.Count == 0 || events.Max(item => item.OccurredAt) < to.AddDays(-7))
            result.Add(new("no-activity", "Info", "No recent activity", "No runtime activity was recorded during the last seven days.", to));
        var providerEvents = events.Where(item => item.EventType.Contains("Execution", StringComparison.OrdinalIgnoreCase)).ToList();
        if (providerEvents.Count >= 20 && providerEvents.Count(item => item.Outcome == "Failed") >= providerEvents.Count * .2)
            result.Add(new("provider-failures", "Warning", "Provider failures increased", "At least 20% of provider executions failed in the selected period.", to));
        var policy = events.Where(item => item.Outcome == "Denied").ToList();
        if (events.Count >= 20 && policy.Count >= events.Count * .15)
            result.Add(new("policy-denials", "Warning", "Policy denials elevated", "At least 15% of recorded activity was denied.", to));
        return result;
    }

    private static AnalyticsEventDto Map(AnalyticsEventRecord item, bool includeActor) => new(
        item.Id, item.WorkspaceId, item.EnvironmentId, includeActor ? item.ActorId : null,
        item.ActorType, includeActor ? item.ActorRole : null, item.Capability, item.EventType, item.Outcome,
        item.Provider, item.Model, item.InputTokens, item.OutputTokens, item.CostZar, item.CostType,
        item.DurationMs, item.QualityScore, item.ProviderInvocationPrevented, item.SourceType, item.SourceId,
        item.ConfigurationRevision, item.CorrelationId, item.OccurredAt);

    private static AnalyticsExportDto Map(AnalyticsExportRecord item) => new(
        item.Id, item.Status, item.FileName, item.RowCount, item.SizeBytes, item.Checksum,
        item.FailureReason, item.CreatedAt, item.ExpiresAt, item.CompletedAt);

    private static string EncodeCursor(AnalyticsEventRecord item) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{item.OccurredAt.UtcTicks.ToString(CultureInfo.InvariantCulture)}|{item.Id:N}"));

    private static bool TryDecodeCursor(string? cursor, out DateTimeOffset occurredAt, out Guid id)
    {
        occurredAt = default; id = default;
        if (string.IsNullOrWhiteSpace(cursor)) return false;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|');
            if (parts.Length != 2 || !long.TryParse(parts[0], CultureInfo.InvariantCulture, out var ticks)
                || !Guid.TryParseExact(parts[1], "N", out id))
                throw new FormatException();
            occurredAt = new DateTimeOffset(ticks, TimeSpan.Zero);
            return true;
        }
        catch (FormatException)
        {
            throw new RequestValidationException("analytics.cursor_invalid", "The event cursor is invalid.");
        }
    }
}
