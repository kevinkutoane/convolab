# Operational Foundation Correction Report

## Status

Workstream: `alpha.15 Operational Foundation — Correction Sprint`

Active release version: `1.0.0-alpha.14`

Evidence date: 2026-08-03

The correction implementation and automated acceptance are complete, but this report does **not** declare the Operational Foundation tranche signed off. Two environment-level exceptions remain:

1. The live checkout is `C:\Users\W1022804\convolab`, not the required `convolab-main`. A safe in-place Windows rename could not be completed while the shared workspace was locked. No copied or parallel repository was created.
2. The active Development environment selects Gemini and has no effective secret reference. The final stack therefore truthfully reports `/health/live` as HTTP 200, `/health/ready` as HTTP 503, required secrets as `Unavailable`, and overall operational status as `Unhealthy`. No fake secret was injected and the finding was not suppressed.

Entra/OIDC, backups and restore, deployment promotion, supply-chain controls, hardened deployment manifests, performance evidence, and the final operational-readiness report remain deferred alpha.15 workstreams.

## Implementation summary

- Analytics readiness now uses database-side pending, failed, age, dirty/failed checkpoint, lag, and last-success queries. Fresh pending work remains healthy; permanent failures and threshold breaches affect status.
- Required-secret readiness resolves effective configuration only for active, in-scope environments and returns sanitized dependency evidence.
- PostgreSQL leases use one `clock_timestamp()` value per statement, continuous renewal, monotonically increasing fencing tokens, stale-owner rejection, and atomic export claims with `SKIP LOCKED` semantics.
- Worker evidence persists component-specific processed/failed counts, bounded failure evidence, iteration status, timestamps, and cumulative work.
- Proxy, local authentication, data protection, secret stores, safe mode, operations thresholds, telemetry, build, required secrets, and Analytics worker settings use validated typed options.
- Database-backed observable gauges cover outbox, aggregation, leases, heartbeat, worker result, safe mode, and active sessions. Snapshot ages use database time.
- OTLP evidence distinguishes configuration from successful collector I/O and does not block startup.
- The lightweight Operations summary caches the last real readiness evaluation; it no longer reports `Healthy` when detailed readiness is `Unhealthy`.
- The global safe-mode query refreshes on polling, focus, visibility, context restoration, safe-mode errors, and mutation. Operations detail queries are invalidated after mutation.
- The Operations Center includes all required panels, explicit dependency states, administrator-only routing, deliberate export-policy wording, and honest `NotConfigured` backup evidence.

## Pipeline evidence

Default applied thresholds were warning/unhealthy pending ages 60/300 seconds, failed degraded/unhealthy counts 1/10, failed unhealthy age 300 seconds, and aggregation warning/unhealthy lag 120/600 seconds.

| Scenario | Pending | Failed | Oldest pending | Oldest failed | Aggregation evidence | Result |
|---|---:|---:|---:|---:|---|---|
| Empty | 0 | 0 | none | none | lag 0, no failed checkpoints | Healthy |
| Fresh pending | 1 | 0 | 5 s | none | lag 0 | Healthy |
| Old pending | 1 | 0 | 61 s | none | lag 0 | Degraded |
| Unhealthy pending | 1 | 0 | 301 s | none | lag 0 | Unhealthy |
| One failed | 0 | 1 | none | 10 s | lag 0 | Degraded |
| Failed-count limit | 0 | 10 | none | 10 s | lag 0 | Unhealthy |
| Old failed | 0 | 1 | none | 300 s | lag 0 | Unhealthy |
| Failed checkpoint | 0 | 0 | none | none | one failed checkpoint | Degraded |

The database-reader integration case observed one pending row aged 60–90 seconds, one failed row aged 300–330 seconds, one dirty and one failed checkpoint, 120–150 seconds maximum lag, and populated last-dispatch/last-aggregation timestamps. Its sentinel payload did not appear in returned evidence.

The final live stack reported pending `0`, failed `0`, aggregation lag `0`, and Analytics status `Healthy`. Required-secret readiness independently kept overall readiness `Unhealthy`.

## Secret readiness evidence

The effective-readiness acceptance fixture proved:

