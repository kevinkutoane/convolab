using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConvoLab.Infrastructure.Operations;

public interface IOperationalWorkerLease
{
    string InstanceId { get; }
    Task<bool> AcquireOrRenewAsync(string workerName, CancellationToken ct = default);
    Task RecordSuccessAsync(string workerName, long processedCount, CancellationToken ct = default);
    Task RecordFailureAsync(string workerName, string failureCode, CancellationToken ct = default);
}

public sealed class OperationalWorkerLease(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : IOperationalWorkerLease
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(120);
    public string InstanceId { get; } = BuildInstanceId();

    public async Task<bool> AcquireOrRenewAsync(string workerName, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (db.Database.IsNpgsql())
        {
            var seconds = (int)LeaseDuration.TotalSeconds;
            var affected = await db.Database.ExecuteSqlInterpolatedAsync(
                PostgresWorkerLeaseSql.AcquireOrRenew(workerName, InstanceId, seconds), ct);
            return affected == 1;
        }

        var now = timeProvider.GetUtcNow();
        var record = await db.OperationalWorkerHeartbeats
            .SingleOrDefaultAsync(item => item.WorkerName == workerName, ct);
        if (record is not null && record.InstanceId != InstanceId && record.LeaseExpiresAt > now)
            return false;
        if (record is null)
        {
            record = new OperationalWorkerHeartbeatRecord
            {
                WorkerName = workerName,
                InstanceId = InstanceId,
                StartedAt = now,
                ProcessedCount = 0,
                Revision = 1
            };
            db.OperationalWorkerHeartbeats.Add(record);
        }
        else
        {
            if (record.InstanceId != InstanceId) record.StartedAt = now;
            record.InstanceId = InstanceId;
            record.Revision++;
        }
        record.LastHeartbeatAt = now;
        record.LeaseExpiresAt = now.Add(LeaseDuration);
        record.CurrentStatus = "Running";
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task RecordSuccessAsync(
        string workerName,
        long processedCount,
        CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (db.Database.IsNpgsql())
        {
            var seconds = (int)LeaseDuration.TotalSeconds;
            await db.Database.ExecuteSqlInterpolatedAsync(
                PostgresWorkerLeaseSql.RecordSuccess(
                    workerName, InstanceId, processedCount, seconds), ct);
            return;
        }

        var now = timeProvider.GetUtcNow();
        var record = await OwnedAsync(db, workerName, ct);
        if (record is null) return;
        record.LastHeartbeatAt = now;
        record.LastSuccessfulIterationAt = now;
        record.LastFailureSummary = null;
        record.CurrentStatus = "Running";
        record.ProcessedCount += processedCount;
        record.LeaseExpiresAt = now.Add(LeaseDuration);
        record.Revision++;
        await db.SaveChangesAsync(ct);
    }

    public async Task RecordFailureAsync(
        string workerName,
        string failureCode,
        CancellationToken ct = default)
    {
        var safeFailureCode = failureCode.Length > 160 ? failureCode[..160] : failureCode;
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (db.Database.IsNpgsql())
        {
            var seconds = (int)LeaseDuration.TotalSeconds;
            await db.Database.ExecuteSqlInterpolatedAsync(
                PostgresWorkerLeaseSql.RecordFailure(
                    workerName, InstanceId, safeFailureCode, seconds), ct);
            return;
        }

        var now = timeProvider.GetUtcNow();
        var record = await OwnedAsync(db, workerName, ct);
        if (record is null) return;
        record.LastHeartbeatAt = now;
        record.LastFailureAt = now;
        record.LastFailureSummary = safeFailureCode;
        record.CurrentStatus = "Degraded";
        record.LeaseExpiresAt = now.Add(LeaseDuration);
        record.Revision++;
        await db.SaveChangesAsync(ct);
    }

    private async Task<OperationalWorkerHeartbeatRecord?> OwnedAsync(
        ApplicationDbContext db,
        string workerName,
        CancellationToken ct) =>
        await db.OperationalWorkerHeartbeats.SingleOrDefaultAsync(
            item => item.WorkerName == workerName && item.InstanceId == InstanceId, ct);

    private static string BuildInstanceId()
    {
        var configured = Environment.GetEnvironmentVariable("CONVOLAB_INSTANCE_ID")?.Trim();
        return string.IsNullOrWhiteSpace(configured)
            ? $"{Environment.MachineName}:{Environment.ProcessId}"
            : configured[..Math.Min(configured.Length, 160)];
    }
}
