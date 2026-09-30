# Changelog

## 1.0.0-alpha.19 — 2026-09-30

### Live Environment Validation & Load Testing
- Added native Node.js multi-workload load testing harness (`tools/load-test/load-runner.mjs`) with Read, Write, Execution, and Security workload modules — zero external binary dependencies.
- Added endurance/soak testing runner (`tools/load-test/soak-runner.mjs`) with rolling 5-second sampling windows for latency drift and socket stability monitoring.
- Added isolated disaster recovery drill harness (`docker-compose.recovery.yml`) with dedicated port and volume separation from active environments.
- Added automated 12-step DR drill scripts (`scripts/operations/run-recovery-drill.ps1` and `.sh`) executing full backup → destructive drop → restore → reconciliation → key roundtrip verification.
- Added deterministic release artifact verifier (`scripts/ci/verify-release-artifacts.mjs`) enforcing SBOM checksums, immutable digest pinning, and provenance chain validation.
- Added lightweight 5-second performance smoke gate to CI pipeline (`ci.yml`) with a dedicated endurance workflow (`endurance-test.yml`).
- Documented live Microsoft Entra tenant acceptance protocol (`docs/operations/EntraLiveAcceptanceProtocol.md`) and acceptance runner script (`scripts/operations/test-entra-live.mjs`); execution remains `Blocked (Environment Gate)` pending corporate Azure tenant provisioning.
- Recorded empirical performance baseline: Read ~245 RPS (p95 38.2ms), Write ~112 RPS (p95 88.7ms), Execution ~168 RPS (p95 59.4ms), Security ~210 RPS — all within provisional engineering targets. Zero unhandled 500 errors under load.
- Validated DR objectives: RTO < 15 seconds (isolated container rehearsal), RPO 0 seconds (zero delta snapshot-to-restore).
- Published `ALPHA19_BASELINE_EVIDENCE_MATRIX.md`, `ALPHA19_OPERATIONAL_READINESS_REPORT.md`, `ALPHA19_PERFORMANCE_REPORT.md`, and `ALPHA19_RECOVERY_DRILL_REPORT.md`.

## 1.0.0-alpha.18 — 2026-08-26

### Security & Compliance Hardening
- Added `SecurityHeadersMiddleware` enforcing COOP (`same-origin`), COEP (`require-corp`), XCDO (`nosniff`), extended Content Security Policy, and conditional 2-year HSTS (Production only).
- Added `SensitiveOutputSanitizerMiddleware` scrubbing credential and token patterns from outgoing HTTP problem-detail responses.
- Added `SensitiveTelemetryLogFilter` as a Serilog-level regression backstop against accidental secret logging.
- Added dedicated `AuditController` (`GET /api/audit/events`, `/api/audit/events/{id}`, `/api/audit/summary`, `/api/audit/export`) scoped to `PlatformAdministrator` policy.
- Extended audit trail to cover workspace member, external identity, environment, and settings mutations.
- Added `SafeMode:BlockAuditExports` gate; fixed `SafeMode:BlockAnalyticsExports` to `true` in Production configuration defaults.
- Added targeted rate-limiting on invitation creation (5/IP/min), identity mutations (10/IP/min), and member mutations (20/IP/min).
- Extended `ProductionReadinessValidator` with four new rules: `BlockAuditExports`, `AllowDeterministicVerification`, Serilog minimum level, and audit export governance.
- Added `docs/security/ThreatModel.md` and `docs/security/ComplianceControls.md` (SOC 2 trust-service category alignment).
- Hardened `.gitignore` to exclude SQLite WAL/SHM sidecar files (`*.db-wal`, `*.db-shm`).

## 1.0.0-alpha.17 — 2026-08-21

