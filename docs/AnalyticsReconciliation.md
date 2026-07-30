# Analytics reconciliation

Reconciliation starts from a `SourceExecutionId`, not from an event count.

For a governed execution:

1. Load the simulation/replay source record.
2. Match policy, provider, evaluation, trace, and replay evidence by source execution, source resource, and correlation.
3. Confirm organisation, workspace, environment, actor, configuration revision, and correlation are identical.
4. Compare provider tokens and classified cost with the source execution metrics.
5. Confirm terminal events contribute exactly one distinct execution.
6. Confirm the affected hourly and daily bucket totals.
7. Compare API/dashboard values for the same `[from,to)` filter.

A denied execution must contain `ProviderInvocationPrevented`, zero provider tokens/cost, no intelligence provider execution record, a denied policy outcome, and a failed/denied terminal execution event.

The repository provides read-only operator queries in `tools/analytics-reconciliation.sql`. They inventory events, show attributed timelines, verify denial invariants, compare aggregate totals, and scan analytics/export content for known sensitive test phrases. Run them against the Docker PostgreSQL database:

```powershell
Get-Content -Raw tools/analytics-reconciliation.sql |
  docker exec -i convolab-db psql -U postgres -d convolab
```

Legacy Alpha 13 evidence has no trustworthy original configuration. It reconciles only to the workspace default environment and is explicitly marked as backfilled/configuration unavailable.
