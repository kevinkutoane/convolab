# ConvoLab Alpha.18 Release Evidence

## Release Identity

| Field | Value |
| --- | --- |
| Release | `1.0.0-alpha.18` |
| Release commit | `073152a40fe81cb3ea3669eeb512d345f6032a4b` |
| Previous release | `1.0.0-alpha.17` at `f8090674651056e90ddb437d12a5d2680038ae34` |
| Workstream | `alpha.18 — Security & Compliance Hardening` |

## 1. Delivered Scope

### HTTP Security Response Headers
- `SecurityHeadersMiddleware` enforcing `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Cross-Origin-Opener-Policy: same-origin`, `Cross-Origin-Embedder-Policy: require-corp`, `X-Permitted-Cross-Domain-Policies: none`, `Permissions-Policy`, and strict `Content-Security-Policy`.
- Conditional HSTS (`Strict-Transport-Security: max-age=63072000; includeSubDomains; preload`) enforced in Production; suppressed in non-Production environments.

### Platform Audit Trail & Compliance
- Dedicated `AuditController` (`GET /api/audit/events`, `GET /api/audit/events/{id}`, `GET /api/audit/summary`, `GET /api/audit/export`) requiring `PlatformAdministrator` policy.
- Audit trail extended to cover workspace member, external identity, environment, and settings mutations.
- `SafeMode:BlockAuditExports` introduced alongside existing `SafeMode:BlockAnalyticsExports` (fixed to `true` in Production defaults).
- Zero exposure of raw credentials, tokens, subjects, passwords, authorization codes, or private claims in audit DTO records.

### Transport Sanitization & Defense-in-Depth
- `SensitiveOutputSanitizerMiddleware` scrubbing sensitive token patterns and credential sentinels from outgoing problem JSON responses.
- `SensitiveTelemetryLogFilter` as a Serilog-level regression backstop.

### Rate Limiting
- Invitation creation: 5 requests/IP/min.
- Identity mutations: 10 requests/IP/min.
- Member mutations: 20 requests/IP/min.

### Production Readiness Validation
- Four new `ProductionReadinessValidator` rules (BlockAuditExports, AllowDeterministicVerification, Serilog level, BlockAuditExports governance).

### Compliance Documentation
- `docs/security/ThreatModel.md` — Platform threat model.
- `docs/security/ComplianceControls.md` — SOC 2 trust-service category alignment.

### Repository Hygiene
- `.gitignore` hardened to exclude SQLite WAL/SHM sidecar files (`*.db-wal`, `*.db-shm`).

## 2. CI Validation

**Status:** Alpha.18 formally tagged and released. CI workflow completed successfully against release commit `073152a`. Release workflow `release-build.yml` updated to target Alpha.18 (commit `073152a ci: update release workflow to target Alpha.18`).

## 3. Post-Release Housekeeping (applied after release)

The following housekeeping corrections were applied to `main` after the alpha.18 release to keep the repository consistent for the alpha.19 milestone:

| Item | Change | File |
| --- | --- | --- |
| Design-time version string | `"1.0.0-alpha.17"` to `"1.0.0-alpha.18"` | `web/src/data/platform.ts` |
| Workstream string | Updated to alpha.18 scope description | `web/src/data/platform.ts` |
| Operations nav status | `"foundation"` to `"stable"` (page fully implemented: 6 live tabs) | `web/src/data/platform.ts` |

### `convolab.db` Git Tracking Verification

Confirmed: `src/Api/ConvoLab.Api/convolab.db` is **not tracked** by git (`git ls-files --error-unmatch` returned exit code 1). The alpha.18 `.gitignore` hardening correctly excludes WAL/SHM sidecars.

## 4. Active Development Milestone

`alpha.19 - Live Environment Validation & Load Testing` is active on `main`. See `docs/Roadmap.md` for scope. The Entra live-tenant acceptance remains environment-gated and is the only open item blocking full alpha.19 closure.
