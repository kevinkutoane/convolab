# Changelog

## 1.0.0-alpha.16 — 2026-08-19

### Backup, Restore & Disaster Recovery v1
- Added active backup orchestration for PostgreSQL state, Knowledge documents, and Data Protection key rings.
- Added authenticated chunked AES-256-GCM encryption with versioned envelopes (`CVLB_GCM_V1`), authenticated AAD metadata, and per-chunk tag verification.
- Added strict `ISecretStore`-backed key resolution with zero insecure fallbacks.
- Added asynchronous restore operations (`POST /api/operations/backups/{id}/restore`) with explicit destructive safeguards.
- Added fail-closed `pg_restore` handling with explicit benign clean warning allow-listing.
- Added deep `RecoveryVerifier` performing automated database, Data Protection, and strict document reconciliation (0 missing / 0 orphans).
- Added isolated disaster recovery profile (`docker-compose.recovery.yml`) and operational tooling scripts (`tools/operations/`).
- Overhauled the Operations Center Studio UI (`/operations`) with clean segmented tabs (Overview, Backup & DR, IAM, Telemetry, Build).

## 1.0.0-alpha.15 — 2026-08-18

### Microsoft Entra ID & Hybrid Authentication
- Added standards-based Microsoft Entra authentication (OIDC with authorization code flow & PKCE).
- Added strongly typed Local, Entra, and Hybrid authentication modes with a safe public options endpoint and mode-aware Studio login.
- Added external identity persistence using provider/issuer/subject tuples.
- Added secure invitation-based first-login linking.
- Removed dependency on `email_verified` claim for invitation linking while enforcing tenant authority validation.
- Added tenant-aware identity validation, OIDC state, nonce, and correlation validation.
- Added opaque application sessions persisted prior to issuing session cookies.
- Added external logout and identity-administration session revocation.

### Security & Break-Glass Hardening
- Hardened break-glass emergency authentication with dedicated failure protection and concurrency control.
- Added dedicated break-glass rate limiting.
- Added temporary account-level break-glass lockout.
- Added safe framework-level Entra failure evidence.
- Added failure-event deduplication.
- Preserved sensitive-token/credential protections (no raw tokens, codes, or secrets in persistence/logs).

### Operations & Analytics
- Restored and expanded authentication evidence in Operations Center.
- Added external login success/failure evidence.
- Added break-glass operational evidence.
- Added trusted Analytics failure-event mapping.
- Added database-backed operational gauges, bounded provider-cost evidence, truthful OTLP dependency states, and Telemetry Operations evidence.
- Replaced raw required-secret scanning with active-scope effective-configuration validation and sanitized dependency evidence.
- Added continuous PostgreSQL-server-time lease renewal, fencing tokens, stale-owner rejection, and atomic retryable Analytics export claims.

### Verification
- Completed authentication regression coverage.
- Completed Playwright lifecycle verification.
- Verified restart persistence.
- Verified Docker rebuild behaviour.

### Validation Status
- Microsoft Entra live tenant validation remains pending (`Not executed`).
- Deterministic/stub provider validation is complete (`StubValidated`).

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