- an archived environment was ignored;
- an active environment with provider execution disabled was not treated as requiring a secret;
- a lower-precedence workspace secret value was not validated;
- only the effective environment reference was passed to validation;
- deterministic validation was labelled `StubValidated`, not `LiveValidated`;
- effective, stale, disabled, and archived reference names and all secret values were absent from serialized evidence.

The secret-store suite passed 18 cases covering environment routing, Docker valid/missing/absolute/traversal/separator/symlink/reparse/permission handling, cancellation, cache hit/expiry/invalidation/concurrency, failed-result non-caching, Azure allowlists/timeouts/cancellation/retry limits, stub-state labelling, response-body sanitation, and restricted UAT/Production credential construction.

Final live sanitized evidence was:

- `env`: `Configured`
- `docker-secret`: `Configured`
- `azure-key-vault`: `NotConfigured`
- Development/Gemini required secret: `Unavailable`, code `secret.required_not_configured`

No reference, environment-variable name, Docker filename, vault URI, credential, or value was returned.

## Worker and migration evidence

The live worker used the configured 120-second lease and 30-second renewal interval. A final live sample reported worker `analytics-maintenance`, owner `63ee690d452f:1`, token `168`, current status `Healthy`, all last-iteration component counts `0`, and cumulative processed count `597`. The zero component counts describe the actual idle iteration rather than an invented processed count.

The dedicated live PostgreSQL long-operation test used a 2-second lease and 1-second renewal interval. Executed output was:

```text
owner=renewing-owner
initialToken=1
initialExpiry=2026-08-03T12:15:40.2007120+00:00
firstRenewalExpiry=2026-08-03T12:15:41.9204370+00:00
secondRenewalExpiry=2026-08-03T12:15:43.3921950+00:00
contenderDuringRenewal=denied
takeoverToken=2
staleFinalWrite=rejected
finalOutboxProcessed=1
```

The operation remained owned beyond its original lease, the contender was denied during renewal, takeover succeeded only after expiry, the stale owner could not write success/counts, and the new owner persisted the actual count.

Atomic export acceptance proved a pending item was claimed once across two contenders, entered `Processing` with owner/token/attempt evidence, was reclaimable after 121 seconds of abandoned processing, rejected a stale token after takeover, and accepted the current token.

Fresh PostgreSQL migration, alpha.14 Analytics upgrade, Operational Foundation migration upgrade, restart persistence, safe-mode preservation, worker-evidence preservation, and idempotent migration execution passed. Migration `202608030002_OperationalFoundationCorrectionsV1` contains only the required worker/export fencing and evidence columns/indexes.

## Safe-mode evidence

Five dedicated infrastructure acceptance cases plus API and browser coverage proved:

- persisted activation blocks external provider execution/validation, plugin activation/probes, and external replay;
- `CONVOLAB_SAFE_MODE=true` takes precedence and cannot be deactivated through the API;
- deterministic verification is blocked when disabled and permitted when enabled while external execution remains blocked;
- Analytics exports are blocked for an explicit `true` decision and remain available for explicit `false`;
- stale expected revisions return concurrency Problem Details;
- mutations persist audit evidence, enqueue trusted Analytics evidence when a scope exists, emit warning/high-severity structured evidence, and refresh global/detail state;
- routine Operations polling creates no persistent audit rows;
- a second browser session receives the updated banner, and temporary refresh failure preserves the last-known active state.

The final persisted safe mode state was disabled; the environment override was disabled.

## Telemetry evidence

Implemented database-backed measurements:

```text
convolab.analytics.outbox.pending
convolab.analytics.outbox.failed
convolab.analytics.outbox.oldest_age
convolab.analytics.aggregate.lag
convolab.worker.lease.active
convolab.worker.heartbeat.age
convolab.worker.last_iteration.status
convolab.safe_mode.active
convolab.auth.session.active
```

Captured gauge samples were pending `1`, failed `1`, oldest age 20–45 seconds, aggregate lag 75–105 seconds, active lease `1`, heartbeat age 0–10 seconds, worker status `3` (`Healthy`), safe mode `1`, and active sessions `1`.

