# Disaster Recovery Environment

To perform safe, non-destructive disaster recovery drills without touching production data, use the isolated recovery profile:

```bash
docker compose -f docker-compose.recovery.yml up --build
```

## Isolation Guarantees

- **Separate Ports**: Recovery API listens on port `5001` (avoiding port `5000` conflict). PostgreSQL listens on port `5433`.
- **Separate Volumes**: Uses dedicated volumes `recovery_pgdata`, `recovery_keys`, `recovery_backups`, and `recovery_documents`.
- **Zero Production Credentials**: Uses local dummy credentials and deterministic AI providers by default.
