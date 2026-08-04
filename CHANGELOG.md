# Changelog

## In progress — alpha.15 Operational Foundation — Final Sign-Off

Active version metadata remains `1.0.0-alpha.14`; this is not an alpha.15 release declaration.

- Corrected Analytics readiness so fresh pending work stays Healthy while aged pending, failed outbox work, failed checkpoints, aggregation lag, and partial worker failures affect status through shared typed thresholds.
- Replaced raw required-secret scanning with active-scope effective-configuration validation and sanitized dependency evidence.
- Added continuous PostgreSQL-server-time lease renewal, fencing tokens, stale-owner rejection, and atomic retryable Analytics export claims.
- Persisted component-specific iteration results and removed the synthetic processed-per-iteration count.
- Added database-backed operational gauges, bounded provider-cost evidence, truthful OTLP dependency states, Telemetry Operations evidence, and cross-session safe-mode refresh.
- Expanded PostgreSQL, secret-store, safe-mode, Operations authorization, and Playwright acceptance while preserving alpha.14 Analytics reconciliation, isolation, and policy-denial guarantees.
- Entra, backup/restore, deployment promotion, supply-chain controls, and the final readiness report remain required later workstreams.

## 1.0.0-alpha.14 — 2026-07-28

- Completed effective Settings-driven runtime configuration and shared immutable snapshot attribution across simulation, policy, provider, evaluation, trace, and replay.
- Separated event and distinct terminal-execution measures and added category-specific usage, cost/budget, quality, governance, performance, and adoption semantics.
- Expanded trusted event coverage across authentication, selection, provider, policy, evaluation, trace, replay, plugin, and configuration activity.
- Closed event/correlation cost-field bypasses, including Production token redaction, and added real role-principal API security coverage.
- Completed matching dashboard/export filters, event detail/correlation drill-down, incremental dirty-range aggregation, reconciliation tooling, and measured 10k/100k PostgreSQL evidence.
- Added trusted runtime-environment attribution, server-generated correlation, immutable configuration snapshots, and explicit Alpha 13 backfill status.
- Added metadata-only Platform Analytics events with a transactional outbox, restart-safe aggregation/checkpoints, governed retention, and asynchronous sanitized CSV exports.
- Added workspace/environment analytics APIs with fixed RBAC, tenant isolation, actor redaction, and Production-cost restrictions.
- Added the lazy, accessible Studio Analytics workspace and eliminated stale environment state that previously required a refresh.
- Replaced the PostgreSQL migration-count assertion with known/applied/pending checks and added a real Alpha 13 upgrade/restart gate.
- Pinned Node 22.22.0 across Docker and CI and removed the unused pnpm lockfile.
- Upgraded the complete Studio to the restrained premium glass system and kept hamburger/close controls mobile-only.

## 1.0.0-alpha.13 — 2026-07-28

- Added governed Environment & Settings Management with scoped inheritance, typed validation, protected changes, secret references, audit history, and configuration import/export.
- Added the Studio settings console and workspace environment switcher.
- Bound workspace and organisation route identifiers to the authenticated tenant context, enforced environment ownership throughout Settings, and added adversarial guessed-ID isolation coverage.
- Replaced timestamp-based configuration snapshot identifiers with deterministic SHA-256 content revisions and normalized typed environment fallbacks.
- Added Docker readiness gating and browser recovery for transient session startup and stale deployment chunks.
- Updated the frontend to React Router 8.3.0 and retained the enforced bundle budget.

## 1.0.0-alpha.12 — 2026-07-22

- Added revocable opaque-cookie authentication, local password hashing, session rotation/revocation, antiforgery validation, login throttling, and hardened response headers.
- Added organisations, workspaces, memberships, fixed RBAC permissions, invitations, scoped one-time service credentials, and append-only attributable audit events.
- Added mandatory workspace ownership and deterministic alpha.11 data backfill across capability roots, plus platform-owned immutable built-in plugins.
- Added protected Studio routing, login, workspace selection/switching, member and service-account administration, role guidance, audit inspection, and restricted states.
- Added migration, isolation, authentication, permission, frontend, interaction, and bundle acceptance coverage. Workspace/IAM remains active until PostgreSQL, restart, Docker, and browser security acceptance complete.

All notable changes to ConvoLab are recorded here. Versions follow Semantic Versioning while the product remains pre-release.

## 1.0.0-alpha.11 — 2026-07-22

### Stabilized

- Lazy route delivery and capability-owned CSS chunks with enforceable raw and gzip budgets.
- Shared loading, error, empty, offline, and mutation feedback patterns.
- PostgreSQL fresh-install, legacy-upgrade, reconnect, Docker restart, and cross-capability acceptance gates.
- Canonical Evaluation APIs with retained singular compatibility contracts.
- Policy and Plugin lifecycle route validation and production dependency-graph auditing.
- Playwright route, interaction, responsive-navigation, and recoverable-error smoke coverage.

### Removed

- Unused production registrations backed by no-op Conversation, Prompt, AI, Evaluation, Trace, and legacy Workflow services.

### Remaining before beta

- Workspace and tenant isolation, identity, authorization, environment governance, and managed secrets.
