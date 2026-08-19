# Backup & Disaster Recovery Threat Model

## Threat 1: Archive Extraction & Path Traversal Attacks
- **Mitigation**: `DocumentStorageArchiver` and `DataProtectionArchiver` strictly sanitize every entry path against the configured root (`StartsWith(fullRootPath)`). Archive paths containing `../` or absolute drive roots are rejected.

## Threat 2: Plaintext Secret Leakage in Backups
- **Mitigation**: The manifest structure explicitly excludes passwords, client secrets, and raw API keys. `ISecretStore` references are preserved, but the secret values themselves remain in external vaults.

## Threat 3: Accidental Production Overwrite
- **Mitigation**: The REST API endpoint `/api/operations/backups/{id}/restore` returns a `400 Bad Request` Problem Details response unless `allowDestructive=true` is explicitly provided. Tooling scripts require `--allow-destructive`.

## Threat 4: Compromised Session Replay post-Restore
- **Mitigation**: `SessionRecoveryMode.Invalidate` is the mandatory default, requiring users and external Entra accounts to re-authenticate following a restore.
