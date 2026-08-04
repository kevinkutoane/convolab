using System.Collections.Concurrent;
using System.Data;
using ConvoLab.Application.Operations;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ConvoLab.Infrastructure.Operations;

public sealed record WorkerLeaseHandle(
    string WorkerName,
    string Owner,
    long Token,
    DateTimeOffset LeaseExpiresAt);

public interface IOperationalWorkerLease
{
    string InstanceId { get; }
    Task<WorkerLeaseHandle?> TryAcquireAsync(string workerName, CancellationToken ct = default);
    Task<bool> RenewAsync(WorkerLeaseHandle lease, CancellationToken ct = default);
    Task<bool> IsOwnedAsync(WorkerLeaseHandle lease, CancellationToken ct = default);
    Task<bool> RecordIterationStartedAsync(WorkerLeaseHandle lease, CancellationToken ct = default);
    Task<bool> RecordResultAsync(WorkerLeaseHandle lease, AnalyticsMaintenanceResult result, CancellationToken ct = default);
    Task<bool> RecordFailureAsync(WorkerLeaseHandle lease, string failureCode, string status = "Failed", CancellationToken ct = default);

    // Compatibility boundary retained for alpha.14 callers and tests.
    Task<bool> AcquireOrRenewAsync(string workerName, CancellationToken ct = default);
    Task RecordSuccessAsync(string workerName, long processedCount, CancellationToken ct = default);
    Task RecordFailureAsync(string workerName, string failureCode, CancellationToken ct = default);
}

public sealed class OperationalWorkerLease(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<AnalyticsWorkerOptions>? configuredOptions = null) : IOperationalWorkerLease
{
    private readonly AnalyticsWorkerOptions _options = configuredOptions?.Value ?? new();
    private readonly ConcurrentDictionary<string, WorkerLeaseHandle> _compatibilityLeases =
        new(StringComparer.Ordinal);
    public string InstanceId { get; } = BuildInstanceId();

    public async Task<WorkerLeaseHandle?> TryAcquireAsync(
        string workerName,
        CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (db.Database.IsNpgsql())
        {
            var row = await ExecuteLeaseQueryAsync(
                db,
                PostgresWorkerLeaseSql.Acquire(
                    workerName,
                    InstanceId,
                    _options.LeaseDurationSeconds),
                ct);
            return row is null
                ? null
                : new(workerName, InstanceId, row.LeaseToken, row.LeaseExpiresAt);
        }

        var now = timeProvider.GetUtcNow();
        var record = await db.OperationalWorkerHeartbeats
            .SingleOrDefaultAsync(item => item.WorkerName == workerName, ct);
        if (record is not null
            && record.InstanceId != InstanceId
            && record.LeaseExpiresAt > now)
            return null;
        if (record is null)
        {
            record = new OperationalWorkerHeartbeatRecord
            {
                WorkerName = workerName,
                InstanceId = InstanceId,
                StartedAt = now,
                LeaseToken = 1,
                Revision = 1
            };
            db.OperationalWorkerHeartbeats.Add(record);
        }
        else
        {
            if (record.InstanceId != InstanceId) record.StartedAt = now;
            record.InstanceId = InstanceId;
            record.LeaseToken++;
            record.Revision++;
        }
        record.LastHeartbeatAt = now;
        record.LeaseExpiresAt = now.AddSeconds(_options.LeaseDurationSeconds);
        record.CurrentStatus = "Running";
        await db.SaveChangesAsync(ct);
        return new(workerName, InstanceId, record.LeaseToken, record.LeaseExpiresAt);
    }

