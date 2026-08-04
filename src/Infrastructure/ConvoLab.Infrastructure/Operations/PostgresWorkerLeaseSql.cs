using ConvoLab.Application.Operations;

namespace ConvoLab.Infrastructure.Operations;

internal static class PostgresWorkerLeaseSql
{
    internal static FormattableString Acquire(
        string workerName,
        string instanceId,
        int leaseSeconds) => $"""
        WITH server_time AS MATERIALIZED (
            SELECT clock_timestamp() AS now
        )
        INSERT INTO "OperationalWorkerHeartbeats" (
            "WorkerName", "InstanceId", "StartedAt", "LastHeartbeatAt",
            "CurrentStatus", "ProcessedCount", "CumulativeProcessedCount",
            "LeaseToken", "LeaseExpiresAt", "Revision")
        SELECT {workerName}, {instanceId}, server_time.now, server_time.now,
            'Running', 0, 0, 1,
            server_time.now + ({leaseSeconds} * interval '1 second'), 1
        FROM server_time
        ON CONFLICT ("WorkerName") DO UPDATE SET
            "InstanceId" = EXCLUDED."InstanceId",
            "StartedAt" = CASE
                WHEN "OperationalWorkerHeartbeats"."InstanceId" = EXCLUDED."InstanceId"
                    THEN "OperationalWorkerHeartbeats"."StartedAt"
                ELSE EXCLUDED."StartedAt"
            END,
            "LastHeartbeatAt" = EXCLUDED."LastHeartbeatAt",
            "CurrentStatus" = 'Running',
            "LeaseToken" = "OperationalWorkerHeartbeats"."LeaseToken" + 1,
            "LeaseExpiresAt" = EXCLUDED."LeaseExpiresAt",
            "Revision" = "OperationalWorkerHeartbeats"."Revision" + 1
        WHERE "OperationalWorkerHeartbeats"."InstanceId" = EXCLUDED."InstanceId"
           OR "OperationalWorkerHeartbeats"."LeaseExpiresAt" <= (SELECT now FROM server_time)
        RETURNING "LeaseToken", "LeaseExpiresAt";
        """;

    internal static FormattableString Renew(
        string workerName,
        string instanceId,
        long leaseToken,
        int leaseSeconds) => $"""
        WITH server_time AS MATERIALIZED (
            SELECT clock_timestamp() AS now
        )
        UPDATE "OperationalWorkerHeartbeats" AS worker
        SET "LastHeartbeatAt" = server_time.now,
            "LeaseExpiresAt" = server_time.now + ({leaseSeconds} * interval '1 second'),
            "Revision" = worker."Revision" + 1
        FROM server_time
        WHERE worker."WorkerName" = {workerName}
          AND worker."InstanceId" = {instanceId}
          AND worker."LeaseToken" = {leaseToken}
          AND worker."LeaseExpiresAt" > server_time.now;
        """;

    internal static FormattableString IsOwned(
        string workerName,
        string instanceId,
        long leaseToken) => $"""
        WITH server_time AS MATERIALIZED (
            SELECT clock_timestamp() AS now
        )
        SELECT EXISTS (
            SELECT 1
            FROM "OperationalWorkerHeartbeats" AS worker, server_time
            WHERE worker."WorkerName" = {workerName}
              AND worker."InstanceId" = {instanceId}
              AND worker."LeaseToken" = {leaseToken}
              AND worker."LeaseExpiresAt" > server_time.now
        ) AS "Value"
        """;

    internal static FormattableString RecordStarted(
        WorkerLeaseHandle lease,
        int leaseSeconds) => $"""
        WITH server_time AS MATERIALIZED (
            SELECT clock_timestamp() AS now
        )
        UPDATE "OperationalWorkerHeartbeats" AS worker
        SET "LastIterationStartedAt" = server_time.now,
            "LastHeartbeatAt" = server_time.now,
            "CurrentStatus" = 'Running',
            "LeaseExpiresAt" = server_time.now + ({leaseSeconds} * interval '1 second'),
            "Revision" = worker."Revision" + 1
        FROM server_time
        WHERE worker."WorkerName" = {lease.WorkerName}
          AND worker."InstanceId" = {lease.Owner}
          AND worker."LeaseToken" = {lease.Token}
          AND worker."LeaseExpiresAt" > server_time.now;
        """;

    internal static FormattableString RecordResult(
        WorkerLeaseHandle lease,
        AnalyticsMaintenanceResult result,
        string status,
        string? failureCode,
        int leaseSeconds) => $"""
        WITH server_time AS MATERIALIZED (
            SELECT clock_timestamp() AS now
        )
        UPDATE "OperationalWorkerHeartbeats" AS worker
        SET "LastIterationCompletedAt" = server_time.now,
            "LastSuccessfulIterationAt" = CASE WHEN {status} = 'Healthy'
                THEN server_time.now ELSE worker."LastSuccessfulIterationAt" END,
            "LastDegradedIterationAt" = CASE WHEN {status} = 'Degraded'
                THEN server_time.now ELSE worker."LastDegradedIterationAt" END,
            "LastFailureCode" = CASE WHEN {status} = 'Degraded'
                THEN {failureCode} ELSE worker."LastFailureCode" END,
            "LastFailureSummary" = CASE WHEN {status} = 'Degraded'
                THEN 'One or more maintenance components reported a partial failure.'
                ELSE worker."LastFailureSummary" END,
            "LastOutboxProcessed" = {result.OutboxProcessed},
            "LastOutboxFailed" = {result.OutboxFailed},
            "LastExportsCompleted" = {result.ExportsCompleted},
            "LastExportsFailed" = {result.ExportsFailed},
            "LastAggregateBucketsCompleted" = {result.AggregateBucketsCompleted},
            "LastAggregateBucketsFailed" = {result.AggregateBucketsFailed},
            "LastRetentionRowsRemoved" = {result.RetentionRowsRemoved},
            "ProcessedCount" = worker."ProcessedCount" + {result.TotalProcessed},
            "CumulativeProcessedCount" = worker."CumulativeProcessedCount" + {result.TotalProcessed},
            "CurrentStatus" = {status},
            "LastHeartbeatAt" = server_time.now,
            "LeaseExpiresAt" = server_time.now + ({leaseSeconds} * interval '1 second'),
            "Revision" = worker."Revision" + 1
        FROM server_time
        WHERE worker."WorkerName" = {lease.WorkerName}
          AND worker."InstanceId" = {lease.Owner}
          AND worker."LeaseToken" = {lease.Token}
          AND worker."LeaseExpiresAt" > server_time.now;
        """;

    internal static FormattableString RecordFailure(
        WorkerLeaseHandle lease,
        string status,
        string failureCode,
        string failureSummary,
        int leaseSeconds) => $"""
        WITH server_time AS MATERIALIZED (
            SELECT clock_timestamp() AS now
        )
        UPDATE "OperationalWorkerHeartbeats" AS worker
        SET "LastIterationCompletedAt" = server_time.now,
            "LastFailureAt" = server_time.now,
            "LastFailureCode" = {failureCode},
            "LastFailureSummary" = {failureSummary},
            "CurrentStatus" = {status},
            "LastHeartbeatAt" = server_time.now,
            "LeaseExpiresAt" = server_time.now + ({leaseSeconds} * interval '1 second'),
            "Revision" = worker."Revision" + 1
        FROM server_time
        WHERE worker."WorkerName" = {lease.WorkerName}
          AND worker."InstanceId" = {lease.Owner}
          AND worker."LeaseToken" = {lease.Token}
          AND worker."LeaseExpiresAt" > server_time.now;
        """;
}
