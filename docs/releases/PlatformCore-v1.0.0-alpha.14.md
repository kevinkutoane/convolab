# ConvoLab Platform and Studio v1.0.0-alpha.14

Alpha 14 introduces Platform Analytics v1 and closes the runtime-environment refresh weakness.

## Delivered

- Trusted server-generated execution correlation and validated runtime-environment attribution.
- Immutable secret-free configuration snapshots and associated execution attribution.
- Append-only safe analytics events with a transactional, idempotent PostgreSQL outbox.
- Restart-safe hourly/daily aggregation, checkpoints, retention, and asynchronous sanitized CSV exports.
- Workspace/environment analytics APIs with role, tenant, actor, and Production-cost enforcement.
- Lazy Studio Analytics views for overview, usage, cost/budget, quality, governance, performance, adoption, events, and exports.
- Alpha 13 PostgreSQL upgrade/backfill coverage and migration discovery without hardcoded counts.
- Exact Node 22.22.0 Docker/CI pinning and npm-only dependency governance.
- Effective Settings-driven provider/evaluation/policy/plugin runtime configuration with one shared immutable revision.
- Correct event-versus-execution measures and dedicated usage, cost/budget, quality, governance, performance, and adoption metrics.
- Full dashboard/export filter parity, event/correlation drill-down, field-level actor/cost/token redaction, and Production bypass coverage.
- Premium restrained-glass Studio surfaces with adaptive workspaces and mobile-only hamburger/close controls.
- Reproducible reconciliation and measured 10k/100k PostgreSQL performance evidence.

## Privacy boundary

Analytics contains operational metadata only. Prompts, messages, provider request/response bodies, trace artifacts, secrets, and customer content are excluded from events and exports.

## Compatibility

Alpha 13 execution clients remain compatible. If the runtime-environment header is absent, the API resolves the active default environment. Historical evidence backfilled during upgrade remains explicitly labelled rather than presented as original attribution.
