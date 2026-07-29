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
            : await db.AnalyticsOutbox
                .Where(item => item.Status == "Pending" && item.AvailableAt <= now)
                .OrderBy(item => item.CreatedAt).Take(100).ToListAsync(ct);
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
                    foreach (var checkpoint in checkpoints.Where(value =>
                        !value.DirtyFromUtc.HasValue || analyticsEvent.OccurredAt < value.DirtyFromUtc))
                        checkpoint.DirtyFromUtc = analyticsEvent.OccurredAt;
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
                var events = (await db.AnalyticsEvents.AsNoTracking()
                        .Where(item => item.WorkspaceId == export.WorkspaceId).ToListAsync(ct))
                    .Where(item => item.OccurredAt >= request.From && item.OccurredAt < request.To
                        && (!request.EnvironmentId.HasValue || item.EnvironmentId == request.EnvironmentId)
                        && (request.Provider == null || item.Provider == request.Provider)
                        && (request.Model == null || item.Model == request.Model)
                        && (request.Capability == null || item.Capability == request.Capability)
                        && (request.Outcome == null || item.Outcome == request.Outcome))
                    .OrderBy(item => item.OccurredAt).Take(100_001).ToList();
                if (events.Count > 100_000) throw new InvalidOperationException("Export exceeds the 100,000 row limit.");
                var csv = new StringBuilder("occurredAt,environmentId,capability,eventType,outcome,provider,model,inputTokens,outputTokens,costZar,costType,durationMs,qualityScore,sourceType,sourceId,configurationRevision,correlationId\r\n");
                foreach (var item in events)
                {
                    csv.AppendLine(string.Join(',', new[]
                    {
                        Cell(item.OccurredAt.ToString("O")), Cell(item.EnvironmentId.ToString()), Cell(item.Capability),
                        Cell(item.EventType), Cell(item.Outcome), Cell(item.Provider), Cell(item.Model),
                        Cell(item.InputTokens?.ToString()), Cell(item.OutputTokens?.ToString()),
                        Cell(item.CostZar?.ToString(System.Globalization.CultureInfo.InvariantCulture)), Cell(item.CostType),
                        Cell(item.DurationMs?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        Cell(item.QualityScore?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        Cell(item.SourceType), Cell(item.SourceId?.ToString()), Cell(item.ConfigurationRevision), Cell(item.CorrelationId)
                    }));
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

    private static string Cell(string? value)
    {
        value ??= string.Empty;
        if (value.Length > 0 && "=+-@".Contains(value[0])) value = $"'{value}";
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static async Task RebuildAggregatesAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var newest = await db.AnalyticsEvents.OrderByDescending(item => item.OccurredAt)
            .Select(item => (DateTimeOffset?)item.OccurredAt).FirstOrDefaultAsync(ct);
        if (!newest.HasValue) return;
        var checkpoints = await db.AnalyticsAggregationCheckpoints.ToListAsync(ct);
        var requiresRebuild = checkpoints.Count == 0
            || checkpoints.Any(item => item.DirtyFromUtc.HasValue || item.HighWatermarkUtc < newest);
        if (!requiresRebuild) return;

        await db.AnalyticsHourlyAggregates.ExecuteDeleteAsync(ct);
        await db.AnalyticsDailyAggregates.ExecuteDeleteAsync(ct);
        var events = await db.AnalyticsEvents.AsNoTracking().ToListAsync(ct);
        AddAggregates(db, events, true);
        AddAggregates(db, events, false);
        var now = DateTimeOffset.UtcNow;
        foreach (var workspace in events.GroupBy(item => item.WorkspaceId))
        {
            var workspaceWatermark = workspace.Max(item => item.OccurredAt);
            foreach (var granularity in new[] { "hour", "day" })
            {
                var checkpoint = checkpoints.SingleOrDefault(item =>
                    item.WorkspaceId == workspace.Key && item.Granularity == granularity);
                if (checkpoint is null)
                {
                    checkpoint = new AnalyticsAggregationCheckpointRecord
                    {
                        Id = Guid.NewGuid(), WorkspaceId = workspace.Key, Granularity = granularity
                    };
                    db.AnalyticsAggregationCheckpoints.Add(checkpoint);
                }
                checkpoint.HighWatermarkUtc = workspaceWatermark;
                checkpoint.DirtyFromUtc = null;
                checkpoint.UpdatedAt = now;
                checkpoint.Revision++;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static void AddAggregates(ApplicationDbContext db, IReadOnlyList<AnalyticsEventRecord> events, bool hourly)
    {
        var groups = events.GroupBy(item => new
        {
            item.WorkspaceId, item.EnvironmentId,
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
            aggregate.WorkspaceId = group.Key.WorkspaceId; aggregate.EnvironmentId = group.Key.EnvironmentId;
            aggregate.BucketStart = group.Key.Bucket; aggregate.Provider = group.Key.Provider; aggregate.Model = group.Key.Model;
            aggregate.Capability = group.Key.Capability; aggregate.Outcome = group.Key.Outcome;
            aggregate.Executions = group.LongCount();
            aggregate.Succeeded = group.LongCount(item => item.Outcome is "Succeeded" or "Completed");
            aggregate.Failed = group.LongCount(item => item.Outcome == "Failed");
            aggregate.Denied = group.LongCount(item => item.Outcome == "Denied");
            aggregate.InputTokens = group.Sum(item => (long)(item.InputTokens ?? 0));
            aggregate.OutputTokens = group.Sum(item => (long)(item.OutputTokens ?? 0));
            aggregate.ActualCostZar = group.Where(item => item.CostType == "Actual").Sum(item => item.CostZar ?? 0);
            aggregate.EstimatedCostZar = group.Where(item => item.CostType == "Estimated").Sum(item => item.CostZar ?? 0);
            aggregate.UnknownCostCount = group.LongCount(item => item.CostType == "Unavailable");
            aggregate.TotalDurationMs = group.Sum(item => item.DurationMs ?? 0);
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
        foreach (var workspaceId in environments.Select(item => item.WorkspaceId).Distinct())
        {
            var exportDays = await RetentionDaysAsync(db, SettingKeys.AnalyticsExportRetentionDays, workspaceId, null, 7, ct);
            await db.AnalyticsExports.Where(item => item.WorkspaceId == workspaceId && item.CreatedAt < now.AddDays(-exportDays)).ExecuteDeleteAsync(ct);
        }
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
