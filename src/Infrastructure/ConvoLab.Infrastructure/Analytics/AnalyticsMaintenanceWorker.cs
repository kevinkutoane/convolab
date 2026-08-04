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
using ConvoLab.Application.Operations;
using ConvoLab.Infrastructure.Operations;
using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace ConvoLab.Infrastructure.Analytics;

public sealed class AnalyticsMaintenanceWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AnalyticsMaintenanceWorker> logger,
    IOperationalWorkerLease lease,
    IOptions<AnalyticsWorkerOptions> workerOptions) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string WorkerName = "analytics-maintenance";
    private readonly AnalyticsWorkerOptions _options = workerOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var acquired = await lease.TryAcquireAsync(WorkerName, stoppingToken);
                if (acquired is null)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(_options.PollIntervalSeconds),
                        stoppingToken);
                    continue;
                }
                await RunOwnedIterationAsync(acquired, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                ConvoLabTelemetry.AnalyticsWorkerFailures.Add(1);
                logger.LogError(
                    "Analytics maintenance iteration failed {ExceptionType}",
                    exception.GetType().Name);
            }
            await Task.Delay(
                TimeSpan.FromSeconds(_options.PollIntervalSeconds),
                stoppingToken);
        }
    }

    private async Task RunOwnedIterationAsync(
        WorkerLeaseHandle acquired,
        CancellationToken stoppingToken)
    {
        if (!await lease.RecordIterationStartedAsync(acquired, stoppingToken)) return;
        using var activity = ConvoLabTelemetry.ActivitySource.StartActivity("analytics.worker.iteration");
        activity?.SetTag("worker.name", WorkerName);
        var stopwatch = Stopwatch.StartNew();
        using var renewalCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var renewalState = new LeaseRenewalState();
        var renewalTask = RenewLeaseAsync(
            acquired,
            renewalState,
            renewalCancellation.Token);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var outbox = await DispatchOutboxAsync(
                db,
                _options.MaximumBatchSize,
                stoppingToken);
            var exports = await BuildExportsAsync(
                db,
                acquired,
                Math.Min(25, _options.MaximumBatchSize),
                stoppingToken);
            var aggregates = await RebuildAggregatesAsync(db, stoppingToken);
            var retained = await ApplyRetentionAsync(db, stoppingToken);
            var failureCodes = outbox.FailureCodes
                .Concat(exports.FailureCodes)
                .Concat(aggregates.FailureCodes)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var result = new AnalyticsMaintenanceResult(
                outbox.Completed,
                outbox.Failed,
                exports.Completed,
                exports.Failed,
                aggregates.Completed,
                aggregates.Failed,
                retained,
                failureCodes.Length > 0,
                failureCodes);

            renewalCancellation.Cancel();
            await AwaitRenewalAsync(renewalTask, stoppingToken);
            if (renewalState.OwnershipLost
                || !await lease.IsOwnedAsync(acquired, stoppingToken)
                || !await lease.RecordResultAsync(acquired, result, stoppingToken))
            {
                await lease.RecordFailureAsync(
                    acquired,
                    "analytics.worker.lease_lost",
                    "LeaseLost",
                    stoppingToken);
                ConvoLabTelemetry.AnalyticsWorkerFailures.Add(1);
                return;
            }

            activity?.SetTag("worker.outcome", result.PartialFailure ? "degraded" : "healthy");
            activity?.SetTag("worker.processed", result.TotalProcessed);
        }
        catch
        {
            renewalCancellation.Cancel();
            await AwaitRenewalAsync(renewalTask, stoppingToken);
            if (!renewalState.OwnershipLost)
                await lease.RecordFailureAsync(
                    acquired,
                    "analytics.worker.iteration_failed",
                    ct: stoppingToken);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            ConvoLabTelemetry.AnalyticsWorkerDuration.Record(stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private async Task RenewLeaseAsync(
        WorkerLeaseHandle acquired,
        LeaseRenewalState state,
        CancellationToken ct)
    {
        var transientFailures = 0;
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.LeaseRenewalSeconds), ct);
            try
            {
                if (!await lease.RenewAsync(acquired, ct))
                {
                    state.OwnershipLost = true;
                    return;
                }
                transientFailures = 0;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                transientFailures++;
                logger.LogWarning(
                    "Analytics worker lease renewal failed {FailureNumber} {ExceptionType}",
                    transientFailures,
                    exception.GetType().Name);
                if (transientFailures > _options.RenewalFailureTolerance)
                {
                    state.OwnershipLost = true;
                    return;
                }
            }
        }
    }

    private static async Task AwaitRenewalAsync(Task renewalTask, CancellationToken stoppingToken)
    {
        try
        {
            await renewalTask;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (OperationCanceledException) { }
    }

    private static async Task<ComponentResult> DispatchOutboxAsync(
        ApplicationDbContext db,
        int maximumBatchSize,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var completed = 0;
        var failed = 0;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var pending = db.Database.IsNpgsql()
            ? await db.AnalyticsOutbox.FromSqlInterpolated($"""
                SELECT * FROM "AnalyticsOutbox"
                WHERE "Status" = 'Pending' AND "AvailableAt" <= {now}
                ORDER BY "CreatedAt"
                FOR UPDATE SKIP LOCKED
                LIMIT {maximumBatchSize}
                """).ToListAsync(ct)
            : (await db.AnalyticsOutbox
                    .Where(item => item.Status == "Pending")
                    .ToListAsync(ct))
                .Where(item => item.AvailableAt <= now)
                .OrderBy(item => item.CreatedAt)
                .Take(maximumBatchSize)
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
                ConvoLabTelemetry.AnalyticsOutboxProcessed.Add(1);
                item.ProcessedAt = now;
                item.LastError = null;
                completed++;
            }
            catch (Exception)
            {
                failed++;
                item.Attempts++;
                item.Status = item.Attempts >= 10 ? "Failed" : "Pending";
                item.LastError = "analytics.outbox.dispatch_failed";
                item.AvailableAt = now.AddSeconds(Math.Min(300, Math.Pow(2, item.Attempts)));
            }
        }
        if (pending.Count > 0) await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(
            completed,
            failed,
            failed == 0 ? [] : ["analytics.outbox.dispatch_failed"]);
    }

    private async Task<ComponentResult> BuildExportsAsync(
        ApplicationDbContext db,
        WorkerLeaseHandle workerLease,
        int maximumBatchSize,
        CancellationToken ct)
    {
        var exports = await ClaimExportsAsync(
            db,
            workerLease,
            maximumBatchSize,
            ct);
        var completed = 0;
        var failed = 0;
        var failureCodes = new HashSet<string>(StringComparer.Ordinal);
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
                var checksum = $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
                if (await CompleteExportAsync(
                        db,
                        workerLease,
                        export.Id,
                        bytes,
                        events.Count,
                        checksum,
                        ct))
                    completed++;
                else
                {
                    failed++;
                    failureCodes.Add("analytics.export.claim_lost");
                }
            }
            catch (Exception)
            {
                failed++;
                failureCodes.Add("analytics.export.processing_failed");
                await FailExportAsync(
                    db,
                    workerLease,
                    export.Id,
                    "analytics.export.processing_failed",
                    ct);
            }
        }
        return new(completed, failed, failureCodes.ToArray());
    }

    private async Task<List<AnalyticsExportRecord>> ClaimExportsAsync(
        ApplicationDbContext db,
        WorkerLeaseHandle workerLease,
        int maximumBatchSize,
        CancellationToken ct)
    {
        if (db.Database.IsNpgsql())
            return await AnalyticsExportClaims.ClaimAsync(
                db,
                workerLease,
                _options.LeaseDurationSeconds,
                maximumBatchSize,
                ct);

        var now = DateTimeOffset.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var claimed = await db.AnalyticsExports
            .Where(item => item.Status == "Pending"
                || item.Status == "Processing"
                && item.ProcessingStartedAt <= now.AddSeconds(-_options.LeaseDurationSeconds))
            .OrderBy(item => item.CreatedAt)
            .Take(maximumBatchSize)
            .ToListAsync(ct);
        foreach (var export in claimed)
        {
            export.Status = "Processing";
            export.ProcessingOwner = workerLease.Owner;
            export.ProcessingLeaseToken = workerLease.Token;
            export.ProcessingStartedAt = now;
            export.AttemptCount++;
            export.FailureReason = null;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        foreach (var export in claimed) db.Entry(export).State = EntityState.Detached;
        return claimed;
    }

    private async Task<bool> CompleteExportAsync(
        ApplicationDbContext db,
        WorkerLeaseHandle workerLease,
        Guid exportId,
        byte[] content,
        long rowCount,
        string checksum,
        CancellationToken ct)
    {
        if (db.Database.IsNpgsql())
            return await db.Database.ExecuteSqlInterpolatedAsync($"""
                WITH server_time AS MATERIALIZED (
                    SELECT clock_timestamp() AS now
                )
                UPDATE "AnalyticsExports" AS export
                SET "Content" = {content},
                    "RowCount" = {rowCount},
                    "SizeBytes" = {content.LongLength},
                    "Checksum" = {checksum},
                    "Status" = 'Completed',
                    "CompletedAt" = server_time.now,
                    "FailureReason" = NULL
                FROM server_time
                WHERE export."Id" = {exportId}
                  AND export."Status" = 'Processing'
                  AND export."ProcessingOwner" = {workerLease.Owner}
                  AND export."ProcessingLeaseToken" = {workerLease.Token}
                  AND EXISTS (
                      SELECT 1 FROM "OperationalWorkerHeartbeats" AS worker
                      WHERE worker."WorkerName" = {workerLease.WorkerName}
                        AND worker."InstanceId" = {workerLease.Owner}
                        AND worker."LeaseToken" = {workerLease.Token}
                        AND worker."LeaseExpiresAt" > server_time.now
                  )
                """, ct) == 1;

        if (!await lease.IsOwnedAsync(workerLease, ct)) return false;
        var export = await db.AnalyticsExports.SingleOrDefaultAsync(item =>
            item.Id == exportId
            && item.Status == "Processing"
            && item.ProcessingOwner == workerLease.Owner
            && item.ProcessingLeaseToken == workerLease.Token, ct);
        if (export is null) return false;
        export.Content = content;
        export.RowCount = rowCount;
        export.SizeBytes = content.LongLength;
        export.Checksum = checksum;
        export.Status = "Completed";
        export.CompletedAt = DateTimeOffset.UtcNow;
        export.FailureReason = null;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<bool> FailExportAsync(
        ApplicationDbContext db,
        WorkerLeaseHandle workerLease,
        Guid exportId,
        string failureCode,
        CancellationToken ct)
    {
        if (db.Database.IsNpgsql())
            return await db.Database.ExecuteSqlInterpolatedAsync($"""
                WITH server_time AS MATERIALIZED (
                    SELECT clock_timestamp() AS now
                )
                UPDATE "AnalyticsExports" AS export
                SET "Status" = 'Failed',
                    "FailureReason" = {failureCode},
                    "CompletedAt" = server_time.now
                FROM server_time
                WHERE export."Id" = {exportId}
                  AND export."Status" = 'Processing'
                  AND export."ProcessingOwner" = {workerLease.Owner}
                  AND export."ProcessingLeaseToken" = {workerLease.Token}
                  AND EXISTS (
                      SELECT 1 FROM "OperationalWorkerHeartbeats" AS worker
                      WHERE worker."WorkerName" = {workerLease.WorkerName}
                        AND worker."InstanceId" = {workerLease.Owner}
                        AND worker."LeaseToken" = {workerLease.Token}
                        AND worker."LeaseExpiresAt" > server_time.now
                  )
                """, ct) == 1;

        if (!await lease.IsOwnedAsync(workerLease, ct)) return false;
        var export = await db.AnalyticsExports.SingleOrDefaultAsync(item =>
            item.Id == exportId
            && item.Status == "Processing"
            && item.ProcessingOwner == workerLease.Owner
            && item.ProcessingLeaseToken == workerLease.Token, ct);
        if (export is null) return false;
        export.Status = "Failed";
        export.FailureReason = failureCode;
        export.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static string? Invariant<T>(T? value) where T : struct, IFormattable =>
        value?.ToString(null, System.Globalization.CultureInfo.InvariantCulture);

    private static string Cell(string? value)
    {
        value ??= string.Empty;
        if (value.Length > 0 && "=+-@".Contains(value[0])) value = $"'{value}";
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static async Task<ComponentResult> RebuildAggregatesAsync(
        ApplicationDbContext db,
        CancellationToken ct)
    {
        var completed = 0;
        var failed = 0;
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
                ConvoLabTelemetry.AnalyticsAggregationRuns.Add(1);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                completed++;
            }
            catch (Exception)
            {
                failed++;
                checkpoint.Status = "Failed";
                checkpoint.FailureReason = "analytics.aggregation.rebuild_failed";
                checkpoint.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
        return new(
            completed,
            failed,
            failed == 0 ? [] : ["analytics.aggregation.rebuild_failed"]);
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

    private static async Task<int> ApplyRetentionAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var removed = 0;
        var environments = await db.RuntimeEnvironments.AsNoTracking()
            .Select(item => new { item.WorkspaceId, EnvironmentId = item.Id }).ToListAsync(ct);
        foreach (var environment in environments)
        {
            var eventDays = await RetentionDaysAsync(db, SettingKeys.AnalyticsEventRetentionDays, environment.WorkspaceId, environment.EnvironmentId, 90, ct);
            var hourlyDays = await RetentionDaysAsync(db, SettingKeys.AnalyticsHourlyRetentionDays, environment.WorkspaceId, environment.EnvironmentId, 90, ct);
            var dailyDays = await RetentionDaysAsync(db, SettingKeys.AnalyticsDailyRetentionDays, environment.WorkspaceId, environment.EnvironmentId, 730, ct);
            removed += await db.AnalyticsEvents.Where(item => item.EnvironmentId == environment.EnvironmentId && item.OccurredAt < now.AddDays(-eventDays)).ExecuteDeleteAsync(ct);
            removed += await db.AnalyticsHourlyAggregates.Where(item => item.EnvironmentId == environment.EnvironmentId && item.BucketStart < now.AddDays(-hourlyDays)).ExecuteDeleteAsync(ct);
            removed += await db.AnalyticsDailyAggregates.Where(item => item.EnvironmentId == environment.EnvironmentId && item.BucketStart < now.AddDays(-dailyDays)).ExecuteDeleteAsync(ct);
        }
        removed += await db.AnalyticsExports
            .Where(item => item.ExpiresAt <= now)
            .ExecuteDeleteAsync(ct);
        return removed;
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

    private sealed record ComponentResult(
        int Completed,
        int Failed,
        IReadOnlyCollection<string> FailureCodes);

    private sealed class LeaseRenewalState
    {
        public volatile bool OwnershipLost;
    }
}
