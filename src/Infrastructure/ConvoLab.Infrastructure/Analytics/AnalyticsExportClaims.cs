using System.Data;
using ConvoLab.Application.Operations;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.Operations;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.Analytics;

internal static class AnalyticsExportClaims
{
    internal static FormattableString Statement(
        WorkerLeaseHandle workerLease,
        int leaseDurationSeconds,
        int maximumBatchSize) => $"""
        WITH server_time AS MATERIALIZED (
            SELECT clock_timestamp() AS now
        ), valid_worker AS MATERIALIZED (
            SELECT 1
            FROM "OperationalWorkerHeartbeats" AS worker, server_time
            WHERE worker."WorkerName" = {workerLease.WorkerName}
              AND worker."InstanceId" = {workerLease.Owner}
              AND worker."LeaseToken" = {workerLease.Token}
              AND worker."LeaseExpiresAt" > server_time.now
        ), candidates AS MATERIALIZED (
            SELECT export."Id"
            FROM "AnalyticsExports" AS export, server_time, valid_worker
            WHERE export."Status" = 'Pending'
               OR (export."Status" = 'Processing'
                   AND export."ProcessingStartedAt" <= server_time.now
                       - ({leaseDurationSeconds} * interval '1 second'))
            ORDER BY export."CreatedAt"
            FOR UPDATE OF export SKIP LOCKED
            LIMIT {maximumBatchSize}
        )
        UPDATE "AnalyticsExports" AS export
        SET "Status" = 'Processing',
            "ProcessingOwner" = {workerLease.Owner},
            "ProcessingLeaseToken" = {workerLease.Token},
            "ProcessingStartedAt" = server_time.now,
            "AttemptCount" = export."AttemptCount" + 1,
            "FailureReason" = NULL
        FROM candidates, server_time
        WHERE export."Id" = candidates."Id"
        RETURNING export."Id";
        """;

    internal static async Task<List<AnalyticsExportRecord>> ClaimAsync(
        ApplicationDbContext db,
        WorkerLeaseHandle workerLease,
        int leaseDurationSeconds,
        int maximumBatchSize,
        CancellationToken ct)
    {
        var statement = Statement(workerLease, leaseDurationSeconds, maximumBatchSize);
        var connection = db.Database.GetDbConnection();
        var closeAfter = connection.State != ConnectionState.Open;
        if (closeAfter) await db.Database.OpenConnectionAsync(ct);
        var ids = new List<Guid>();
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
            while (await reader.ReadAsync(ct)) ids.Add(reader.GetGuid(0));
        }
        finally
        {
            if (closeAfter) await db.Database.CloseConnectionAsync();
        }

        if (ids.Count == 0) return [];
        return await db.AnalyticsExports.AsNoTracking()
            .Where(item => ids.Contains(item.Id)
                && item.Status == "Processing"
                && item.ProcessingOwner == workerLease.Owner
                && item.ProcessingLeaseToken == workerLease.Token)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(ct);
    }
}
