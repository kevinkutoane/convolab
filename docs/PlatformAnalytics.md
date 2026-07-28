# Platform Analytics v1

Platform Analytics provides workspace- and environment-scoped operational evidence for ConvoLab. It is metadata-only: prompts, messages, provider payloads, trace artifacts, secret references, and customer content are never copied into analytics events or exports.

## Trusted attribution

Execution-producing requests resolve a runtime environment after authentication. Clients may send `X-ConvoLab-Environment-Id`; when omitted, the active default environment is used. The API returns `X-ConvoLab-Resolved-Environment-Id`. Invalid, foreign, inactive, and unavailable-default environments are rejected before execution.

The API generates the authoritative `X-Correlation-ID`. A client-supplied value is retained only as `X-Parent-Correlation-ID`.

Each new simulation run stores:

- an immutable, secret-free configuration snapshot with a content-derived SHA-256 revision;
- associated execution attribution for organisation, workspace, environment, actor, role, configuration, and correlation;
- a deterministic, safe analytics event in the transactional outbox.

Alpha 13 operational records are attributed to the default environment during migration and explicitly marked `BackfilledDefaultEnvironment` with revision `legacy:alpha13-unattributed`.

## Persistence and processing

`AnalyticsEvents` is append-only during normal application operation. `AnalyticsOutbox` provides idempotent delivery and PostgreSQL workers claim records with `FOR UPDATE SKIP LOCKED`. Hourly and daily aggregates use deterministic keys. Checkpoints record high-watermarks and dirty ranges so late events trigger a safe rebuild.

CSV exports are asynchronous, restart-safe, limited to 100,000 rows and 25 MB, protected against spreadsheet formula injection, and removed after expiry.

| Data | Setting | Default |
| --- | --- | --- |
| Raw analytics events | `retention.analytics_event_days` | 90 days |
| Hourly aggregates | `retention.analytics_hourly_days` | 90 days |
| Daily aggregates | `retention.analytics_daily_days` | 730 days |
| CSV exports | `retention.analytics_export_days` | 7 days |

Retention can be overridden at workspace or environment scope. Removing raw events limits fine-grained rebuild and actor-cardinality availability but does not alter governance audit evidence.

## Cost semantics

All monetary values use ZAR:

- `Actual`: explicitly reported billed cost.
- `Estimated`: `inputTokens / 1000 × inputPrice + outputTokens / 1000 × outputPrice`.
- `Unavailable`: token or pricing evidence is incomplete; it is never represented as zero.
- Policy-prevented provider calls record explicit zero usage/cost with `ProviderInvocationPrevented=true`.

Budget consumption prefers actual cost and otherwise uses the estimate. Unknown-cost activity is reported separately.

## API and access

Endpoints are rooted at `/api/workspaces/{workspaceId}/analytics` and cover overview, usage, cost, budget, quality, governance, performance, adoption, safe events/correlations, and asynchronous exports.

Periods are UTC half-open intervals `[from,to)`. Aggregate queries support up to 366 days, hourly and raw-event queries up to 31 days, and exports up to 90 days. Event pagination is keyset-based.

The server enforces fixed role permissions. Viewers receive aggregated overview only. Engineers cannot access Production cost analytics or actor identity. Reviewers receive quality/governance/adoption evidence. Operators receive operational usage, performance, governance, and budget evidence. Administrators may view actor detail and create exports. Cross-workspace and cross-environment identifiers return `404`.

## Studio behavior

The Analytics route is lazy-loaded and adds no chart library to the initial graph. Charts are accessible SVG with textual descriptions and tabular alternatives. Environment changes are validated server-side, then old environment queries are cancelled and cleared before the new selection becomes active, preventing stale data and the former refresh requirement.

Operational indicators are deterministic rules, not statistical anomaly detection, and are labelled accordingly.
