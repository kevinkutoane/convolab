# Platform Analytics v1 — Release Evidence

Target: `v1.0.0-alpha.14`

## Implemented

- Runtime request context and validated environment selection
- Content-addressed configuration snapshots
- Execution attribution and Alpha 13 backfill
- Transactional analytics outbox and append-only event store
- Hourly/daily aggregates, checkpoints, retention, and exports
- Permission-filtered analytics API
- Lazy accessible Studio Analytics workspace
- Exact Node toolchain policy

## Release evidence — 28 July 2026

- Dockerized .NET 8 solution: 284 passed, 0 failed
  - Domain: 177
  - Application: 36
  - PostgreSQL infrastructure: 39
  - API/security: 16
  - Architecture: 16
- PostgreSQL: fresh migration, Alpha 13 upgrade/backfill, reconnect, idempotency, and no-pending-migration gates passed.
- Pinned Node `22.22.0`: install, lint, frontend contracts, 33-file interaction audit, production build, baseline audit, and dependency audit passed.
- npm audit: 0 vulnerabilities.
- Bundle gate: initial graph and all 19 lazy routes passed; Analytics route JavaScript is 13.64 kB raw / 4.25 kB gzip.
- Browser regression: transient session bootstrap recovered with one document navigation and no manual page refresh.
- Docker production builds: API and Studio passed.
- Docker Compose: database, API, and Studio containers healthy; database and API restart preserved 13 migrations, 2 simulation records, and 53 execution attributions.
- Readiness status is `Degraded` only because Gemini is intentionally unconfigured; database, document storage, deterministic provider, and workspace identity checks are healthy.
