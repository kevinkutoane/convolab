# Persistence

PostgreSQL is the Docker/production-oriented adapter; SQLite remains a local development option. Application depends on repository ports and `IUnitOfWork`, not Entity Framework. Infrastructure maps persistence records to application/domain state.

Mutable Prompt and Knowledge records carry monotonic `Revision` values configured as EF Core concurrency tokens. Clients send `expectedRevision`; stale updates return HTTP 409 rather than silently overwriting changes.

Development may apply migrations on startup. Production deployments should run migrations as a controlled release step before application rollout and verify the pending-migration readiness check.
