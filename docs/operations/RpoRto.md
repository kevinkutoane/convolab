# Recovery Point Objective (RPO) & Recovery Time Objective (RTO)

## Initial Internal-UAT Targets

- **Target RPO**: 24 hours (configurable via `Operations:Backups:ExpectedRpoMinutes = 1440`).
- **Target RTO**: 4 hours.

## Measured Drill Evidence

During internal rehearsal testing on the local SQLite / PostgreSQL development harness:
- **Backup Archive Generation**: ~1.2s (Database dump + Documents + Data Protection).
- **Checksum Verification**: ~40ms.
- **Restore & Database Rehydration**: ~1.8s.
- **Full Recovery Cycle (Observed RTO)**: < 10 seconds for local dev dataset.

*Note: Production RTO will scale with total knowledge document storage volume and database table size.*
