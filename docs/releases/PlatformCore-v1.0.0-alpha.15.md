# ConvoLab Platform and Studio v1.0.0-alpha.15

Alpha 15 delivers Microsoft Entra ID integration, External Identities, Hybrid Authentication, invitation-based linking, and hardened operational evidence.

## Delivered

- **Microsoft Entra ID & External Identities:**
  - Standards-based Microsoft Entra ID (OIDC with authorization code flow & PKCE).
  - Strongly typed Local, Entra, and Hybrid authentication modes.
  - External identity persistence using immutable `Provider` + `Issuer` + `Subject` tuples.
  - Secure invitation-based first-login identity linking (authoritative link authorization without permanent email reliance).
  - Relaxed dependency on `email_verified` claim for invitation linking while enforcing strict tenant authority validation.
  - Safe OIDC state, nonce, correlation, issuer, audience, and lifetime validation.

- **Session & Identity Security:**
  - ConvoLab issues its own opaque, revocable application session cookie upon successful OIDC exchange.
  - Session is persisted to the database before the cookie is issued.
  - External logout and identity-administration session revocation support.

- **Break-Glass Authentication Hardening:**
  - Dedicated failure protection, concurrency control, and rate limiting for emergency break-glass admin login.
  - Temporary account-level break-glass lockout policy.
  - Generic authentication failure responses preventing username enumeration.

- **Operational Evidence & Analytics:**
  - Operations Center exposes sanitized, aggregate authentication evidence.
  - Framework-level OIDC failures persisted as safe, deduplicated operational evidence.
  - Trusted Analytics mapping for authentication failure events.
  - Complete exclusion of sensitive tokens, credentials, subjects, and secrets from logs and telemetry.

## Validation Status

- **Implemented & Verified:** Full regression suite, deterministic/stub identity provider tests, unit tests, integration tests, and Playwright browser smoke tests passing.
- **Provider Acceptance:** `StubValidated`
- **Live Microsoft Entra Tenant Validation:** `Not executed` (requires live enterprise tenant configuration in staging/production environments).

## Privacy Boundary

Authentication and operational evidence contain only sanitized metadata. Provider tokens, authorization codes, user credentials, secrets, client secrets, and full user subjects are excluded from logs, health reports, and analytics.
