# Recovery Runbook & Scenarios

This runbook outlines how operators handle real-world recovery scenarios in ConvoLab.

## Scenario A: Total PostgreSQL Failure
1. Provision a fresh PostgreSQL instance or spin up the recovery container.
2. Select the latest verified backup from the storage path.
3. Execute `./tools/operations/restore.sh /path/to/backup --allow-destructive`.
4. Run `./tools/operations/recovery-verify.sh http://localhost:5000`.
5. Verify that users, workspaces, and policy rules match expected state.

## Scenario B: Corrupt Backup Artifact
1. When a restore or verification is attempted, SHA-256 checksums are evaluated against `checksums.sha256`.
2. The restore fails immediately before modifying any database tables.
3. Operator selects the previous valid point-in-time backup.

## Scenario C: Document Storage Discrepancy (Reconciliation)
1. Following `documents.tar.zst` extraction, `recovery-verify.sh` compares the count of physical files against the `KnowledgeDocuments` database table.
2. Any missing or orphaned document records are flagged for administrative review.
