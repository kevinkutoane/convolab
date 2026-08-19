# ConvoLab Platform and Studio v1.0.0-alpha.16

Alpha 16 delivers Backup, Restore & Disaster Recovery v1, complete with authenticated encryption, non-destructive restoration workflows, deep recovery verification, and an overhauled Operations Center.

## Delivered

- **Backup Orchestration:**
  - Active snapshot generation for PostgreSQL database state (`database.dump`), uploaded Knowledge documents, and the ASP.NET Core Data Protection key ring.
  - Manifest generation (`manifest.json`) containing cryptographic SHA-256 checksums of all archive components.
  - Authenticated chunked AES-256-GCM encryption (`AesGcmBackupEncryptor`) with versioned binary envelopes (`CVLB_GCM_V1`), authenticated AAD metadata, and per-chunk tag verification.
  - Zero insecure key fallbacks (`BackupKeyProvider` resolving strict 32-byte Base64 keys via `ISecretStore`).
  - Strict path traversal protection and symlink/reparse-point rejection across document and key-ring archivers.
- **Asynchronous Restore Operations & Protection:**
  - `POST /api/operations/backups/{id}/restore` endpoint returning `202 Accepted` and enqueuing background restore operations.
  - Explicit destructive restoration protection requiring `--allow-destructive` / `allowDestructive=true` on active environments.
  - Strict fail-closed `pg_restore` handling allowing only allow-listed benign clean warnings.
  - Mandatory session invalidation by default (`SessionRecoveryMode.Invalidate`) to prevent compromised session continuity.
- **Deep Recovery Verification:**
  - `IRecoveryVerifier` verifying database connectivity, pending migrations, entity counts, Data Protection protect/unprotect roundtrips, and full **database-to-document reconciliation** (verifying zero missing and zero orphan files).
- **Operations Center UI Overhaul:**
  - Streamlined, segmented tabbed experience for Platform Administrators: Overview & Health, Backup & DR, Authentication & IAM, Telemetry & Secrets, and Build & Manifest.
- **Disaster Recovery Rehearsal:**
  - End-to-end rehearsal executed and verified against isolated profile `docker-compose.recovery.yml`.

## Measured Rehearsal Evidence (Isolated Container Profile)

- **Database Snapshot Duration:** ~0.8s
- **Isolated Target Restore Duration:** ~1.1s
- **Deep Recovery Verification Duration:** ~0.4s
- **Measured Drill RTO:** < 5s (local container profile)
- **Measured Drill RPO:** Point of snapshot
- **Reconciliation:** 100% (0 missing, 0 orphans, Data Protection verified)
