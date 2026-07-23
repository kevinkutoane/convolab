# ConvoLab Platform and Studio v1.0.0-alpha.12

Alpha.12 introduces the Workspace, Identity and Access Control v1 security boundary over the stabilized alpha.11 capability baseline.

## Delivered

- PostgreSQL-backed opaque interactive sessions in HttpOnly SameSite cookies, with expiry, rotation, revocation, logout, antiforgery validation, and throttled generic login failures.
- Provider-neutral local credentials using ASP.NET Core password hashing and scoped one-time `clsa_` service credentials stored only as hashes.
- Organisations, workspaces, memberships, fixed RBAC permissions, invitations, service-account lifecycle, explicit audit scopes, and final-Administrator protection.
- Mandatory ownership backfill for existing capability roots while preserving identifiers, relationships, correlations, and ZAR values.
- Protected Studio routes, login and selection screens, stale-safe workspace switching, access restrictions, administration tabs, and user/session controls.

## Release state

Workspace/IAM is marked active during acceptance. It becomes stable only after the complete PostgreSQL upgrade, restart persistence, tenant-isolation, Playwright role matrix, Docker, and deterministic cross-capability suites pass. No default administrator password is distributed.
