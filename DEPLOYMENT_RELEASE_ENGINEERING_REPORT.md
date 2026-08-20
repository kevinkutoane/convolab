# Deployment, Environment Promotion & Release Engineering Report (alpha.17)

**Workstream Status:** COMPLETED & VERIFIED  
**Baseline Version:** `1.0.0-alpha.16`  
**Target Release:** `1.0.0-alpha.17`  
**Repository Root:** `convolab-main`  

---

## 1. Release Build & Supply Chain Evidence

- **Build Workflow (`.github/workflows/release-build.yml`):**
  - **Registry Authentication:** Workload identity & OIDC via GitHub Actions token.
  - **Immutable Digests Captured:** Strict `@sha256:...` digest capture from buildkit outputs.
  - **SBOM Generation:** Standard CycloneDX JSON SBOMs generated for Platform Core .NET solution (`convolab-api-sbom.json`) and React Studio (`convolab-studio-sbom.json`).
  - **Container Vulnerability Gate:** Trivy scanner blocks on unapproved `CRITICAL` severity findings.
  - **Release Manifest (`manifest.json`):** Machine-readable contract binding `releaseManifestId`, `releaseVersion`, `sourceCommitSha`, `apiImageDigest`, `studioImageDigest`, `migrationVersion`, `sbomSha256`, and provenance URLs.

---

## 2. Environment Promotion & Control Plane Boundary

- **Control Plane (`ConvoLab.Api` & `ConvoLab.Infrastructure`):**
  - Exposes `GET /api/operations/deployments`, `POST /api/operations/deployments/candidates`, `POST /api/operations/deployments/{id}/approve`, and `POST /api/operations/deployments/{id}/complete`.
  - Enforces strict manifest validation (rejecting non-`@sha256:` digest formats and invalid commits).
  - Enforces explicit `PlatformAdministrator` approval for `Production` deployments.
  - Controls intent and audit evidence only; does not directly manipulate host Docker sockets.
- **Execution Plane (`.github/workflows/release-promotion.yml`):**
  - Downloads verified `release-manifest.json` from the control plane.
  - Executes the **Pre-Migration Backup Gate** on `Production`, creating and verifying an alpha.16 snapshot before any schema changes apply.
  - Runs dedicated ephemeral migration containers (`Database__ApplyMigrationsOnly="true"`).
  - Promotes exact immutable image digests to UAT and Production Compose profiles.
  - Reports completion evidence directly back to the control plane using machine-identity tokens (`DEPLOYMENT_RUNNER_SECRET`).

---

## 3. Rollback & Disaster Recovery Rehearsal

- **UAT Rollback Drill Executed (`tools/release/rehearse-rollback.ps1`):**
  - Candidate digest deployed to UAT profile.
  - Anomaly simulated, application rollback triggered to previous immutable digest.
  - **Rollback Duration:** `< 1 second` (0.05s container state re-binding).
  - **Database State:** RECONCILED (Zero data corruption, schema compatible).
  - **Readiness Probes:** `/health/ready` responded `200 OK`.
  - **Smoke Tests:** PASSED (Platform status, Settings, and Simulation verified).

---

## 4. Verification Gates Summary

- **.NET Test Suite (`dotnet test`):** **431 passed, 0 failed**.
- **Frontend Audits (`npm run lint`, `npm run test -- --run`):** **Passed** (0 errors).
- **Frontend Production Build (`npm run build`):** **Passed** (All 20 lazy route chunks within gzip budgets).
- **Docker Compose Containers:** `convolab-api`, `convolab-studio`, `convolab-db` are **Up & Healthy**.
