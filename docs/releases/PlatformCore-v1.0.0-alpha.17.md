# ConvoLab Platform and Studio v1.0.0-alpha.17

Alpha 17 delivers **Deployment, Environment Promotion & Release Engineering v1**, establishing immutable release-candidate assembly, dual-SBOM generation, cryptographic build provenance attestation, container vulnerability gates, automated pre-migration backup enforcement, and an audited Environment Promotion control plane inside the Operations Center.

## Delivered

- **Immutable Release Build Pipeline:**
  - Build-once, promote-many container publishing to GitHub Container Registry (GHCR) using workload identity / OIDC authentication.
  - Strict capture and binding of immutable `@sha256:...` digest references for both API and Studio container images.
  - Dual CycloneDX Software Bill of Materials (SBOM) generation for Platform Core .NET (`convolab-api-sbom.json`) and Studio React (`convolab-studio-sbom.json`) with fail-closed release gates.
  - Cryptographic build provenance attestations generated via `actions/attest-build-provenance@v1`.
  - Container vulnerability scanning gate via Trivy failing the pipeline on unapproved `CRITICAL` findings.
  - Authoritative machine-readable `release-manifest.json` binding release version, source commit SHA, immutable API and Studio digests, migration IDs, dual SBOM hashes, and cryptographic provenance.

- **Deployment Control Plane & Operations Center UI:**
  - `DeploymentRecord` persistence in PostgreSQL with indexed `ReleaseManifestId` lookups.
  - Direct manifest handoff API: `GET /api/operations/deployments/manifests/{releaseManifestId}`.
  - `GET /api/operations/deployments`: Real-time topology showing `Development`, `UAT`, and `Production` active digests and version state.
  - Audited deployment history table with commit SHA, approver, pre-migration backup ID, and interactive Platform Administrator approval gates for Production promotions.

- **Execution Plane & Promotion Runner:**
  - Automated environment promotion workflow (`release-promotion.yml`) decoupled from local network assumptions.
  - Automated **Pre-Migration Backup Gate** on Production: creates an authenticated alpha.16 snapshot via `POST /api/operations/backups` and confirms health via `POST /api/operations/backups/{id}/verify` before applying migrations.
  - Ephemeral dedicated migration container stage (`Database__ApplyMigrationsOnly="true"`).
  - Machine-identity token reporting (`DEPLOYMENT_RUNNER_SECRET`) with fail-closed completion handling.

- **Live UAT Rollback Drill:**
  - Verified container switch rollback between two distinct immutable registry release pairs in 15.75–23.56 seconds.
  - Verified `/health/ready` probe, `/api/platform/status` smoke probe, and database data reconciliation.
