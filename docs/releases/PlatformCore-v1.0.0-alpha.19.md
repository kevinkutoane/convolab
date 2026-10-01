# ConvoLab Platform and Studio v1.0.0-alpha.19

> **Status: Operational Milestone — Formally Closed**
> This document describes engineering work delivered in the `1.0.0-alpha.19` operational milestone on `main`.
> The last formal product release is `v1.0.0-alpha.18` (commit `073152a40fe81cb3ea3669eeb512d345f6032a4b`).
> Alpha.19 is bound to operational tag `v1.0.0-alpha.19` and release-build workflow `36931388866` for evidence and reproducibility. No formal GitHub Release was published for Alpha.19.
> Alpha.19 delivers operational-validation evidence and tooling rather than a new product release.

Alpha.19 delivers **Live Environment Validation & Load Testing**, establishing a reproducible performance baseline, isolated disaster recovery verification, deterministic supply-chain artifact verification, and formal documentation of all outstanding environment gates.

## Delivered

- **Native Load Testing Harness:**
  - `tools/load-test/load-runner.mjs` — multi-workload runner (Read, Write, Execution, Security) with zero external binary dependencies.
  - `tools/load-test/soak-runner.mjs` — endurance runner with rolling 5-second sampling windows.
  - Empirical baseline: Read ~245 RPS (p95 38.2ms), Write ~112 RPS (p95 88.7ms), Execution ~168 RPS (p95 59.4ms), Security ~210 RPS — all within provisional engineering targets. Zero unhandled 500 errors.

- **Isolated Disaster Recovery Drill:**
  - `docker-compose.recovery.yml` — fully isolated drill environment (ports 5001/5433, dedicated volumes).
  - `scripts/operations/run-recovery-drill.ps1` / `.sh` — 12-step automated verification protocol (backup → destructive drop → restore → reconciliation → key roundtrip).
  - DR objectives validated: RTO < 15 seconds, RPO 0 seconds (isolated container rehearsal).

- **Deterministic Release Artifact Verifier:**
  - `scripts/ci/verify-release-artifacts.mjs` — fail-closed verifier enforcing SBOM checksums, immutable digest pinning, and provenance chain validation; integrated into CI hygiene.

- **CI Performance Gate:**
  - Lightweight 5-second smoke gate added to `ci.yml`.
  - Dedicated `endurance-test.yml` workflow for extended load runs.

- **Evidence Reports:**
  - `docs/reports/ALPHA19_BASELINE_EVIDENCE_MATRIX.md`
  - `docs/reports/ALPHA19_OPERATIONAL_READINESS_REPORT.md`
  - `docs/reports/ALPHA19_PERFORMANCE_REPORT.md`
  - `docs/reports/ALPHA19_RECOVERY_DRILL_REPORT.md`

- **Microsoft Entra Live Acceptance Protocol:**
  - `docs/operations/EntraLiveAcceptanceProtocol.md` — formal runbook for live tenant validation.
  - `scripts/operations/test-entra-live.mjs` — acceptance runner ready for execution when credentials are available.
  - **Status:** `Blocked (Environment Gate)` — pending corporate Azure tenant provisioning. This is not a code blocker.

## Known Limitations (Environment Gates)

| Capability | State | Requirement |
| --- | --- | --- |
| Live Microsoft Entra Tenant | Blocked (Environment Gate) | Corporate Azure AD tenant + credentials |
| External APM / OTLP backend | Infrastructure-gated | Production OTLP endpoint |
| SCIM Provisioning | Deferred | Phase 5 scope |
| Point-in-time Recovery (PITR) | Deferred | Enterprise cloud infrastructure |
