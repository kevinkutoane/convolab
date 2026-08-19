# Backup & Restore Operations

ConvoLab provides active backup creation, cryptographic verification, and controlled restoration tooling for the platform's non-rebuildable state.

## Scope of Backups

Every backup artifact produced by ConvoLab packages three primary components:
1. **PostgreSQL Database State (`database.dump`)**: Using `pg_dump -Fc` custom format containing all relational entities, users, workspaces, external identities, policies, and outbox events.
2. **Knowledge Documents (`documents.tar.zst`)**: Preserving all uploaded source documents and chunks managed under `IKnowledgeDocumentStorage`.
3. **Data Protection Key Ring (`dataprotection.tar.zst`)**: Preserving the active XML key ring necessary for decrypting existing application sessions and antiforgery cookies.

## Encryption Architecture

Backups are encrypted using AES-256 authenticated encryption. The encryption key is resolved dynamically through the existing `ISecretStore` abstraction:
- **Development**: Optional unencrypted or local fallback key.
- **UAT / Containerized**: Resolved via `env:BACKUP_ENCRYPTION_KEY` or `docker-secret:backup-key`.
- **Production**: Managed vault reference (`azure-key-vault:backup-encryption-key`).

The encryption key is **never** included in manifests, archives, or log evidence.

## Retention Policy

Default internal-UAT retention policies:
- **Daily Backups**: Retained for 14 days
- **Weekly Backups**: Retained for 8 weeks
- **Monthly Backups**: Retained for 6 months

Retention cleanup ensures that at least one verified backup point is always preserved.