`convolab.provider.cost.zar` acceptance emitted only `Actual` and `Estimated` values (1.25 and 2.50 ZAR). `Unavailable` and missing costs emitted no measurement. Labels were limited to bounded provider type, cost type, and outcome; the user-configurable model was absent.

Collector acceptance observed:

- configured collector: `LiveValidated` after successful TCP I/O;
- collector stopped: API liveness HTTP 200, state `Unavailable`, code `telemetry.collector_unavailable`, last-live timestamp retained;
- collector restarted: state returned to `LiveValidated`, failure code cleared, API liveness remained HTTP 200;
- both trace and metric batches appeared in collector output after recovery.

The exporter does not expose per-batch delivery callbacks. `LiveValidated` therefore proves collector reachability/I/O, not durable delivery or replay of telemetry created during outage.

The sentinel scan covered password, Gemini key, Azure credential, Docker value, cookie, authorization, antiforgery token, secret reference, prompt, customer message, provider response, and OTLP header values. Custom span/metric capture and final API/collector logs contained none of them. Compact JSON request evidence included correlation ID and CLEF `@tr`/`@sp` trace/span IDs plus bounded workspace context. Automatic Gemini, Key Vault, and plugin-probe HTTP spans were suppressed; sanitized custom activities remained.

## Browser evidence

The Operations suite passed 8/8 in each required lifecycle position:

- before restart;
- after API restart;
- after PostgreSQL restart;
- against rebuilt images;
- against the final truthful-readiness images.

Journeys covered all panels, administrator navigation, every non-platform role denial, backup `NotConfigured`, dependency labels, safe-mode activation/deactivation, blocked-work Problem Details, revision conflict, cross-session banner refresh, last-known-state preservation, polling/audit behavior, and mobile/tablet/desktop layouts.

The complete browser regression suite passed 21/21, including canonical/compatibility routes, transient bootstrap recovery, visual baselines, governance interactions, accessibility/responsive navigation, error recovery, settings, Analytics, themes, and deterministic-provider wording.

## Verification evidence

| Gate | Executed result |
|---|---|
| `dotnet restore ConvoLab.sln` | Exit 0; projects current; restricted-network `NU1900` vulnerability-feed warnings only |
| Release build | Succeeded, 0 errors, 4 `NU1900` warnings |
| Full .NET tests with live PostgreSQL | 366/366 passed: Domain 186, Application 42, Architecture 16, API 39, Infrastructure 83 |
| Focused pipeline evaluator | 6/6 passed |
| Secret store | 18/18 passed |
| Safe-mode acceptance | 5/5 passed |
| Telemetry acceptance | 4/4 passed |
| Long lease/fencing test | 1/1 passed with the timestamps above |
| `npm ci` | 199 packages installed, 0 vulnerabilities reported at install |
| `npm run lint` | Passed |
| `npm run test` | Contract tests passed; interaction audit passed across 36 TSX files |
| `npm run build` | Passed; initial graph and 20 lazy-route budgets passed |
| `npm audit --audit-level=low` | 0 vulnerabilities |
| Docker API/Studio build | Both images built successfully |
| Docker services | PostgreSQL healthy; API and Studio respond; API readiness intentionally 503 for the real missing required secret |
| API restart | Liveness recovered; Operations suite 8/8 |
| PostgreSQL restart | PostgreSQL health and API liveness recovered; Operations suite 8/8 |
| OTLP outage/recovery | Passed as documented above |
| Sensitive telemetry scan | Passed; trace and metric batches observed |
| Complete Playwright | 21/21 passed |

Analytics reconciliation, late-event aggregation, outbox idempotency, tenant isolation, cross-environment rejection, and policy denial before provider invocation remain covered by the unchanged alpha.14 regression suite.

## Sign-off exceptions and next actions

1. Close processes holding the workspace and safely rename the existing directory from `convolab` to `convolab-main`; do not copy it.
2. Configure a real effective Development Gemini secret reference, or deliberately change the active Development provider/execution decision. Re-run `/health/ready` and Docker Compose startup evidence afterward.
3. Keep all active metadata at `1.0.0-alpha.14` and continue to describe this as an in-progress alpha.15 correction workstream.
4. Do not start Entra/OIDC or claim full alpha.15 completion until the deferred workstreams and their own acceptance evidence are complete.
