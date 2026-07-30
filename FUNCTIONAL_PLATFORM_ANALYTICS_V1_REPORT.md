# Functional Platform Analytics v1 report

Release: `v1.0.0-alpha.14`  
Evidence date: 30 July 2026  
Status: implementation complete; ready for controlled UAT

## Implementation summary

Runtime execution now resolves the selected organisation/workspace/environment's effective Settings configuration. Provider, model, temperature, token limits, timeouts, retries, pricing, budget, evaluation, policy, plugin, feature, replay and trace controls are captured in one immutable secret-free SHA-256 snapshot. Simulator controls are validated execution overrides. The same revision and server correlation are stored across simulation, policy, provider, evaluation, trace, replay, attribution and analytics evidence.

Platform Analytics uses a transactional outbox, deterministic event keys, append-only safe events, dirty-range hourly/daily aggregation, persisted checkpoints, governed retention and asynchronous exports. Event count and distinct terminal execution count are separate measures. Each category has dedicated metrics and the Studio renders the full category result rather than a generic slice.

The API performs database-side filtering, bounded periods and keyset event pagination. Filtered export requests persist every supported dashboard filter and the caller's field visibility. Event detail and correlation timelines link to safe source evidence.

Security is enforced at route and field level. Actor, cost, token usage, provider detail and source visibility are resolved server-side. During completion testing, a Production bypass was found where an Engineer's event/correlation response could retain token counts after cost redaction. The visibility rule now requires both environment and cost permission for token usage, and the role-level API test prevents regression.

## Verification evidence

All .NET commands were executed through Docker.

| Gate | Result |
| --- | --- |
| Docker API Release publish | Passed, 0 warnings / 0 errors |
| Domain tests | 186 passed |
| Application tests | 36 passed |
| Infrastructure tests | 41 passed |
| Architecture tests | 16 passed |
| API/security tests | 18 passed |
| PostgreSQL migration suite | 4 passed: fresh/reconnect, Alpha 13 upgrade/backfill/restart, Alpha 14 completion/preservation/restart, legacy scorecard |
| Frontend lint | Passed |
| Frontend contract + interaction tests | Passed; 34 TSX surfaces audited |
| Frontend production build | Passed |
| npm audit | 0 vulnerabilities |
| Playwright functional suite | 12 functional scenarios passed |
| Playwright visual suite | dark/light baselines passed |
| Docker Compose build/start | database, API and Studio healthy |
| Restart persistence | 14 migrations, simulations, analytics events and execution attributions retained |
| Sensitive-data inspection | 0 matching analytics rows; 0 matching export files |

The final bundle evidence was:

- initial JavaScript: 277 KiB raw / 88.7 KiB gzip;
- initial CSS: 57.5 KiB raw / 11.1 KiB gzip;
- Analytics route aggregate: 99.3 KiB raw / 35.4 KiB gzip;
- all 19 lazy-route budgets passed.

The responsive shell test and deployed CSS confirm that hamburger, desktop close icon and mobile backdrop are hidden at widths of 861 px and above.

## Reconciliation evidence

The governed API journey executes an allowed deterministic run, persists evaluation and trace evidence, creates/completes replay evidence, activates a deny policy, and proves the provider execution count does not increase for the denied run.

Allowed execution:

```text
SourceExecutionId: 53b77955-fe56-43db-9aa7-37b045abb81b
CorrelationId: 086efca96c964d80b3d6d53055b67a0b
OrganisationId: 10000000-0000-0000-0000-000000000001
WorkspaceId: 20000000-0000-0000-0000-000000000001
EnvironmentId: 6a8deb9d-6a34-4f9a-8f05-2690d526f147
ActorId: 30000000-0000-0000-0000-000000000001
ConfigurationRevision: sha256:d0de48fb0c49c1b2f903b6058ba7c2538ec3dc58f15327382217691d7f72062b
Provider / model: Deterministic / convolab-deterministic-primary
Input / output tokens: 211 / 101
Estimated cost: ZAR 0.008260
Policy outcome: Allowed
Evaluation and trace: persisted and found by SourceExecutionId
Replay: candidate persisted, completed and archived
Terminal event: exactly one SimulationCompleted
```

