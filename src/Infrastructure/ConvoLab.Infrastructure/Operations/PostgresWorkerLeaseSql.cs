namespace ConvoLab.Infrastructure.Operations;

internal static class PostgresWorkerLeaseSql
{
    internal static FormattableString AcquireOrRenew(
        string workerName,
        string instanceId,
        int leaseSeconds) => $"""
        WITH server_clock AS MATERIALIZED (
            SELECT clock_timestamp() AS value
        )
        INSERT INTO "OperationalWorkerHeartbeats" (
            "WorkerName", "InstanceId", "StartedAt", "LastHeartbeatAt",
            "CurrentStatus", "ProcessedCount", "LeaseExpiresAt", "Revision")
        SELECT {workerName}, {instanceId}, server_clock.value, server_clock.value,
            'Running', 0, server_clock.value + ({leaseSeconds} * interval '1 second'), 1
        FROM server_clock
        ON CONFLICT ("WorkerName") DO UPDATE SET
            "InstanceId" = EXCLUDED."InstanceId",
            "StartedAt" = CASE
                WHEN "OperationalWorkerHeartbeats"."InstanceId" = EXCLUDED."InstanceId"
                    THEN "OperationalWorkerHeartbeats"."StartedAt"
                ELSE EXCLUDED."StartedAt"
            END,
            "LastHeartbeatAt" = EXCLUDED."LastHeartbeatAt",
            "CurrentStatus" = 'Running',
            "LeaseExpiresAt" = EXCLUDED."LeaseExpiresAt",
            "Revision" = "OperationalWorkerHeartbeats"."Revision" + 1
        WHERE "OperationalWorkerHeartbeats"."InstanceId" = EXCLUDED."InstanceId"
           OR "OperationalWorkerHeartbeats"."LeaseExpiresAt" <= (SELECT value FROM server_clock);
        """;

    internal static FormattableString RecordSuccess(
        string workerName,
        string instanceId,
        long processedCount,
        int leaseSeconds) => $"""
        WITH server_clock AS MATERIALIZED (
            SELECT clock_timestamp() AS value
        )
        UPDATE "OperationalWorkerHeartbeats" AS worker
        SET "LastHeartbeatAt" = server_clock.value,
            "LastSuccessfulIterationAt" = server_clock.value,
            "LastFailureSummary" = NULL,
            "CurrentStatus" = 'Running',
            "ProcessedCount" = worker."ProcessedCount" + {processedCount},
            "LeaseExpiresAt" = server_clock.value + ({leaseSeconds} * interval '1 second'),
            "Revision" = worker."Revision" + 1
        FROM server_clock
        WHERE worker."WorkerName" = {workerName}
          AND worker."InstanceId" = {instanceId};
        """;

    internal static FormattableString RecordFailure(
        string workerName,
        string instanceId,
        string safeFailureCode,
        int leaseSeconds) => $"""
        WITH server_clock AS MATERIALIZED (
            SELECT clock_timestamp() AS value
        )
        UPDATE "OperationalWorkerHeartbeats" AS worker
        SET "LastHeartbeatAt" = server_clock.value,
            "LastFailureAt" = server_clock.value,
            "LastFailureSummary" = {safeFailureCode},
            "CurrentStatus" = 'Degraded',
            "LeaseExpiresAt" = server_clock.value + ({leaseSeconds} * interval '1 second'),
            "Revision" = worker."Revision" + 1
        FROM server_clock
        WHERE worker."WorkerName" = {workerName}
          AND worker."InstanceId" = {instanceId};
        """;
}
