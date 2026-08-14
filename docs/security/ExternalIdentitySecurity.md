# External identity security

The permanent key is `(Provider, Issuer, Subject)`. Email and display name are mutable profile observations recorded only at last login. ConvoLab never infers platform or workspace roles from Entra groups, token roles, job title, department, domain, or tenant membership.

Unknown identities cannot create users or memberships. Linking requires a cryptographically random, hashed, expiring, revocable, single-use invitation for the configured provider and tenant plus a verified matching email. Existing local users are not linked by email alone.

External identity mutations require `PlatformAdministrator` and optimistic concurrency. Disablement revokes all related sessions. Disabling the final usable method requires explicit confirmation, and administrators cannot remove their own final usable method. Identity evidence is retained rather than cascade-deleted with user lifecycle changes.

Raw ID/access/refresh tokens, authorization codes, state, nonce, client secrets, invitation tokens, subjects, and email claims are prohibited in logs, traces, metric labels, Analytics payloads, audit details, Problem Details, and Operations responses. OIDC tokens are neither persisted nor used as ConvoLab sessions.

The main session cookie stays Strict. OIDC nonce and correlation cookies alone are cross-site compatible and always secure. Forwarded host/protocol values are accepted only from explicit trusted proxies; Production requires HTTPS, an exact public origin, and an AllowedHosts entry matching the callback host.
