# Analytics aggregation and recovery

Raw events are the source of truth. Hourly and daily UTC aggregates are derived, restart-safe projections.

Each aggregate has a deterministic key over organisation, workspace, environment, bucket, and nullable dimensions. Null dimensions are canonicalised before hashing so equivalent groups cannot create duplicate rows.

## Incremental processing

Each workspace and granularity has an aggregation checkpoint containing:

- dirty start and end;
- high watermark;
- last processed event;
- status and failure reason;
- last successful run and revision.

Outbox dispatch expands the dirty range when a new or late event arrives. The worker recomputes complete affected buckets, replaces their deterministic aggregate rows, and advances the checkpoint only after a successful transaction. A restart resumes from persisted checkpoint state.

Late events rebuild their affected hour/day. Duplicate events are rejected by event key and do not double-count. Operator-triggered rebuilds use the same dirty-range mechanism.

## Measures

Aggregates keep `EventCount` and `ExecutionCount` separately, plus simulation, evaluation, trace, replay, provider invocation/prevention, policy decision, plugin, token, classified cost, duration, and quality measures. The legacy `Executions` column remains only for migration compatibility and is not the v1 execution measure.

Default retention is 90 days for raw events and hourly aggregates, and 730 days for daily aggregates. Retention limits the granularity available for historical rebuilds but does not change the append-only workspace audit log.
