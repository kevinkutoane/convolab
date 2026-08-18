# Disaster Recovery Runbook

This document defines the automated orchestration and manual recovery procedures for the `alpha.16 — Backup, Restore & Disaster Recovery` workstream.

## PostgreSQL Backup Scope

ConvoLab natively tracks critical operational state inside its PostgreSQL database, including:
- Identity mappings (Local & Entra Hybrid)
- Audit log evidence
- Platform Analytics events (Append-only outbox)
- Workflows, Prompts, Policies, and plugin registrations

The backup envelope **must** encompass the entire PostgreSQL dataset.

## Execution Responsibility

**ConvoLab monitors backups; it does not execute them.**
It is an anti-pattern for an ASP.NET Core process to directly shell-execute `pg_dump`. 
The infrastructure team must configure an external cron job or Kubernetes `CronJob` to execute backups and drop them into a secure storage path (e.g., local mount, S3, or Azure Blob Storage).

The ConvoLab API expects the `Operations:Backups:DirectoryPath` and `Operations:Backups:ExpectedRpoMinutes` settings to point to this location. The `IBackupEvidenceSource` adapter actively polls this path to calculate Recovery Point Objective (RPO) health telemetry for the Operations Center.

### Suggested Cron Job Command
```bash
pg_dump -Fc -h $PG_HOST -U $PG_USER $PG_DATABASE > /path/to/backups/convolab_backup_$(date +%Y%m%d%H%M%S).dump
```

## Restore Procedure (Runbook)

If the database is lost, follow these steps to restore service.

### 1. Stop ConvoLab API and Workers
To prevent race conditions with Analytics outbox dispatchers or identity changes during the restore, completely stop the ASP.NET Core API process.

```bash
docker compose stop api
```

### 2. Restore PostgreSQL State
Use `pg_restore` to rehydrate the state from the last valid `.dump` file.
```bash
pg_restore -c -d $PG_DATABASE -h $PG_HOST -U $PG_USER /path/to/backups/convolab_backup_target.dump
```

### 3. Restore Data Protection Keys
ConvoLab relies on an explicit Key Ring directory (`DataProtection:KeyRingPath`) and X.509 PEM certificates to encrypt application sessions and antiforgery tokens. 
If the file system was also lost, you **must** restore these files to the exact paths configured in `appsettings.json`. If you fail to restore the key ring, all active user sessions will be invalidated immediately upon restart, forcing all users to re-authenticate.

### 4. Idempotency Checks (Analytics Outbox)
ConvoLab is designed to be restart-safe. The Analytics outbox relies on fencing tokens and pessimistic worker leases (`OperationalWorkerHeartbeats`).
Upon restart, the background workers will safely claim any outbox items that were restored from the snapshot but have not yet been dispatched. Because correlation IDs and terminal event boundaries are strictly managed, restoring an old snapshot will **not** cause duplicate remote dispatches or corrupted aggregation windows.

### 5. Restart ConvoLab API
Once the database and Data Protection keys are verified, start the application.
```bash
docker compose start api
```

### 6. Verify Operations Evidence
Log in as a Platform Administrator and navigate to **Settings > Operations**. 
Verify that the `Backups` operational telemetry correctly displays `Configured`, and the `Last backup completed` timestamp matches the time of your restored snapshot (or a newly generated post-restore snapshot).
