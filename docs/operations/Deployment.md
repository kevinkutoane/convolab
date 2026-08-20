# Deployment, Environment Promotion & Release Engineering

ConvoLab Studio follows a strict **Build Once, Promote Many** release philosophy. The exact same immutable container images built from a verified source commit move across environments (`Development` → `UAT` → `Production`).

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

## 2. Pre-Migration Backup Safety Gate

Any deployment against `Production` containing database schema migrations requires an active, verified backup snapshot prior to executing the migration container:
1. The deployment runner triggers `POST /api/operations/backups`.
2. The snapshot is verified via `POST /api/operations/backups/{id}/verify`.
3. If backup verification fails, the deployment halts immediately.

---

## 3. Rollback Decision Tree

- **Application-Only Rollback:** When schema is backward-compatible, deploy the previous `apiImageDigest` and `studioImageDigest`.
- **Forward-Fix:** Deploy a fast forward-fix image when schema has advanced compatibly.
- **Database Restore:** Trigger `tools/operations/restore.sh --backup-id <id> --allow-destructive` if data or schema corruption occurs.
