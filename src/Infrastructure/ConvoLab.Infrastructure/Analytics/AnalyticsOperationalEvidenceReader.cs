using System.Data.Common;
using System.Globalization;
using ConvoLab.Application.Operations;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.Analytics;

public sealed class AnalyticsOperationalEvidenceReader(
    ApplicationDbContext db) : IAnalyticsOperationalEvidenceReader
{
    public async Task<AnalyticsPipelineEvidence> ReadAsync(CancellationToken ct = default)
    {
        var pendingCount = await db.AnalyticsOutbox.AsNoTracking()
            .CountAsync(item => item.Status == "Pending", ct);
        var failedCount = await db.AnalyticsOutbox.AsNoTracking()
            .CountAsync(item => item.Status == "Failed", ct);
        var dirtyCount = await db.AnalyticsAggregationCheckpoints.AsNoTracking()
            .CountAsync(item => item.DirtyFromUtc != null, ct);
        var failedCheckpointCount = await db.AnalyticsAggregationCheckpoints.AsNoTracking()
            .CountAsync(item => item.Status == "Failed", ct);

        var now = await ReadDateAsync(CurrentTimeSql(), ct) ?? DateTimeOffset.UtcNow;
        var oldestPending = await ReadDateAsync("""
            SELECT MIN("CreatedAt") FROM "AnalyticsOutbox" WHERE "Status" = 'Pending'
            """, ct);
        var oldestFailed = await ReadDateAsync("""
            SELECT MIN("CreatedAt") FROM "AnalyticsOutbox" WHERE "Status" = 'Failed'
            """, ct);
        var oldestDirty = await ReadDateAsync("""
            SELECT MIN(COALESCE("DirtyFromUtc", "UpdatedAt"))
            FROM "AnalyticsAggregationCheckpoints"
            WHERE "DirtyFromUtc" IS NOT NULL OR "Status" = 'Failed'
            """, ct);
        var lastAggregation = await ReadDateAsync("""
            SELECT MAX("LastSuccessfulRunAt") FROM "AnalyticsAggregationCheckpoints"
            """, ct);
        var lastDispatch = await ReadDateAsync("""
            SELECT MAX("ProcessedAt") FROM "AnalyticsOutbox" WHERE "Status" = 'Processed'
            """, ct);

        return new(
            pendingCount,
            failedCount,
            Age(now, oldestPending),
            Age(now, oldestFailed),
            dirtyCount,
            failedCheckpointCount,
            Age(now, oldestDirty) ?? 0,
            lastAggregation,
            lastDispatch);
    }

    private string CurrentTimeSql() => db.Database.IsNpgsql()
        ? "SELECT clock_timestamp()"
        : "SELECT CURRENT_TIMESTAMP";

    private async Task<DateTimeOffset?> ReadDateAsync(string sql, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        await using DbCommand command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync(ct);
        return value switch
        {
            null or DBNull => null,
            DateTimeOffset timestamp => timestamp,
            DateTime timestamp => new DateTimeOffset(
                DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)),
            string timestamp when DateTimeOffset.TryParse(
                timestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var parsed) => parsed,
            _ => throw new InvalidOperationException(
                "The database returned an unsupported operational timestamp value.")
        };
    }

    private static double? Age(DateTimeOffset now, DateTimeOffset? timestamp) =>
        timestamp.HasValue
            ? Math.Max(0, (now - timestamp.Value).TotalSeconds)
            : null;
}
