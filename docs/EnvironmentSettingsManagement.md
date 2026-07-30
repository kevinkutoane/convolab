# Environment & Settings Management v1

Environment & Settings Management is the alpha.13 configuration control plane for ConvoLab. It replaces implicit `appsettings.json`/environment-variable configuration with a governed, auditable, environment-aware settings model that every capability resolves at runtime.

## Concepts

**Runtime environments** are first-class workspace resources (`Development`, `Staging`, `Production`, `Custom`) with a lifecycle (`Active`, `Suspended`, `Archived`), a default flag, and optimistic-concurrency revisions. Archived environments are immutable.

**Setting definitions** form a typed catalogue (~40 seeded by migration) covering AI provider, budgets, evaluation, trace/retention, feature flags, policy, and plugin categories. Each definition declares a value type (`String`, `Integer`, `Decimal`, `Boolean`, `Json`, `Enum`, `SecretRef`), validation constraints (min/max, allowed values, pattern), scope permissions, secrecy, restart requirements, and whether it is protected.

**Setting values** are scoped overrides. Effective configuration resolves with deterministic precedence:

```text
Environment override → Workspace override → Organisation override → Platform default
```

Every effective setting reports its source scope, inheritance status, and validation state.

At execution time the runtime adapters resolve this effective configuration for the trusted request context, validate any permitted simulator overrides, persist/reuse an immutable secret-free SHA-256 snapshot, and pass the resolved values to provider, policy, evaluation, plugin, replay and trace behavior. Process environment variables remain bootstrap/infrastructure or secret-value sources; they do not silently replace persisted business settings.

**Secret references** never store secret material. A reference names an external location (environment variable today; vault providers later). The database stores only the reference, its validation status, and audit metadata. Exports never contain secret values; imports never accept them.

**Configuration changes** are append-only audit records with actor, reason, correlation ID, previous/new value summaries, and outcome. History is queryable per workspace and per environment.

## Safeguards

Writes are validated against the typed definition before persistence — range, pattern, enum membership, JSON shape — and string values are screened for credential-shaped content (for example `sk-…` keys), which is rejected outright. Production environments enforce two additional safeguards: every change requires a stated reason, and disabling protected enforcement settings (for example `policy.enforcement_enabled`) additionally requires an explicit `confirmProtectedChange` acknowledgement. Both refusals return structured Problem Details.

## Provider validation

`POST …/provider/validate` performs a live, cost-free configuration check for the active AI provider: it resolves the configured secret reference internally (never returning it), calls the provider's model-listing endpoint, and reports a staged outcome — `SecretMissing`, `Unreachable`, `AuthFailed`, `ModelUnavailable`, or `Valid` — with per-stage booleans and duration. No tokens are consumed.

## API surface

`EnvironmentsController` and settings controllers expose environment CRUD and lifecycle (activate/suspend/archive/make-default), scoped setting list/effective/upsert/delete at organisation, workspace, and environment scope, secret-reference management, change history, full settings validation reports, provider validation, and versioned export/import (schema `1.0`) with a validate-only preview mode. All endpoints are policy-authorised (`CanViewSettings`, `CanManageWorkspaceSettings`, `CanManageEnvironmentSettings`, `CanManageSecretReferences`, `CanValidateProviderConfiguration`, `CanExportConfiguration`, `CanImportConfiguration`) and workspace-isolated.

## Studio experience

`/settings` provides the operator console: an environment rail with lifecycle actions, eleven tabbed areas (Environments, General, AI Provider, Budgets, Evaluation, Trace & Retention, Feature Flags, Policies & Plugins, Secrets, Change History, Import/Export), inline typed editing with scope and inheritance badges, reset-to-inherited, production reason prompts, provider connection testing, and export/import with preview. The topbar hosts a live environment switcher with type-coloured chips; the selection persists per workspace and broadcasts to all Studio pages.

## Deferred

Vault-backed secret providers, environment promotion pipelines, configuration drift detection between environments, scheduled configuration changes, and per-setting change approvals are outside alpha.13.