    private static async Task<LeaseRow?> ExecuteLeaseQueryAsync(
        ApplicationDbContext db,
        FormattableString statement,
        CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var closeAfter = connection.State != ConnectionState.Open;
        if (closeAfter) await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            var commandText = statement.Format;
            for (var index = statement.ArgumentCount - 1; index >= 0; index--)
            {
                var parameterName = $"@p{index}";
                commandText = commandText.Replace(
                    $"{{{index}}}", parameterName, StringComparison.Ordinal);
                var parameter = command.CreateParameter();
                parameter.ParameterName = parameterName;
                parameter.Value = statement.GetArgument(index) ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
            command.CommandText = commandText;
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            return new LeaseRow
            {
                LeaseToken = reader.GetInt64(0),
                LeaseExpiresAt = reader.GetFieldValue<DateTimeOffset>(1)
            };
        }
        finally
        {
            if (closeAfter) await db.Database.CloseConnectionAsync();
        }
    }

    public async Task<bool> RenewAsync(
        WorkerLeaseHandle lease,
        CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (db.Database.IsNpgsql())
            return await db.Database.ExecuteSqlInterpolatedAsync(
                PostgresWorkerLeaseSql.Renew(
                    lease.WorkerName,
                    lease.Owner,
                    lease.Token,
                    _options.LeaseDurationSeconds), ct) == 1;

        var now = timeProvider.GetUtcNow();
        var record = await OwnedAsync(db, lease, ct);
        if (record is null || record.LeaseExpiresAt <= now) return false;
        record.LastHeartbeatAt = now;
        record.LeaseExpiresAt = now.AddSeconds(_options.LeaseDurationSeconds);
        record.Revision++;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> IsOwnedAsync(
        WorkerLeaseHandle lease,
        CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (db.Database.IsNpgsql())
            return await db.Database.SqlQuery<bool>(
                    PostgresWorkerLeaseSql.IsOwned(
                        lease.WorkerName,
                        lease.Owner,
                        lease.Token))
                .SingleAsync(ct);
        var now = timeProvider.GetUtcNow();
        return await db.OperationalWorkerHeartbeats.AsNoTracking().AnyAsync(item =>
            item.WorkerName == lease.WorkerName
            && item.InstanceId == lease.Owner
            && item.LeaseToken == lease.Token
            && item.LeaseExpiresAt > now, ct);
    }

    public Task<bool> RecordIterationStartedAsync(
        WorkerLeaseHandle lease,
        CancellationToken ct = default) => UpdateOwnedAsync(
        lease,
        PostgresWorkerLeaseSql.RecordStarted(lease, _options.LeaseDurationSeconds),
        (record, now) =>
        {
            record.LastIterationStartedAt = now;
            record.LastHeartbeatAt = now;
            record.CurrentStatus = "Running";
        },
        ct);

    public Task<bool> RecordResultAsync(
        WorkerLeaseHandle lease,
        AnalyticsMaintenanceResult result,
        CancellationToken ct = default)
    {
        var status = result.PartialFailure ? "Degraded" : "Healthy";
        var failureCode = Bounded(string.Join(',', result.FailureCodes), 160);
        return UpdateOwnedAsync(
            lease,
            PostgresWorkerLeaseSql.RecordResult(
                lease,
                result,
                status,
                failureCode,
                _options.LeaseDurationSeconds),
            (record, now) => ApplyResult(record, result, status, failureCode, now),
            ct);
    }

    public Task<bool> RecordFailureAsync(
        WorkerLeaseHandle lease,
        string failureCode,
        string status = "Failed",
        CancellationToken ct = default)
    {
        var safeStatus = status is "Failed" or "LeaseLost" ? status : "Failed";
        var safeCode = Bounded(failureCode, 160) ?? "analytics.worker.failed";
        const string summary = "The maintenance iteration did not complete.";
        return UpdateOwnedAsync(
            lease,
            PostgresWorkerLeaseSql.RecordFailure(
                lease,
                safeStatus,
                safeCode,
                summary,
                _options.LeaseDurationSeconds),
            (record, now) =>
            {
                record.LastIterationCompletedAt = now;
                record.LastFailureAt = now;
                record.LastFailureCode = safeCode;
                record.LastFailureSummary = summary;
                record.CurrentStatus = safeStatus;
            },
            ct);
    }

    public async Task<bool> AcquireOrRenewAsync(string workerName, CancellationToken ct = default)
    {
        if (_compatibilityLeases.TryGetValue(workerName, out var current)
            && await RenewAsync(current, ct))
            return true;
        var acquired = await TryAcquireAsync(workerName, ct);
        if (acquired is null) return false;
        _compatibilityLeases[workerName] = acquired;
        return true;
    }

    public async Task RecordSuccessAsync(
        string workerName,
        long processedCount,
        CancellationToken ct = default)
    {
        if (!_compatibilityLeases.TryGetValue(workerName, out var current)) return;
        await RecordResultAsync(current, AnalyticsMaintenanceResult.Empty with
        {
            OutboxProcessed = (int)Math.Min(int.MaxValue, Math.Max(0, processedCount))
        }, ct);
    }

    public async Task RecordFailureAsync(
        string workerName,
        string failureCode,
        CancellationToken ct = default)
    {
        if (!_compatibilityLeases.TryGetValue(workerName, out var current)) return;
        await RecordFailureAsync(current, failureCode, ct: ct);
    }

    private async Task<bool> UpdateOwnedAsync(
        WorkerLeaseHandle lease,
        FormattableString postgresSql,
        Action<OperationalWorkerHeartbeatRecord, DateTimeOffset> apply,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (db.Database.IsNpgsql())
            return await db.Database.ExecuteSqlInterpolatedAsync(postgresSql, ct) == 1;

        var now = timeProvider.GetUtcNow();
        var record = await OwnedAsync(db, lease, ct);
        if (record is null || record.LeaseExpiresAt <= now) return false;
        apply(record, now);
        record.LeaseExpiresAt = now.AddSeconds(_options.LeaseDurationSeconds);
        record.Revision++;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static void ApplyResult(
        OperationalWorkerHeartbeatRecord record,
        AnalyticsMaintenanceResult result,
        string status,
        string? failureCode,
        DateTimeOffset now)
    {
        record.LastIterationCompletedAt = now;
        if (status == "Healthy") record.LastSuccessfulIterationAt = now;
        else
        {
            record.LastDegradedIterationAt = now;
            record.LastFailureCode = failureCode;
            record.LastFailureSummary =
                "One or more maintenance components reported a partial failure.";
        }
        record.LastOutboxProcessed = result.OutboxProcessed;
        record.LastOutboxFailed = result.OutboxFailed;
        record.LastExportsCompleted = result.ExportsCompleted;
        record.LastExportsFailed = result.ExportsFailed;
        record.LastAggregateBucketsCompleted = result.AggregateBucketsCompleted;
        record.LastAggregateBucketsFailed = result.AggregateBucketsFailed;
        record.LastRetentionRowsRemoved = result.RetentionRowsRemoved;
        record.ProcessedCount += result.TotalProcessed;
        record.CumulativeProcessedCount += result.TotalProcessed;
        record.CurrentStatus = status;
        record.LastHeartbeatAt = now;
    }

    private static async Task<OperationalWorkerHeartbeatRecord?> OwnedAsync(
        ApplicationDbContext db,
        WorkerLeaseHandle lease,
        CancellationToken ct) =>
        await db.OperationalWorkerHeartbeats.SingleOrDefaultAsync(item =>
            item.WorkerName == lease.WorkerName
            && item.InstanceId == lease.Owner
            && item.LeaseToken == lease.Token, ct);

    private static string? Bounded(string? value, int maximum) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value[..Math.Min(value.Length, maximum)];

    private static string BuildInstanceId()
    {
        var configured = Environment.GetEnvironmentVariable("CONVOLAB_INSTANCE_ID")?.Trim();
        return string.IsNullOrWhiteSpace(configured)
            ? $"{Environment.MachineName}:{Environment.ProcessId}"
            : configured[..Math.Min(configured.Length, 160)];
    }

    private sealed class LeaseRow
    {
        public long LeaseToken { get; init; }
        public DateTimeOffset LeaseExpiresAt { get; init; }
    }
}
