# Implementation Plan: alpha.16 — Backup, Restore & Disaster Recovery

## Goal Description
ConvoLab currently tracks an abundance of mission-critical state in PostgreSQL, including governed conversational assets (prompts, workflows, policies), enterprise external identity links, sessions, and append-only Analytics outbox events. 

However, the operational `Backups` capability is hardcoded to `NotConfigured`. The goal of the `alpha.16` workstream is to implement PostgreSQL backup orchestration, track backup verification states in the Operations Center, and establish an automated or explicitly documented Disaster Recovery (DR) runbook ensuring strict RPO/RTO metrics and data integrity upon restore.

## Proposed Changes

### 1. Metadata and Workstream Promotion
Update all canonical source files to transition the active in-progress workstream from `alpha.15` to `alpha.16`. The actual release metadata (`1.0.0-alpha.15`) remains stable while work on `alpha.16` is active.

#### [MODIFY] [README.md](file:///C:/Users/W1022804/convolab-main/README.md)
Update the `Current workstream` from `alpha.15` to `alpha.16 — Backup, Restore & Disaster Recovery`.

#### [MODIFY] [ROADMAP.md](file:///C:/Users/W1022804/convolab-main/ROADMAP.md)
Update the roadmap to show `alpha.15` as delivered and `alpha.16` as the active workstream.

#### [MODIFY] [docs/Roadmap.md](file:///C:/Users/W1022804/convolab-main/docs/Roadmap.md)
Reflect the new workstream priority.

### 2. Application Layer Contracts
Define the interfaces and DTOs that will govern Backup operations.

#### [MODIFY] [src/Application/ConvoLab.Application/Operations/OperationalContracts.cs](file:///C:/Users/W1022804/convolab-main/src/Application/ConvoLab.Application/Operations/OperationalContracts.cs)
- Add `IBackupEvidenceSource` interface.
- Add `BackupEvidence` record containing:
  - `OperationalDependencyState State`
  - `DateTimeOffset? LastBackupCompletedAt`
  - `DateTimeOffset? LastBackupVerifiedAt`
  - `long? LastBackupSizeBytes`
  - `TimeSpan? ConfiguredRpo`
  - `TimeSpan? ConfiguredRto`

### 3. Infrastructure Implementation
Implement the adapter that interacts with the PostgreSQL layer (or file system) to perform and verify backups.

#### [NEW] [src/Infrastructure/ConvoLab.Infrastructure/Operations/PostgresBackupEvidenceSource.cs](file:///C:/Users/W1022804/convolab-main/src/Infrastructure/ConvoLab.Infrastructure/Operations/PostgresBackupEvidenceSource.cs)
- Implement `IBackupEvidenceSource`.
- For `alpha.16`, this service will monitor a designated backup directory (or cloud storage abstraction if applicable) and evaluate backup files to calculate RPO compliance and report `Configured` or `LiveValidated`.

#### [MODIFY] [src/Infrastructure/ConvoLab.Infrastructure/DependencyInjection.cs](file:///C:/Users/W1022804/convolab-main/src/Infrastructure/ConvoLab.Infrastructure/DependencyInjection.cs)
- Register `IBackupEvidenceSource` in the DI container.

### 4. API Layer
Expose the new real telemetry instead of the hardcoded `NotConfigured` response.

#### [MODIFY] [src/Api/ConvoLab.Api/Controllers/OperationsController.cs](file:///C:/Users/W1022804/convolab-main/src/Api/ConvoLab.Api/Controllers/OperationsController.cs)
- Inject `IBackupEvidenceSource` into `OperationsController`.
- Update the `[HttpGet("backups")]` endpoint to return the snapshot from `IBackupEvidenceSource` instead of the hardcoded `NotConfigured` anonymous object.

### 5. Frontend (React/Vite)
Update the Operations UI to surface the new backup telemetry.

#### [MODIFY] [web/src/pages/OperationsPage.tsx](file:///C:/Users/W1022804/convolab-main/web/src/pages/OperationsPage.tsx)
- Update the UI to render `LastBackupCompletedAt`, `ConfiguredRpo`, and `LastBackupVerifiedAt` when the state is `Configured` or `LiveValidated`.

### 6. Documentation & Runbooks
Add the formal Disaster Recovery runbook to prove that the platform can recover safely.

#### [NEW] [docs/operations/DisasterRecovery.md](file:///C:/Users/W1022804/convolab-main/docs/operations/DisasterRecovery.md)
- Detail the `pg_dump` / `pg_restore` commands required.
- Detail how `SharedFileSystem` data protection keys must be backed up to preserve session integrity.
- Detail the idempotency checks: explaining how the Analytics Outbox and fencing tokens handle a restored database timeline.

#### [MODIFY] [docs/operations/OperationsCenter.md](file:///C:/Users/W1022804/convolab-main/docs/operations/OperationsCenter.md)
- Update the documentation to reflect that Backups now report `Configured` and track RPO metrics.

## Open Questions

> [!WARNING]
> **Backup Execution Scope:** Should `ConvoLab.Infrastructure` *actively* execute the `pg_dump` via a `Process.Start()` wrapper/background worker? Or should ConvoLab merely *monitor* a backup directory populated by an external cron/Kubernetes CronJob? For enterprise platforms, it is usually preferred that the app monitors, rather than executes, infrastructure-level dumps.

## Verification Plan

### Automated Tests
- Update `ConvoLab.Api.IntegrationTests` to assert that `/api/operations/backups` returns a properly structured payload rather than `NotConfigured`.
- Update `web/tests/browser/operations.spec.ts` to expect `Configured` or `LiveValidated` instead of `NotConfigured` for the Backups section.

### Manual Verification
- Stop the API. Delete the local SQLite/PostgreSQL database.
- Follow the runbook in `DisasterRecovery.md` to restore a backup.
- Start the API and verify that sessions remain valid (using backed-up data protection keys), no outbox events are duplicated, and the Operations Center reports a recent `LastBackupCompletedAt`.