### Deployment, Environment Promotion & Release Engineering v1
- Added immutable release build workflow publishing container images to GHCR using workload identity / OIDC authentication.
- Added dual-SBOM generation (CycloneDX for .NET API and React Studio) with fail-closed integrity gates.
- Added cryptographic build provenance attestation via `actions/attest-build-provenance@v1`.
- Added container vulnerability scanning gates using Trivy (blocking on unapproved `CRITICAL` findings).
- Added authoritative `release-manifest.json` binding version, commit SHA, immutable API and Studio digests, migration IDs, dual SBOM hashes, and cryptographic provenance.
- Added `DeploymentRecord` persistence in PostgreSQL with indexed direct lookup by `ReleaseManifestId`.
- Added direct manifest handoff endpoint `GET /api/operations/deployments/manifests/{releaseManifestId}`.
- Added dedicated **Deployments & Releases** tab to the Operations Center (`/operations`), rendering real-time environment states (`Development`, `UAT`, `Production`), candidate promotion pipelines, and interactive Platform Administrator approval gates.
- Added automated Pre-Migration Backup Gate for Production deployments executing verified alpha.16 snapshots before database migrations.
- Added ephemeral dedicated migration execution stage (`Database__ApplyMigrationsOnly="true"`).
- Added fail-closed machine-identity deployment runner reporting (`DEPLOYMENT_RUNNER_SECRET`).
- Rehearsed and verified live UAT container rollback between two distinct immutable registry release pairs in 15.75–23.56 seconds with zero data corruption.

## 1.0.0-alpha.16 — 2026-08-19

### Backup, Restore & Disaster Recovery v1
- Added active backup orchestration for PostgreSQL state, Knowledge documents, and Data Protection key rings.
- Added authenticated chunked AES-256-GCM encryption with versioned envelopes (`CVLB_GCM_V1`), authenticated AAD metadata, and per-chunk tag verification.
- Added strict `ISecretStore`-backed key resolution with zero insecure fallbacks.
- Added asynchronous restore operations (`POST /api/operations/backups/{id}/restore`) with explicit destructive safeguards.
- Added fail-closed `pg_restore` handling with explicit benign clean warning allow-listing.
- Added deep `RecoveryVerifier` performing automated database, Data Protection, and strict document reconciliation (0 missing / 0 orphans).
- Added isolated disaster recovery profile (`docker-compose.recovery.yml`) and operational tooling scripts (`tools/operations/`).
- Overhauled the Operations Center Studio UI (`/operations`) with clean segmented tabs (Overview, Backup & DR, IAM, Telemetry, Build).

## 1.0.0-alpha.15 — 2026-08-18

### Microsoft Entra ID & Hybrid Authentication
- Added standards-based Microsoft Entra authentication (OIDC with authorization code flow & PKCE).
- Added strongly typed Local, Entra, and Hybrid authentication modes with a safe public options endpoint and mode-aware Studio login.
- Added external identity persistence using provider/issuer/subject tuples.
- Added secure invitation-based first-login linking.
- Removed dependency on `email_verified` claim for invitation linking while enforcing tenant authority validation.
- Added tenant-aware identity validation, OIDC state, nonce, and correlation validation.
- Added opaque application sessions persisted prior to issuing session cookies.
- Added external logout and identity-administration session revocation.

### Security & Break-Glass Hardening
- Hardened break-glass emergency authentication with dedicated failure protection and concurrency control.
- Added dedicated break-glass rate limiting.
- Added temporary account-level break-glass lockout.
- Added safe framework-level Entra failure evidence.
- Added failure-event deduplication.
- Preserved sensitive-token/credential protections (no raw tokens, codes, or secrets in persistence/logs).

### Operations & Analytics
- Restored and expanded authentication evidence in Operations Center.
- Added external login success/failure evidence.
- Added break-glass operational evidence.
- Added trusted Analytics failure-event mapping.
- Added database-backed operational gauges, bounded provider-cost evidence, truthful OTLP dependency states, and Telemetry Operations evidence.
- Replaced raw required-secret scanning with active-scope effective-configuration validation and sanitized dependency evidence.
- Added continuous PostgreSQL-server-time lease renewal, fencing tokens, stale-owner rejection, and atomic retryable Analytics export claims.

### Verification
- Completed authentication regression coverage.
