# Operations Center

`/operations` is lazy-loaded and available only to Platform Administrators. The API policy is enforced independently of navigation visibility. The Center contains Overview, Health, Workers, Analytics pipeline, Authentication, Secret providers, Backups, Build and deployment, Telemetry, and Safe mode panels.

The lightweight status endpoint is polled every 30 seconds. Detailed evidence loads on demand and all `operations` query keys are invalidated after safe-mode mutations. The Analytics panel shows pending/failed counts and ages, dirty/failed checkpoints, aggregation lag, last successful dispatch/aggregation, status, and applied typed thresholds. The worker panel targets `analytics-maintenance` explicitly and reports lease owner/token plus component-specific iteration counts.

The Authentication panel renders Entra/identity and break-glass evidence together: mode and enablement, sanitized tenant/client configuration state, Entra dependency state/last validation/safe failure code, aggregate external identity, linked-active-user, recent external-login and active-session counts, plus aggregate break-glass availability, state, recent uses/failures, and last success. All counts and the last-use value are database aggregates. Tenant IDs, authorities, secret references, identity claims, account identities, and credential material are excluded.

Backups are `NotConfigured`; no RPO, RTO, backup age, verification date, or restore claim is invented. Dependency labels remain distinct: `NotConfigured`, `Configured`, `StubValidated`, `LiveValidated`, `Unavailable`, and `Degraded`.

Routine status, worker, Analytics, authentication, secret-provider, backup, build, and telemetry reads use structured logs, counters, and traces rather than persistent audit rows. Safe-mode mutations, explicit live validation, sensitive evidence access, manual worker intervention, and future backup/restore actions are audited. Explicit readiness evidence access is audited; it is never the polled endpoint.
