# ConvoLab Platform and Studio v1.0.0-alpha.13

Alpha.13 introduces Environment & Settings Management v1 — the governed configuration control plane — over the alpha.12 Workspace/IAM security boundary.

## Delivered

- Runtime environments as first-class workspace resources with lifecycle (Active/Suspended/Archived), default designation, optimistic-concurrency revisions, and archived immutability.
- A typed setting catalogue (~40 definitions seeded by migration) across AI provider, budgets, evaluation, trace/retention, feature flags, policy, and plugin categories, with per-definition validation constraints, scope permissions, secrecy, restart, and protection flags.
- Deterministic effective-configuration resolution: Environment → Workspace → Organisation → Platform default, with source scope, inheritance, and validation state reported per setting.
- Typed validation on every write (range, pattern, enum, JSON shape) plus credential-shape screening that rejects secret material in plain settings.
- Production safeguards: mandatory change reasons, and explicit confirmation for disabling protected enforcement settings; both refusals return structured Problem Details.
- Secret references without secret material — environment-variable provider today, database stores only the reference and validation metadata; exports and imports never carry secret values.
- Live, cost-free AI provider validation resolving secrets internally and reporting staged outcomes (SecretMissing/Unreachable/AuthFailed/ModelUnavailable/Valid).
- Append-only configuration change audit with actor, reason, correlation ID, value summaries, and outcome, queryable per workspace and environment.
- Versioned configuration export/import (schema 1.0) with validate-only preview and atomic apply.
- Studio `/settings` console: environment rail with lifecycle actions, eleven tabbed areas, inline typed editing with scope/inheritance badges, reset-to-inherited, production reason prompts, provider connection testing, secrets manager, change history, and import/export — plus a live topbar environment switcher persisted per workspace.
- Policy-authorised, workspace-isolated API surface for all of the above under the alpha.12 RBAC model, including read-only settings visibility for Viewers.

## Repository hardening in this release

- Fixed 19 compile errors in the settings migration and corrected SQLite GUID text casing in the environment backfill.
- Fixed environment-scope resolution in setting upsert/delete that could create duplicate override rows.
- Replaced an unsupported SQLite `DateTimeOffset` ordering in change history.
- Migrated the frontend to react-router 8.3.0, clearing all npm audit findings while keeping the initial bundle inside its 300 KB budget.

## Release state

Environment & Settings Management is active during acceptance. Backend suites pass in full (272 tests across Domain, Application, Architecture, Infrastructure, and API projects, including 62 new settings tests), and frontend lint, type-check, build, and bundle budgets are green. Vault-backed secret providers, environment promotion, drift detection, scheduled changes, and per-setting approvals remain deferred.
