# Master Checklist Status — v1.0.0-alpha.14

The 22 July 2026 PDF remains the product backlog source, with these stabilization corrections:

- Plugin Center Docker acceptance, GitHub publication, backend/frontend CI, Docker builds, structured logging, correlation IDs, OpenTelemetry instrumentation, and health endpoints are complete.
- Policy Center, Evaluation Studio, Trace Explorer, Replay Studio, and Plugin Center are complete functional v1 capabilities.
- Route-level lazy loading, capability CSS chunks, bundle budgets, Playwright smoke coverage, PostgreSQL upgrade tests, restart persistence, and placeholder audits are release gates for alpha.11.
- Workspace, Identity and Access Control v1 is implemented and remains active until its complete security, PostgreSQL, restart, Docker, and browser acceptance suite passes.
- Opaque sessions, local credentials, organisations, workspaces, memberships, fixed RBAC, service identities, ownership backfill, sensitive-trace audit, and protected Studio routes are implemented.
- Environment and Secret Management follows Workspace/IAM and remains required for beta.
- Platform Analytics v1 includes trusted runtime attribution, immutable configuration snapshots, transactional outbox delivery, restart-safe aggregation and exports, permission-filtered APIs, and the lazy-loaded Studio workspace.
- The Analytics completion sprint now uses effective persisted Settings at runtime, separates events from executions, covers the governed execution loop, enforces field-level cost/actor/token security, provides category-specific metrics and drill-down, and includes 10k/100k PostgreSQL evidence.
- Premium glass Studio acceptance includes roomy adaptive workspaces and desktop-only removal of the hamburger, close control, and mobile backdrop.
