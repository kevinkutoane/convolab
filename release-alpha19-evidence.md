# ConvoLab Alpha.19 Operational Milestone Evidence

## Release Identity

| Field | Value |
| --- | --- |
| Milestone | `Alpha.19 — Live Environment Validation & Load Testing` |
| Operational version identifier | `1.0.0-alpha.19` |
| Operational milestone tag | `v1.0.0-alpha.19` → closure commit `bd4bdcb59115e7bab9db4894209b34114e4dbb35` |
| Previous formal product release | `1.0.0-alpha.18` at commit `073152a40fe81cb3ea3669eeb512d345f6032a4b` |
| Evidence base commit | `26c8e3b` (load test soak breakdown) |
| Release-build workflow | GitHub Actions run `36931388866` |
| Release artifact | `release-artifacts` (artifact SHA256 `7a9f7cfc812d089c8050b4bfaa97d0bf5055ffb4284c26de2a4f4b7ca7f0c84d`) |

The `v1.0.0-alpha.19` tag and release-build artifact bind the operational milestone to an immutable source commit and reproducible build evidence. Alpha.19 is an operational validation milestone; subsequent feature development culminated in the `v1.0.0-enterprise` Release Candidate delivering the four strategic enterprise pillars.

## 1. Delivered Scope

All alpha.19 deliverables are implemented and committed to `main`. The milestone delivers operational evidence and tooling rather than a new product release.

The authoritative Alpha.19 release-build manifest was produced from commit `bd4bdcb59115e7bab9db4894209b34114e4dbb35` and recorded:

- API image digest: `ghcr.io/kevinkutoane/convolab/convolab-api@sha256:42294f96f13d63d12ec6e6c704a9399644c07eca41ea259defe191326b835f71`
- Studio image digest: `ghcr.io/kevinkutoane/convolab/convolab-studio@sha256:92f9670c5bcca900a95e00f2cdd887a7e09fbca90a75e83c2bfcbe1c3d4920d1`
- API SBOM SHA256: `fe42bcba458002d6761ee9d8d261752485eb209d3dfac4c25425ef403dcf44a4`
- Studio SBOM SHA256: `e72ad3491364a33faf9a4c93a5d1338990e540c7257b8db0d27415cf46321329`
- Provenance/build workflow: `36931388866`
- Build timestamp: `2026-10-01T21:51:31Z`

### Load Testing Harness
- `tools/load-test/load-runner.mjs` — multi-workload load runner (Read, Write, Execution, Security); zero external binary dependencies.
- `tools/load-test/soak-runner.mjs` — endurance runner with rolling sampling windows.
- Empirical baseline recorded: Read ~245 RPS (p95 38.2ms), Write ~112 RPS (p95 88.7ms), Execution ~168 RPS (p95 59.4ms). Zero unhandled 500 errors under load.

### Disaster Recovery Drill
- `docker-compose.recovery.yml` — isolated drill environment (ports 5001/5433, dedicated volumes).
- `scripts/operations/run-recovery-drill.ps1/.sh` — 12-step automated DR verification.
- Objectives validated: RTO < 15 seconds, RPO 0 seconds.

### Release Artifact Verifier
- `scripts/ci/verify-release-artifacts.mjs` — deterministic fail-closed verifier integrated into CI hygiene.

### CI Performance Gate
- Lightweight 5-second smoke gate in `ci.yml`; dedicated `endurance-test.yml` workflow.

## 2. Operational Evidence Reports

| Report | Location |
| --- | --- |
| Baseline Evidence Matrix | `docs/reports/ALPHA19_BASELINE_EVIDENCE_MATRIX.md` |
| Operational Readiness Report | `docs/reports/ALPHA19_OPERATIONAL_READINESS_REPORT.md` |
| Performance & Load Report | `docs/reports/ALPHA19_PERFORMANCE_REPORT.md` |
| Disaster Recovery Drill Report | `docs/reports/ALPHA19_RECOVERY_DRILL_REPORT.md` |

## 3. Environment Gate Register

The following capabilities are formally documented as environment-gated and are NOT considered code blockers:

| Gate | State | What is needed |
| --- | --- | --- |
| Live Microsoft Entra tenant acceptance | Blocked (Environment Gate) | Corporate Azure AD tenant + `CONVOLAB_ENTRA_*` credentials. Run: `node scripts/operations/test-entra-live.mjs` |
| External APM / OTLP backend | Infrastructure-gated | Production OTLP endpoint configuration |
| SCIM Provisioning | Deferred (Phase 5) | Enterprise Azure AD SCIM endpoint |
| Point-in-Time Recovery (PITR) | Deferred (post-alpha) | WAL archiving + enterprise cloud infrastructure |

## 4. Post-Milestone Housekeeping Applied

The following housekeeping items were applied to `main` during the alpha.19 closure pass:

| Item | Change |
| --- | --- |
| `web/src/pages/LoginPage.tsx` | Enterprise login page redesign; version string updated to alpha.18 |
| `web/src/workspace-identity.css` | Enterprise login page CSS (lp-* system) |
| `web/src/data/platform.ts` | Version → `1.0.0-alpha.18`, workstream → alpha.18, Operations nav → `stable` |
| `tools/release/build-release-artifacts.sh` | Default VERSION → `1.0.0-alpha.18` |
| `docs/project/CHANGELOG.md` | Alpha.18 and alpha.19 entries added |
| `docs/releases/PlatformCore-v1.0.0-alpha.19.md` | Operational milestone notes created |
| `docs/Roadmap.md` | Alpha.19 marked delivered; alpha.20 added as next milestone |
| `docs/project/ROADMAP.md` | Alpha.19 workstream appended |
| `web/scripts/verify-baseline.mjs` | Alpha.19 release notes file added to verification list |
| `release-alpha18-evidence.md` | Root-level alpha.18 evidence document created |

## 5. Active Development Milestone

`alpha.20 — Environment & Secret Management` is the next planned milestone. Scope: per-environment configuration governance, secret rotation controls, environment promotion rules, and secret-store integration hardening required for beta readiness.
