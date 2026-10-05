# Deployment, Environment Promotion & Release Engineering (ALM)

ConvoLab follows an enterprise Application Lifecycle Management (ALM) strategy grounded in **Build Once, Promote Many**. The exact same immutable container manifests and agent configurations advance through environments (`Development` → `Staging/UAT` → `Production`) gated by source environment health checks and automated rollback recovery.

---

## 1. Immutable Artifact Promotion

1. **Build Once:**
   - The Release Build workflow compiles the .NET 8 Platform API and the React 19 Studio.
   - Generates immutable SHA-256 container digests:
     - `convolab-api@sha256:<digest>`
     - `convolab-studio@sha256:<digest>`
2. **Release Manifest (`release-manifest.json`):**
   - The single immutable promotion contract binding:
     - `releaseManifestId`
     - `releaseVersion`
     - `sourceCommitSha`
     - `apiImageDigest`
     - `studioImageDigest`
     - `migrationVersion`
     - `sbomSha256`
     - `buildTimestamp`
3. **Environment Deployment:**
   - Environment differences are injected solely through infrastructure configuration and external secrets (`ISecretStore`).

---

## 2. Multi-Environment Promotion Pipeline

The deployment service ([`DeploymentService.cs`](file:///c:/Users/W1022804/convolab-main/src/Infrastructure/ConvoLab.Infrastructure/Operations/Deployment/DeploymentService.cs)) manages candidate promotion across workspaces and runtime environments:

### Promotion Flow
1. Operator or CI/CD invokes `POST /api/operations/deployments/promote` with:
   - `ManifestId`
   - `SourceEnvironmentId` (e.g., Staging)
   - `TargetEnvironmentId` (e.g., Production)
   - `RequiredApproverRole` (e.g., `ReleaseManager` / `PlatformAdministrator`)
2. **Source Environment Health Gate:**
   - Before allowing promotion, the platform verifies that the source environment is actively healthy (status is `Healthy`, no critical operational anomalies).
   - If the source environment has active errors or fails readiness checks, promotion is blocked.
3. **Target Manifest Deployment:**
   - Deploys the candidate manifest to the target environment with traceable audit events (`DeploymentPromoted`).

---

## 3. Automated Rollback Recovery

In the event of runtime anomalies, elevated error rates, or post-deployment failures, the platform supports automated one-click rollback:

- **Endpoint:** `POST /api/operations/deployments/{id}/rollback`
- **Execution:**
  - Identifies the current failing deployment.
  - Queries the target environment's history to locate the most recent previously completed healthy deployment manifest.
  - Automatically spins up an expedited recovery deployment using the known-healthy manifest.
  - Records an immutable audit log (`DeploymentRollbackInitiated`) with cross-correlation.

---

## 4. Pre-Migration Backup Safety Gate

Any deployment against `Production` containing database schema migrations requires an active, verified backup snapshot prior to executing the migration container:
1. The deployment runner triggers `POST /api/operations/backups`.
2. The snapshot is verified via `POST /api/operations/backups/{id}/verify`.
3. If backup verification fails, the deployment halts immediately.

---

## 5. Operations API Endpoints

- `GET /api/operations/deployments` — List deployments
- `POST /api/operations/deployments` — Trigger new deployment
- `POST /api/operations/deployments/promote` — Promote release manifest across environments
- `POST /api/operations/deployments/{id}/rollback` — Execute automated rollback recovery
- `GET /api/operations/status` — Platform operational state & health
