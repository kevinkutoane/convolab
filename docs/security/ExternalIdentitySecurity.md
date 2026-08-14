# External identity security

The permanent key is `(Provider, Issuer, Subject)`. Email and display name are mutable profile observations recorded only at last login. ConvoLab never infers platform or workspace roles from Entra groups, token roles, job title, department, domain, or tenant membership.

Unknown identities cannot create users or memberships. Linking requires a cryptographically random, hashed, expiring, revocable, single-use invitation for the configured provider and tenant. A usable `email` claim is optional corroboration and must match; its absence is allowed. `preferred_username`, `upn`, and `email_verified` are not linking evidence. Existing local users are never located or linked by email alone.

The external identity key is persisted as provider, issuer, subject, and tenant. Identity creation, invitation consumption, opaque-session persistence, audit, and outbox evidence share one transaction. Cookie issuance occurs only after commit, and concurrency or commit failure rolls back the link and issues no application cookie.

External identity mutations require `PlatformAdministrator` and optimistic concurrency. Disablement revokes all related sessions. Disabling the final usable method requires explicit confirmation, and administrators cannot remove their own final usable method. Identity evidence is retained rather than cascade-deleted with user lifecycle changes.

Raw ID/access/refresh tokens, authorization codes, state, nonce, client secrets, invitation tokens, subjects, and email claims are prohibited in logs, traces, metric labels, Analytics payloads, audit details, Problem Details, and Operations responses. OIDC tokens are neither persisted nor used as ConvoLab sessions.

The main session cookie stays Strict. OIDC nonce and correlation cookies alone are cross-site compatible and always secure. Forwarded host/protocol values are accepted only from explicit trusted proxies; Production requires HTTPS, an exact public origin, and an AllowedHosts entry matching the callback host.
