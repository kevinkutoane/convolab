using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConvoLab.Application.Analytics;
using ConvoLab.Domain.Analytics;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConvoLab.Infrastructure.Analytics;

public sealed class AnalyticsMaintenanceWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AnalyticsMaintenanceWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await DispatchOutboxAsync(db, stoppingToken);
                await BuildExportsAsync(db, stoppingToken);
                await RebuildAggregatesAsync(db, stoppingToken);
                await ApplyRetentionAsync(db, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Analytics maintenance iteration failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    private static async Task DispatchOutboxAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var pending = db.Database.IsNpgsql()
            ? await db.AnalyticsOutbox.FromSqlInterpolated($"""
                SELECT * FROM "AnalyticsOutbox"
                WHERE "Status" = 'Pending' AND "AvailableAt" <= {now}
                ORDER BY "CreatedAt"
                FOR UPDATE SKIP LOCKED
                LIMIT 100
                """).ToListAsync(ct)
            : (await db.AnalyticsOutbox
                    .Where(item => item.Status == "Pending")
                    .ToListAsync(ct))
                .Where(item => item.AvailableAt <= now)
                .OrderBy(item => item.CreatedAt)
                .Take(100)
                .ToList();
        foreach (var item in pending)
        {
            try
            {
                var analyticsEvent = JsonSerializer.Deserialize<AnalyticsEventRecord>(item.PayloadJson, JsonOptions)
                    ?? throw new InvalidOperationException("Analytics outbox payload is invalid.");
                if (!await db.AnalyticsEvents.AnyAsync(value => value.EventKey == item.EventKey, ct))
                {
                    db.AnalyticsEvents.Add(analyticsEvent);
                    var checkpoints = await db.AnalyticsAggregationCheckpoints
                        .Where(value => value.WorkspaceId == analyticsEvent.WorkspaceId).ToListAsync(ct);
                    foreach (var granularity in new[] { "hour", "day" })
                    {
                        var checkpoint = checkpoints.SingleOrDefault(value =>
                            value.Granularity == granularity);
                        if (checkpoint is null)
                        {
                            checkpoint = new AnalyticsAggregationCheckpointRecord
                            {
                                Id = Guid.NewGuid(),
                                WorkspaceId = analyticsEvent.WorkspaceId,
                                Granularity = granularity,
                                Status = "Pending"
                            };
                            db.AnalyticsAggregationCheckpoints.Add(checkpoint);
                            checkpoints.Add(checkpoint);
                        }

                        checkpoint.DirtyFromUtc = !checkpoint.DirtyFromUtc.HasValue
                            || analyticsEvent.OccurredAt < checkpoint.DirtyFromUtc
                            ? analyticsEvent.OccurredAt
                            : checkpoint.DirtyFromUtc;
                        checkpoint.DirtyToUtc = !checkpoint.DirtyToUtc.HasValue
                            || analyticsEvent.OccurredAt > checkpoint.DirtyToUtc
                            ? analyticsEvent.OccurredAt
                            : checkpoint.DirtyToUtc;
                        checkpoint.LastProcessedEventId = analyticsEvent.Id;
                        checkpoint.Status = "Pending";
                        checkpoint.FailureReason = null;
                        checkpoint.UpdatedAt = now;
                    }
                }
                item.Status = "Processed";
                item.ProcessedAt = now;
                item.LastError = null;
            }
            catch (Exception exception)
            {
                item.Attempts++;
                item.Status = item.Attempts >= 10 ? "Failed" : "Pending";
                item.LastError = exception.Message[..Math.Min(exception.Message.Length, 2000)];
                item.AvailableAt = now.AddSeconds(Math.Min(300, Math.Pow(2, item.Attempts)));
            }
        }
        if (pending.Count > 0) await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static async Task BuildExportsAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var exports = await db.AnalyticsExports.Where(item => item.Status == "Pending")
            .OrderBy(item => item.CreatedAt).Take(5).ToListAsync(ct);
        foreach (var export in exports)
        {
            try
            {
                var request = JsonSerializer.Deserialize<CreateAnalyticsExportRequest>(export.FiltersJson, JsonOptions)
                    ?? throw new InvalidOperationException("Export filters are invalid.");
                var query = db.AnalyticsEvents.AsNoTracking()
                    .Where(item => item.WorkspaceId == export.WorkspaceId
                        && item.OccurredAt >= request.From
                        && item.OccurredAt < request.To);
                if (request.EnvironmentId.HasValue)
                    query = query.Where(item => item.EnvironmentId == request.EnvironmentId);
                if (request.Provider is not null) query = query.Where(item => item.Provider == request.Provider);
                if (request.Model is not null) query = query.Where(item => item.Model == request.Model);
                if (request.Capability is not null) query = query.Where(item => item.Capability == request.Capability);
                if (request.Outcome is not null) query = query.Where(item => item.Outcome == request.Outcome);
                if (request.ConfigurationRevision is not null)
                    query = query.Where(item => item.ConfigurationRevision == request.ConfigurationRevision);
                if (request.Prompt is not null) query = query.Where(item => item.PromptName == request.Prompt);
                if (request.Workflow is not null) query = query.Where(item => item.WorkflowName == request.Workflow);
                if (request.KnowledgeCollection is not null)
                    query = query.Where(item => item.KnowledgeCollectionName == request.KnowledgeCollection);
                if (request.ActorId.HasValue) query = query.Where(item => item.ActorId == request.ActorId);
                if (request.EventType is not null) query = query.Where(item => item.EventType == request.EventType);
                if (request.CostType is not null) query = query.Where(item => item.CostType == request.CostType);

                var events = await query.OrderBy(item => item.OccurredAt)
                    .ThenBy(item => item.Id)
                    .Take(100_001)
                    .ToListAsync(ct);
                if (events.Count > 100_000) throw new InvalidOperationException("Export exceeds the 100,000 row limit.");
                var columns = new List<string>
                {
                    "occurredAt", "environmentId", "capability", "eventType", "outcome"
                };
                if (export.IncludeActor) columns.AddRange(["actorId", "actorType", "actorRole"]);
                if (export.IncludeProviderDetails) columns.AddRange(["provider", "model"]);
                if (export.IncludeTokenUsage) columns.AddRange(["inputTokens", "outputTokens"]);
                if (export.IncludeCost) columns.AddRange(["costZar", "costType", "pricingRevision"]);
                columns.AddRange([
                    "durationMs", "qualityScore", "groundedness", "relevance", "safety",
                    "overallQuality", "providerInvocationPrevented", "sourceExecutionId",
                    "sourceType", "sourceId"
                ]);
                if (export.IncludeSensitiveSource)
                    columns.AddRange(["prompt", "workflow", "knowledgeCollection"]);
                columns.AddRange([
                    "policyOutcome", "evaluationOutcome", "configurationRevision", "correlationId"
                ]);

                var csv = new StringBuilder();
                csv.AppendLine(string.Join(',', columns.Select(Cell)));
                foreach (var item in events)
                {
                    var values = new List<string?>
                    {
                        item.OccurredAt.ToString("O"), item.EnvironmentId.ToString(), item.Capability,
                        item.EventType, item.Outcome
                    };
                    if (export.IncludeActor)
                        values.AddRange([item.ActorId?.ToString(), item.ActorType, item.ActorRole]);
                    if (export.IncludeProviderDetails) values.AddRange([item.Provider, item.Model]);
                    if (export.IncludeTokenUsage)
                        values.AddRange([item.InputTokens?.ToString(), item.OutputTokens?.ToString()]);
                    if (export.IncludeCost)
                        values.AddRange([
                            Invariant(item.CostZar), item.CostType, item.PricingRevision
                        ]);
                    values.AddRange([
                        Invariant(item.DurationMs), Invariant(item.QualityScore),
                        Invariant(item.Groundedness), Invariant(item.Relevance),
                        Invariant(item.Safety), Invariant(item.OverallQuality),
                        item.ProviderInvocationPrevented.ToString(), item.SourceExecutionId?.ToString(),
                        item.SourceType, item.SourceId?.ToString()
                    ]);
                    if (export.IncludeSensitiveSource)
                        values.AddRange([item.PromptName, item.WorkflowName, item.KnowledgeCollectionName]);
                    values.AddRange([
                        item.PolicyOutcome, item.EvaluationOutcome, item.ConfigurationRevision,
                        item.CorrelationId
                    ]);
                    csv.AppendLine(string.Join(',', values.Select(Cell)));
                }
                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                if (bytes.Length > 25 * 1024 * 1024) throw new InvalidOperationException("Export exceeds the 25 MB limit.");
                export.Content = bytes;
                export.RowCount = events.Count;
                export.SizeBytes = bytes.Length;
                export.Checksum = $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
                export.Status = "Completed";
                export.CompletedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception exception)
            {
                export.Status = "Failed";
                export.FailureReason = exception.Message[..Math.Min(exception.Message.Length, 2000)];
                export.CompletedAt = DateTimeOffset.UtcNow;
            }
        }
        if (exports.Count > 0) await db.SaveChangesAsync(ct);
    }

    private static string? Invariant<T>(T? value) where T : struct, IFormattable =>
        value?.ToString(null, System.Globalization.CultureInfo.InvariantCulture);

    private static string Cell(string? value)
    {
        value ??= string.Empty;
        if (value.Length > 0 && "=+-@".Contains(value[0])) value = $"'{value}";
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static async Task RebuildAggregatesAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var checkpoints = await db.AnalyticsAggregationCheckpoints.ToListAsync(ct);
        var ranges = await db.AnalyticsEvents.AsNoTracking()
            .GroupBy(item => item.WorkspaceId)
            .Select(group => new
            {
                WorkspaceId = group.Key,
                From = group.Min(item => item.OccurredAt),
                To = group.Max(item => item.OccurredAt)
            })
            .ToListAsync(ct);
        foreach (var range in ranges)
        {
            foreach (var granularity in new[] { "hour", "day" })
            {
                var checkpoint = checkpoints.SingleOrDefault(item =>
                    item.WorkspaceId == range.WorkspaceId && item.Granularity == granularity);
                if (checkpoint is null)
                {
                    checkpoint = new AnalyticsAggregationCheckpointRecord
                    {
                        Id = Guid.NewGuid(),
                        WorkspaceId = range.WorkspaceId,
                        Granularity = granularity,
                        DirtyFromUtc = range.From,
                        DirtyToUtc = range.To,
                        Status = "Pending",
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    db.AnalyticsAggregationCheckpoints.Add(checkpoint);
                    checkpoints.Add(checkpoint);
                }
                else if (!checkpoint.DirtyFromUtc.HasValue
                    && (!checkpoint.HighWatermarkUtc.HasValue
                        || range.To > checkpoint.HighWatermarkUtc))
                {
                    checkpoint.DirtyFromUtc = checkpoint.HighWatermarkUtc ?? range.From;
                    checkpoint.DirtyToUtc = range.To;
                    checkpoint.Status = "Pending";
                    checkpoint.UpdatedAt = DateTimeOffset.UtcNow;
                }
                else if (!checkpoint.DirtyFromUtc.HasValue
                    && checkpoint.LastSuccessfulRunAt < DateTimeOffset.UtcNow.AddHours(-1))
                {
                    var reconciliationFrom = range.To.AddHours(-24);
                    checkpoint.DirtyFromUtc = reconciliationFrom > range.From
                        ? reconciliationFrom
                        : range.From;
                    checkpoint.DirtyToUtc = range.To;
                    checkpoint.Status = "Pending";
                    checkpoint.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
        }
        await db.SaveChangesAsync(ct);

        foreach (var checkpoint in checkpoints
            .Where(item => item.DirtyFromUtc.HasValue)
            .OrderBy(item => item.UpdatedAt))
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                if (db.Database.IsNpgsql())
                {
                    var lockKey = $"analytics-aggregate:{checkpoint.WorkspaceId:N}:{checkpoint.Granularity}";
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))",
                        ct);
                }
                checkpoint.Status = "Processing";
                checkpoint.FailureReason = null;
                checkpoint.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);

                var hourly = checkpoint.Granularity == "hour";
                var from = Truncate(checkpoint.DirtyFromUtc!.Value, hourly);
                var dirtyTo = checkpoint.DirtyToUtc ?? checkpoint.DirtyFromUtc.Value;
                var to = Truncate(dirtyTo, hourly).Add(hourly
                    ? TimeSpan.FromHours(1)
                    : TimeSpan.FromDays(1));

                if (hourly)
                {
                    await db.AnalyticsHourlyAggregates
                        .Where(item => item.WorkspaceId == checkpoint.WorkspaceId
                            && item.BucketStart >= from
                            && item.BucketStart < to)
                        .ExecuteDeleteAsync(ct);
                }
                else
                {
                    await db.AnalyticsDailyAggregates
                        .Where(item => item.WorkspaceId == checkpoint.WorkspaceId
                            && item.BucketStart >= from
                            && item.BucketStart < to)
                        .ExecuteDeleteAsync(ct);
                }

                var events = await db.AnalyticsEvents.AsNoTracking()
                    .Where(item => item.WorkspaceId == checkpoint.WorkspaceId
                        && item.OccurredAt >= from
                        && item.OccurredAt < to)
                    .ToListAsync(ct);
                AddAggregates(db, events, hourly);

                checkpoint.HighWatermarkUtc = checkpoint.HighWatermarkUtc > dirtyTo
                    ? checkpoint.HighWatermarkUtc
                    : dirtyTo;
                checkpoint.DirtyFromUtc = null;
                checkpoint.DirtyToUtc = null;
                checkpoint.LastSuccessfulRunAt = DateTimeOffset.UtcNow;
                checkpoint.Status = "Completed";
                checkpoint.FailureReason = null;
                checkpoint.UpdatedAt = DateTimeOffset.UtcNow;
                checkpoint.Revision++;
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception exception)
            {
                checkpoint.Status = "Failed";
                checkpoint.FailureReason = exception.Message[..Math.Min(
                    exception.Message.Length,
                    2000)];
                checkpoint.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private static void AddAggregates(ApplicationDbContext db, IReadOnlyList<AnalyticsEventRecord> events, bool hourly)
    {
        var groups = events.GroupBy(item => new
        {
            item.OrganisationId, item.WorkspaceId, item.EnvironmentId,
            Bucket = Truncate(item.OccurredAt, hourly),
            item.Provider, item.Model, item.Capability, item.Outcome
        });
        foreach (var group in groups)
        {
            var key = AnalyticsKeys.Aggregate(
                hourly ? "hour" : "day", group.Key.WorkspaceId.ToString(), group.Key.EnvironmentId.ToString(),
                group.Key.Bucket.ToString("O"), group.Key.Provider, group.Key.Model, group.Key.Capability, group.Key.Outcome);
            AnalyticsAggregateRecord aggregate = hourly
                ? new AnalyticsHourlyAggregateRecord()
                : new AnalyticsDailyAggregateRecord();
            aggregate.Id = Guid.NewGuid(); aggregate.AggregateKey = key;
            aggregate.OrganisationId = group.Key.OrganisationId;
            aggregate.WorkspaceId = group.Key.WorkspaceId; aggregate.EnvironmentId = group.Key.EnvironmentId;
            aggregate.BucketStart = group.Key.Bucket; aggregate.Provider = group.Key.Provider; aggregate.Model = group.Key.Model;
            aggregate.Capability = group.Key.Capability; aggregate.Outcome = group.Key.Outcome;
            aggregate.EventCount = group.LongCount();
            aggregate.ExecutionCount = group
                .Where(item => AnalyticsSemantics.IsExecutionTerminal(item.EventType))
                .Select(item => item.SourceExecutionId ?? item.Id)
                .Distinct()
                .LongCount();
            aggregate.Executions = aggregate.ExecutionCount;
            aggregate.SimulationCount = group.LongCount(item =>
                AnalyticsSemantics.IsSimulationTerminal(item.EventType));
            aggregate.EvaluationCount = group.LongCount(item =>
                AnalyticsSemantics.IsEvaluationTerminal(item.EventType));
            aggregate.TraceCount = group.LongCount(item =>
                AnalyticsSemantics.IsTraceTerminal(item.EventType));
            aggregate.ReplayCount = group.LongCount(item =>
                AnalyticsSemantics.IsReplayTerminal(item.EventType));
            aggregate.ProviderInvocationCount = group.LongCount(item =>
                AnalyticsSemantics.IsProviderInvocation(item.EventType));
            aggregate.ProviderInvocationPreventedCount = group.LongCount(item =>
                item.ProviderInvocationPrevented);
            aggregate.PolicyEvaluationCount = group.LongCount(item =>
                AnalyticsSemantics.IsPolicyEvaluation(item.EventType));
            aggregate.PolicyAllowedCount = group.LongCount(item =>
                AnalyticsSemantics.IsPolicyEvaluation(item.EventType) && item.Outcome == "Allowed");
            aggregate.PolicyDeniedCount = group.LongCount(item =>
                AnalyticsSemantics.IsPolicyEvaluation(item.EventType) && item.Outcome == "Denied");
            aggregate.PolicyWarningCount = group.LongCount(item =>
                AnalyticsSemantics.IsPolicyEvaluation(item.EventType) && item.Outcome == "Warning");
            aggregate.PluginOperationCount = group.LongCount(item =>
                AnalyticsSemantics.IsPluginOperation(item.EventType));
            aggregate.Succeeded = group.LongCount(item => item.Outcome is "Succeeded" or "Completed");
            aggregate.Failed = group.LongCount(item => item.Outcome == "Failed");
            aggregate.Denied = group.LongCount(item => item.Outcome == "Denied");
            aggregate.InputTokens = group.Sum(item => (long)(item.InputTokens ?? 0));
            aggregate.OutputTokens = group.Sum(item => (long)(item.OutputTokens ?? 0));
            aggregate.ActualCostZar = group.Where(item => item.CostType == "Actual").Sum(item => item.CostZar ?? 0);
            aggregate.EstimatedCostZar = group.Where(item => item.CostType == "Estimated").Sum(item => item.CostZar ?? 0);
            aggregate.UnknownCostCount = group.LongCount(item => item.CostType == "Unavailable");
            aggregate.TotalDurationMs = group.Sum(item => item.DurationMs ?? 0);
            aggregate.MaximumDurationMs = group.Max(item => item.DurationMs ?? 0);
            aggregate.DurationCount = group.LongCount(item => item.DurationMs.HasValue);
            aggregate.TotalQualityScore = group.Sum(item => item.QualityScore ?? 0);
            aggregate.QualityCount = group.LongCount(item => item.QualityScore.HasValue);
            aggregate.UpdatedAt = DateTimeOffset.UtcNow;
            if (hourly) db.AnalyticsHourlyAggregates.Add((AnalyticsHourlyAggregateRecord)aggregate);
            else db.AnalyticsDailyAggregates.Add((AnalyticsDailyAggregateRecord)aggregate);
        }
    }

    private static DateTimeOffset Truncate(DateTimeOffset value, bool hourly)
    {
        var utc = value.UtcDateTime;
        return hourly
            ? new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero)
            : new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero);
    }

    private static async Task ApplyRetentionAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var environments = await db.RuntimeEnvironments.AsNoTracking()
            .Select(item => new { item.WorkspaceId, EnvironmentId = item.Id }).ToListAsync(ct);
        foreach (var environment in environments)
        {
            var eventDays = await RetentionDaysAsync(db, SettingKeys.AnalyticsEventRetentionDays, environment.WorkspaceId, environment.EnvironmentId, 90, ct);
            var hourlyDays = await RetentionDaysAsync(db, SettingKeys.AnalyticsHourlyRetentionDays, environment.WorkspaceId, environment.EnvironmentId, 90, ct);
            var dailyDays = await RetentionDaysAsync(db, SettingKeys.AnalyticsDailyRetentionDays, environment.WorkspaceId, environment.EnvironmentId, 730, ct);
            await db.AnalyticsEvents.Where(item => item.EnvironmentId == environment.EnvironmentId && item.OccurredAt < now.AddDays(-eventDays)).ExecuteDeleteAsync(ct);
            await db.AnalyticsHourlyAggregates.Where(item => item.EnvironmentId == environment.EnvironmentId && item.BucketStart < now.AddDays(-hourlyDays)).ExecuteDeleteAsync(ct);
            await db.AnalyticsDailyAggregates.Where(item => item.EnvironmentId == environment.EnvironmentId && item.BucketStart < now.AddDays(-dailyDays)).ExecuteDeleteAsync(ct);
        }
        await db.AnalyticsExports
            .Where(item => item.ExpiresAt <= now)
            .ExecuteDeleteAsync(ct);
    }

    private static async Task<int> RetentionDaysAsync(
        ApplicationDbContext db,
        string key,
        Guid workspaceId,
        Guid? environmentId,
        int fallback,
        CancellationToken ct)
    {
        var values = await db.SettingValues.AsNoTracking()
            .Where(item => item.DefinitionKey == key
                && (item.WorkspaceId == workspaceId || environmentId.HasValue && item.EnvironmentId == environmentId))
            .ToListAsync(ct);
        var selected = values.FirstOrDefault(item => environmentId.HasValue && item.EnvironmentId == environmentId)
            ?? values.FirstOrDefault(item => item.WorkspaceId == workspaceId);
        var raw = selected?.ValueJson
            ?? await db.SettingDefinitions.AsNoTracking().Where(item => item.Key == key).Select(item => item.DefaultValue).SingleOrDefaultAsync(ct);
        return int.TryParse(raw?.Trim('"'), out var days) ? Math.Clamp(days, 1, 3650) : fallback;
    }
}
