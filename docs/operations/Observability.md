# Operational observability

The correction sprint exports traces and metrics through optional OTLP configuration while keeping collector failure outside the startup dependency boundary.

## Database-backed gauges

The periodically refreshed snapshot meter `ConvoLab.OperationalFoundation.DatabaseState` exposes:

| Metric | Source |
| --- | --- |
| `convolab.analytics.outbox.pending` | Pending rows in PostgreSQL. |
| `convolab.analytics.outbox.failed` | Failed rows in PostgreSQL. |
| `convolab.analytics.outbox.oldest_age` | Age of the oldest pending row. |
| `convolab.analytics.aggregate.lag` | Maximum dirty-checkpoint lag. |
| `convolab.worker.lease.active` | Effective Analytics worker lease. |
| `convolab.worker.heartbeat.age` | Age of `analytics-maintenance` heartbeat. |
| `convolab.worker.last_iteration.status` | Bounded numeric status: Starting `0`, Running `1`, Degraded `2`, Healthy `3`, Failed `4`, LeaseLost `5`, Stopped `6`, other `7`. |
| `convolab.safe_mode.active` | Effective persisted-or-environment safe mode. |
| `convolab.auth.session.active` | Unexpired, non-revoked sessions in the database. |

No measurement is emitted before the first successful snapshot. Backup-age remains intentionally absent until backup tooling exists. Provider cost is emitted only for trusted `Actual` or `Estimated` cost evidence; unavailable cost is not emitted as zero.

## OTLP evidence

`Configured` proves valid exporter configuration. `LiveValidated` is set only after the explicit collector connection probe succeeds. `Unavailable` records a failed probe without preventing startup; `NotConfigured` means no endpoint/exporter is enabled. A probe proves collector reachability, not that every span or metric batch was accepted or durably stored. Telemetry created during an outage must not be described as delivered unless exporter buffering supplies separate evidence.

Operations Center exposes only whether an endpoint is configured, trace/metric enablement, service name, release version, last live validation time, and a safe failure code. It never exposes the endpoint, headers, or credentials.

Labels are bounded and exclude identifiers, email addresses, correlations, prompts, payloads, models, customer messages, and secret references. Automatic HTTP spans are suppressed for Gemini, Key Vault, and outbound plugin probes; sanitized custom activities describe those operations.

