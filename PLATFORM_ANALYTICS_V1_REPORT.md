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

## Completion evidence — 30 July 2026

- Dockerized .NET 8 solution: 297 passed, 0 failed
  - Domain: 186
  - Application: 36
  - PostgreSQL infrastructure: 41
  - API/security: 18
  - Architecture: 16
- PostgreSQL: fresh migration, Alpha 13 upgrade/backfill, Alpha 14 completion upgrade/preservation, reconnect, restart, idempotency, and no-pending-migration gates passed.
- Pinned Node `22.22.0`: install, lint, frontend contracts, 34-file interaction audit, production build, visual baseline, and dependency audit passed.
- npm audit: 0 vulnerabilities.
- Bundle gate: initial graph and all 19 lazy routes passed; initial JavaScript is 277 kB raw / 88.7 kB gzip and the Analytics aggregate is 99.3 kB raw / 35.4 kB gzip.
- Browser regression: functional and dark/light visual suites passed; environment changes and session bootstrap require no manual refresh.
- Docker production builds: API and Studio passed.
- Docker Compose: database, API, and Studio containers healthy; API restart preserved 14 migrations plus simulation, analytics-event, and execution-attribution data.
- Readiness is `Healthy`; database, migrations/schema, document storage, local deterministic provider, and workspace identity checks pass.
- PostgreSQL 16.14 transaction-local 100,000-event evidence measured overview at 123.7 ms, cost at 35.8 ms, quality at 35.2 ms, governance at 45.4 ms, correlation at 5.3 ms, filtered export materialisation at 56.7 ms, and one-day late repair at 41.0 ms.

See `FUNCTIONAL_PLATFORM_ANALYTICS_V1_REPORT.md` for reconciliation, security, performance, bundle, and known-limitation evidence.
