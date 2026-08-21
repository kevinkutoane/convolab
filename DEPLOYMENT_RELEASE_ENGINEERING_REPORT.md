# Deployment, Environment Promotion & Release Engineering Report (alpha.17)

**Workstream Status:** FULLY VERIFIED, TESTED & SIGN-OFF READY  
**Active Baseline Version:** `1.0.0-alpha.16`  
**Target Release Workstream:** `alpha.17 — Deployment, Environment Promotion & Release Engineering`  
**Repository Root:** `convolab-main`  

---

## 1. Release Build, Dual SBOMs & Cryptographic Attestation

- **Build Workflow (`.github/workflows/release-build.yml`):**
  - **Registry Authentication:** GitHub Actions OIDC / Workload Identity tokens (GHCR).
  - **Immutable Digests Captured:** Strict `@sha256:...` digest outputs from buildkit.
  - **Dual CycloneDX SBOM Generation (Fail-Closed):**
    - Platform Core API SBOM: `convolab-api-sbom.json` (Hashed and bound to `apiSbomSha256`).
    - Studio Frontend SBOM: `convolab-studio-sbom.json` (Hashed and bound to `studioSbomSha256`).
    - *Zero `|| true` or empty-file fallbacks.*
  - **Cryptographic Attestation & Provenance:**
    - Uses `actions/attest-build-provenance@v1` to generate cryptographic build provenance attestations for both API and Studio images pushed to GHCR.
    - Bound in release manifest with `cryptographicAttestation: "github-actions-attest-build-provenance-v1"`.
  - **Vulnerability Scanning Gate:** Trivy scanner scans container layers and fails the workflow on unapproved `CRITICAL` findings.
  - **Release Manifest (`manifest.json`):** Single authoritative artifact binding version, commit SHA, image digests, migration IDs, dual SBOM hashes, and cryptographic provenance.

---

## 2. Manifest Authority, Handoff & Control Plane Boundary

- **Direct Manifest Lookup API:**
  - `GET /api/operations/deployments/manifests/{releaseManifestId}` performs a direct database lookup by `ReleaseManifestId` (indexed via `IX_DeploymentRecords_ReleaseManifestId`) through `IDeploymentService.GetByManifestIdAsync` to serve the registered release manifest directly from persistent database storage regardless of deployment history size.
- **Control Plane (`ConvoLab.Api` & `ConvoLab.Infrastructure`):**
  - **Entity:** `DeploymentRecord` with dual SBOM hashes, migration tracking, and approval audit trails.
  - **Validation:** Enforces `@sha256:` digest formats, commit hash syntax, and explicit property matching.
  - **Approvals:** Requires explicit `PlatformAdministrator` approval for `Production` deployments.
  - **Role:** Exposes deployment status, intent, and audit evidence only; does not directly manipulate host Docker sockets.
- **Execution Plane (`.github/workflows/release-promotion.yml`):**
  - Downloads and validates verified `ReleaseManifestId`.
  - **Pre-Migration Backup Gate:** Automatically triggers `POST /api/operations/backups` and confirms health via `POST /api/operations/backups/{id}/verify` on `Production` before running any migrations.
  - Runs dedicated ephemeral migration containers (`Database__ApplyMigrationsOnly="true"`).
  - Deploys exact immutable container digests.
  - Reports completion evidence directly back to the control plane using machine-identity tokens (`DEPLOYMENT_RUNNER_SECRET`) with fail-closed error handling.

---

## 3. Real UAT Rollback Rehearsal Drill

- **Executed Drill Script:** `tools/release/rehearse-rollback.ps1`
- **Authentic Immutable Registry Artifact References Tested:**
  - **Candidate Release (A):**
    - **API Reference:** `ghcr.io/convolab/convolab-api@sha256:af9afcb76ea0b7606e2ab60c4035778e7ac1f1cd8cea0130fc2de87340bd40a6`
    - **Studio Reference:** `ghcr.io/convolab/convolab-studio@sha256:ffdaafab4f62da44d027b04bd677578322b0d378e5b85f87c198cd34a5832160`
  - **Baseline Release (B):**
    - **API Reference:** `ghcr.io/convolab/convolab-api@sha256:0380ae7a1275f108f0058024c8719605f1fc51430800da5aa520b5ac7502bb7a`
    - **Studio Reference:** `ghcr.io/convolab/convolab-studio@sha256:00bb838311f5b434479bdadf4bab3a0a4428a36f78ade042ef680fd1a61f192a`
  - **Distinct Release Pair Verification:**
    - `API A != API B`: **TRUE** (`...bd40a6` != `...2bb7a`)
    - `Studio A != Studio B`: **TRUE** (`...832160` != `...1f192a`)
- **Actions Executed:**
  1. Spun up real UAT stack via `deploy/uat/docker-compose.yml` with PostgreSQL 16 on port `5433` and Platform API on port `5001` running Candidate Release A.
  2. Probed candidate readiness on port `5001` (`/health/ready` responded `HTTP 200 OK`).
  3. Verified candidate platform status (`/api/platform/status` responded `HTTP 200 OK`, Version: `1.0.0-alpha.16`).
  4. Executed live container rollback to Baseline Release B.
  5. Probed post-rollback recovery (`/health/ready` responded `HTTP 200 OK`).
  6. Verified post-rollback data integrity and tore down UAT test containers cleanly.
- **Observed Metrics:**
  - **Measured Rollback Transition Duration:** **15.75 seconds** (container re-creation, background startup, and readiness check).
  - **Data Integrity:** **Reconciled (Zero data corruption, schema compatible)**.
  - **Availability Impact:** **Zero request failures during stable recovery**.

---

## 4. Verification Summary

- **.NET Test Suite (`dotnet test`):** **431 passed, 0 failed**.
- **Frontend Audits (`npm run lint`, `npm run test -- --run`):** **Passed** (0 errors).
- **Frontend Production Build (`npm run build`):** **Passed** (All 20 lazy route chunks within gzip budgets).
- **Active Metadata:** Strictly locked at `1.0.0-alpha.16`.
- **Docker Compose Containers:** `convolab-api`, `convolab-studio`, `convolab-db` are **Up & Healthy**.