Denied execution:

```text
SourceExecutionId: eb683235-ba3e-48b2-a1a3-a901d615c27e
CorrelationId: 7e4c3ced96b54f01b1ff4b1f7346be40
OrganisationId: 10000000-0000-0000-0000-000000000001
WorkspaceId: 20000000-0000-0000-0000-000000000001
EnvironmentId: 6a8deb9d-6a34-4f9a-8f05-2690d526f147
ActorId: 30000000-0000-0000-0000-000000000001
ConfigurationRevision: sha256:d0de48fb0c49c1b2f903b6058ba7c2538ec3dc58f15327382217691d7f72062b
ProviderInvocationPrevented: true
Input / output tokens: 0 / 0
Provider cost: ZAR 0
Policy outcome: Denied
ProviderInvocationCompleted events: 0
Provider execution-count increase: 0
Terminal event: exactly one SimulationFailed
```

The test captured 11 deterministic event IDs for each timeline and asserted a single shared correlation/configuration revision per execution. PostgreSQL reconciliation tooling is in `tools/analytics-reconciliation.sql`.

The persisted Docker dataset predates the completion event taxonomy for two older Alpha 14 runs. Those rows remain safe and attributable but have the historical `SimulationExecution` type, so their aggregate execution count is intentionally not reinterpreted. New runs emit the full terminal taxonomy.

## Security evidence

The API security test creates a Production environment and a provider event with known actor, tokens and actual cost, then authenticates real session principals with fixed workspace roles.

| Scenario | Proven result |
| --- | --- |
| Engineer Production cost route | `403` |
| Engineer Production events | `200`, actor/source/cost/tokens redacted |
| Engineer Production correlation | `200`, actor/source/cost/tokens redacted |
| Reviewer cost route | `403` |
| Reviewer events | actor/source/cost/tokens redacted |
| Viewer overview | `200` aggregated only |
| Viewer event drill-down | `403` |
| Administrator event detail | actor, tokens, cost and source present |
| Foreign organisation/workspace/environment | `404` isolation contract |
| Analytics database content scan | 0 known sensitive-customer/token patterns |
| CSV content scan | 0 known sensitive-customer/token patterns |

## Performance evidence

PostgreSQL `16.14` was measured with 100,000 transaction-local events; the benchmark rolled back and did not alter UAT data. The 3-day window represented approximately 10,000 events.

| Operation | Volume | Execution time |
| --- | ---: | ---: |
| Overview | ~10,376 | 81.3 ms |
| Overview | ~100,008 | 123.7 ms |
| Cost | 100,000 | 35.8 ms |
| Quality | 100,000 | 35.2 ms |
| Governance | 100,000 | 45.4 ms |
| Keyset event page, 50 rows | 100,000 | 90.0 ms |
| Correlation lookup, 8 rows | 100,000 | 5.3 ms |
| Filtered export materialisation, 4,167 rows | 100,000 | 56.7 ms |
| One-day incremental daily aggregation | ~3,464 | 118.4 ms |
| One-day late-event rebuild | ~3,456 | 41.0 ms |

The 100k overview and governance plans used `IX_AnalyticsEvents_WorkspaceId_EnvironmentId_OccurredAt`; correlation used `IX_AnalyticsEvents_WorkspaceId_CorrelationId_OccurredAt`. The 100k overview distinct-execution sort spilled 4.9 MB to temporary storage. Other observed sorts used approximately 25 KB–1.9 MB. One-million-event readiness is not claimed.

The reproducible benchmark is `tools/analytics-performance.sql`.

## Known limitations

- Alpha 14 exposes workspace/environment analytics only; platform-wide rollups remain reserved.
- Actual cost exists only when a provider reports billed ZAR; deterministic usage is synthetic and labelled.
- Raw-event retention limits old fine-grained rebuilds.
- Historical Alpha 13 attribution cannot reconstruct its original configuration.
- The deterministic provider is for local repeatable testing, not model-quality validation.
- Production SSO and managed vault adapters remain beta work.

## Deliverables

The repository includes the required Analytics model, execution context, aggregation, cost, security, exports, reconciliation and permissions documentation, this functional report, and a clean `convolab-main` release archive.
