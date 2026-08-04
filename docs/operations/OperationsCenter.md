# Operations Center

`/operations` is lazy-loaded and available only to Platform Administrators. The API policy is enforced independently of navigation visibility. The Center contains Overview, Health, Workers, Analytics pipeline, Authentication, Secret providers, Backups, Build and deployment, Telemetry, and Safe mode panels.

The lightweight status endpoint is polled every 30 seconds. Detailed evidence loads on demand and all `operations` query keys are invalidated after safe-mode mutations. The Analytics panel shows pending/failed counts and ages, dirty/failed checkpoints, aggregation lag, last successful dispatch/aggregation, status, and applied typed thresholds. The worker panel targets `analytics-maintenance` explicitly and reports lease owner/token plus component-specific iteration counts.

Backups are `NotConfigured`; no RPO, RTO, backup age, verification date, or restore claim is invented. Dependency labels remain distinct: `NotConfigured`, `Configured`, `StubValidated`, `LiveValidated`, `Unavailable`, and `Degraded`.

Routine status, worker, Analytics, authentication, secret-provider, backup, build, and telemetry reads use structured logs, counters, and traces rather than persistent audit rows. Safe-mode mutations, explicit live validation, sensitive evidence access, manual worker intervention, and future backup/restore actions are audited. Explicit readiness evidence access is audited; it is never the polled endpoint.

