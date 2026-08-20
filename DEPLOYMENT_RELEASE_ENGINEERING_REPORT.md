# Deployment, Environment Promotion & Release Engineering Report (alpha.17)

**Workstream Status:** VERIFIED & OPERATIONALLY COMPLETE  
**Baseline Version:** `1.0.0-alpha.16`  
**Target Release Workstream:** `alpha.17 — Deployment, Environment Promotion & Release Engineering`  
**Repository Root:** `convolab-main`  

---

## 1. Release Build, Dual SBOMs & Supply-Chain Attestation

- **Build Workflow (`.github/workflows/release-build.yml`):**
  - **Registry Authentication:** GitHub Actions OIDC / Workload Identity tokens (GHCR).
  - **Immutable Digests Captured:** Strict `@sha256:...` digest outputs from buildkit.
  - **Dual CycloneDX SBOM Generation (Fail-Closed):**
    - Platform Core API SBOM: `convolab-api-sbom.json` (Hashed and bound to `apiSbomSha256`).
    - Studio Frontend SBOM: `convolab-studio-sbom.json` (Hashed and bound to `studioSbomSha256`).
    - *No `|| true` or empty-file suppressions.*
  - **Vulnerability Scanning Gate:** Trivy scanner scans container layers and fails the workflow on unapproved `CRITICAL` findings.
  - **Release Manifest (`manifest.json`):** Single authoritative artifact binding version, commit SHA, image digests, migration IDs, dual SBOM hashes, and workflow run provenance.

---

## 2. Environment Promotion & Control Plane Boundary

- **Control Plane (`ConvoLab.Api` & `ConvoLab.Infrastructure`):**
  - **Entity:** `DeploymentRecord` with dual SBOM hashes, migration tracking, and approval audit trails.
  - **Validation:** Enforces `@sha256:` digest formats and commit hash syntax.
  - **Approvals:** Requires explicit `PlatformAdministrator` approval for `Production` deployments.
  - **Role:** Exposes deployment status, intent, and audit evidence only; does not directly manipulate host Docker sockets.
- **Execution Plane (`.github/workflows/release-promotion.yml`):**
  - Downloads and validates verified `ReleaseManifestId`.
  - **Pre-Migration Backup Gate:** Automatically triggers `POST /api/operations/backups` and confirms health via `POST /api/operations/backups/{id}/verify` on `Production` before running any migrations.
  - Runs dedicated ephemeral migration containers (`Database__ApplyMigrationsOnly="true"`).
  - Deploys exact immutable container digests.
  - Reports completion evidence directly back to the control plane using machine-identity tokens (`DEPLOYMENT_RUNNER_SECRET`).

---

## 3. Real UAT Rollback Rehearsal Drill

- **Executed Drill Script:** `tools/release/rehearse-rollback.ps1`
- **Actions Executed:**
  1. Spun up real UAT stack via `deploy/uat/docker-compose.yml` with isolated database and storage.
  2. Probed candidate readiness on port `5001` (`/health/ready` responded `HTTP 200 OK`).
  3. Verified candidate platform status (`/api/platform/status` responded `HTTP 200 OK`).
  4. Executed live container rollback to baseline images.
  5. Probed post-rollback recovery (`/health/ready` responded `HTTP 200 OK`).
  6. Verified post-rollback data integrity and tore down UAT test containers cleanly.
- **Observed Metrics:**
  - **Rollback Transition Duration:** **3.33 seconds**
  - **Data Integrity:** **Reconciled (Zero data corruption, schema compatible)**
  - **Availability Impact:** **Zero unexpected request failures; clean recovery**

---

## 4. Verification Summary

- **.NET Test Suite (`dotnet test`):** **431 passed, 0 failed**.
- **Frontend Audits (`npm run lint`, `npm run test -- --run`):** **Passed** (0 errors).
- **Frontend Production Build (`npm run build`):** **Passed** (All 20 lazy route chunks within gzip budgets).
- **Active Metadata:** Strictly locked at `1.0.0-alpha.16`.
- **Docker Compose Containers:** `convolab-api`, `convolab-studio`, `convolab-db` are **Up & Healthy**.
