# Analytics exports

CSV export is asynchronous and restart-safe. Creation stores the exact validated filter set and caller field visibility. A background worker generates content, records row count, size, checksum and completion/failure state, and can resume pending work after restart.

Supported filters match the dashboard:

```text
EnvironmentId, From, To, Provider, Model, Capability, Outcome,
ConfigurationRevision, Prompt, Workflow, KnowledgeCollection,
ActorId, EventType, CostType
```

Actor filtering requires actor permission. Periods are UTC half-open intervals `[from,to)` and exports are limited to 90 days. Invalid or unsupported filters produce structured validation errors rather than being ignored.

CSV output contains safe operational metadata only. Cost, token, actor, provider, and source columns are included only when authorised. Formula-leading cells are prefixed safely. Exports stop at 100,000 rows or 25 MB.

`ExpiresAt` comes from the effective `retention.analytics_export_days` setting (default seven days). The same value controls cleanup and user-visible expiry. Creation and download create attributable audit/analytics evidence.
