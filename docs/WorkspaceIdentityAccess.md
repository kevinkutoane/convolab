# Workspace, Identity and Access Control v1

Workspace/IAM is the alpha.12 security boundary for every functional ConvoLab capability.

## Authentication

Interactive users authenticate with a high-entropy opaque token in the `convolab_session` HttpOnly, SameSite cookie. PostgreSQL stores only its SHA-256 hash, active workspace, expiry, rotation link, last-seen time, and revocation state. Unsafe cookie-authenticated requests require the `X-XSRF-TOKEN` antiforgery header. Login failures are generic and throttled.

Local passwords use ASP.NET Core `PasswordHasher`. The local provider is an adapter boundary for future Entra ID or OIDC support. No password is generated, shipped, returned, or logged.

Service accounts use one-time `clsa_<id>_<secret>` bearer credentials. Only the secret hash is retained. Scopes, expiry, rotation, revocation, and last use are persisted and audited.

## Workspace isolation and RBAC

The authenticated principal supplies trusted user, organisation, workspace, membership, role, session, and actor-type claims. EF root queries are filtered by the request workspace, child lookups verify a visible parent, and cross-workspace identifiers return not found. Platform-owned built-in plugins remain visible but immutable to workspace users.

Fixed permissions are assigned centrally to Administrator, Engineer, Reviewer, Operator, and Viewer. Controllers do not compare role names. Workspace and member management requires Administrator permission; organisation creation and lifecycle require the bootstrap Platform Administrator.

## Bootstrap

Configure these values through user-secrets or deployment environment variables:

```text
Bootstrap__Administrator__Email=admin@example.com
Bootstrap__Administrator__Password=<injected secret of at least 12 characters>
```

If no password is supplied, the deterministic ConvoLab organisation, Default Workspace, bootstrap identity, and membership are created without an active credential. Readiness reports `setupRequired`; no generated password is logged.

## Deferred

Custom roles, production SSO, secret-vault integration, environment promotion, cross-organisation sharing, and public SaaS onboarding are outside alpha.12.
