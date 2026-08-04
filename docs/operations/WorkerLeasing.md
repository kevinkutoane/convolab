# Analytics worker leasing

The Production Analytics worker uses PostgreSQL server time for acquisition, heartbeat, renewal, expiry, takeover, iteration evidence, and export claims. Each statement evaluates `clock_timestamp()` exactly once in a materialized `server_time` CTE and reuses it. Application time is not authoritative for Production lease decisions.

Defaults bind from `AnalyticsWorker`: 120-second lease, 30-second renewal interval, 10-second poll, maximum batch size 100, and two tolerated renewal failures. Startup validation requires renewal below lease duration and all values within safe bounds.

Every acquisition advances a monotonically increasing fencing token. A dedicated linked renewal loop runs for the full maintenance iteration. Renewal, ownership checks, final success/failure evidence, and export completion require worker name, owner, token, and an unexpired server-derived lease. A stale owner cannot record successful counts.

Exports are claimed atomically with `FOR UPDATE SKIP LOCKED` and `UPDATE … RETURNING`. Claims persist owner, lease token, server-derived start time, and attempt count, and require the current worker fencing token. Abandoned `Processing` items become eligible only after the configured lease duration.

Iteration states are `Starting`, `Running`, `Healthy`, `Degraded`, `Failed`, `LeaseLost`, and `Stopped`. Persisted evidence separates outbox processed/failed, exports completed/failed, aggregate buckets completed/failed, retention rows removed, and cumulative actual work. Partial component failures produce `Degraded`; the former constant processed count has been removed.

