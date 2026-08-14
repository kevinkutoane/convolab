# Operational health checks

ConvoLab remains versioned `1.0.0-alpha.14`. Entra dependency evidence is extended by the in-progress `alpha.15 — Microsoft Entra ID, External Identities & Hybrid Authentication` workstream.

## Endpoints

| Endpoint | Meaning |
| --- | --- |
| `/health/live` | Process availability only. |
| `/health/startup` | Static configuration, bootstrap, database/schema, storage, data protection, and required startup dependencies. |
| `/health/ready` | Current ability to serve governed work, including PostgreSQL, schema, storage, bootstrap, data protection, effective required secrets, the Analytics worker, outbox backlog, and aggregation lag. |
| `/health` | Compatibility alias for readiness. |

Anonymous health responses contain only status, the actual assembly version, and correlation ID. Detailed component evidence is restricted to Platform Administrators in Operations Center.

## Analytics readiness

Readiness uses database-side aggregates for pending and failed outbox records, their oldest ages, dirty and failed aggregation checkpoints, maximum aggregation lag, and last successful dispatch/aggregation timestamps. A fresh pending record is normal asynchronous work and remains Healthy. Permanent or exhausted failures are never hidden.

| Threshold | Default |
| --- | ---: |
| Worker warning / unhealthy | 45 / 60 seconds |
| Pending outbox warning / unhealthy | 60 / 300 seconds |
| Failed outbox degraded / unhealthy count | 1 / 10 |
| Failed outbox unhealthy age | 300 seconds |
| Aggregation warning / unhealthy lag | 120 / 600 seconds |

The values bind from `Operations:Thresholds`, are validated at startup, and are shared by health checks, Operations APIs, tests, and Studio labels.

Healthy means all ages and counts remain below warning thresholds. Degraded includes aged pending work, any configured failed-count threshold, failed aggregation checkpoints, warning-level aggregation lag, or a recent partial worker iteration. Unhealthy includes unhealthy ages/counts/lags or a stale required Analytics worker.
