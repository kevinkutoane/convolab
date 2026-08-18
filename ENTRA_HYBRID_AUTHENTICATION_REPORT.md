# Entra Hybrid Authentication Evidence Report

Workstream: `alpha.15 — Microsoft Entra ID, External Identities & Hybrid Authentication`
Repository root: `convolab-main`
Active release metadata: `1.0.0-alpha.14`
Evidence date: 2026-08-14

## Implementation summary

- Strongly typed `Local`, `Entra`, and `Hybrid` modes control the public authentication options and login experience.
- ASP.NET Core OpenID Connect uses tenant-specific authorization-code flow, PKCE, framework state/nonce/correlation validation, strict issuer/audience/signature/lifetime validation, secure temporary cookies, and asynchronous client-secret resolution.
- External identities are keyed uniquely by provider, issuer, and subject. Email is mutable profile/invitation evidence and is never the permanent identity key.
- Existing linked identities resolve only to active ConvoLab users. Unknown identities are denied unless a valid tenant-bound, single-use invitation is present. A usable `email` claim must match; a missing claim is allowed. `preferred_username`, `upn`, and `email_verified` provide no linking authority, and email equality alone never links a local account.
- Successful OIDC callbacks require the framework's already matched `TokenValidatedContext.Nonce` evidence before beginning database work, then atomically create the identity when needed, consume the invitation, persist the opaque hashed-token ConvoLab session, audit, and outbox evidence. Only the post-commit ticket event can issue the application cookie. Entra tokens are not saved.
- Local logout always revokes the ConvoLab session; an Entra session can additionally initiate framework-managed external logout.
- Break glass is off by default, uses a separate endpoint, limiter, failure counter, optimistic-concurrency revision, and lockout state, and is restricted to an active Platform Administrator with a local credential. It creates a one-hour `BreakGlass` session and emits bounded audit/metric evidence without changing ordinary local lockout state.
- Platform Administrators can list, invite, enable, disable, and logically remove external identities. Disablement revokes associated sessions, uses revision checks, requires final-method confirmation, and prevents self-lockout.
- The Operations authentication endpoint exposes sanitized mode/configuration/dependency classifications and aggregate external-identity, linked-active-user, external-login, active-session, and break-glass evidence together. It returns no tenant ID, authority, secret reference, identity claim, account identity, or credential material.
- Every genuine failed Entra callback uses one persisted evidence path. Framework failures before token validation, application rejections, token-exchange failures, and unavailable client authentication each create one sanitized `Authentication.EntraLogin`/`Failed` audit and one trusted `UserLoginFailed` Analytics outbox event. A request-local marker prevents `RemoteFailure` from duplicating evidence already written by the application rejection path.
- PostgreSQL and SQLite are supported by the original `202608040001_EntraHybridAuthenticationV1` migration and correction migration `202608050001_EntraHybridAuthenticationCorrectionsV1`. The correction adds only four dedicated break-glass columns and preserves existing authentication data.

Known limitations: group-to-role mapping, multi-tenant Entra, SCIM, automatic onboarding, certificate client authentication, and authenticated self-linking are deferred. Live tenant validation has not been performed. ConvoLab remains the sole role and workspace authorization authority.

## Configuration evidence

Sanitized examples (the secret value is held by the configured provider, never in JSON):

```json
{ "Authentication": { "Mode": "Local", "Local": { "Enabled": true, "ProductionAllowed": false, "BreakGlassEnabled": false }, "Entra": { "Enabled": false } } }
```

```json
{
  "Authentication": {
    "Mode": "Entra",
    "Local": { "Enabled": false, "BreakGlassEnabled": false },
    "Entra": {
      "Enabled": true,
      "Authority": "https://login.microsoftonline.com/{tenant-guid}/v2.0",
      "TenantId": "{tenant-guid}",
      "ClientId": "{application-guid}",
      "ClientSecretReference": "env:CONVOLAB_ENTRA_CLIENT_SECRET",
      "PublicOrigin": "https://studio.example.invalid",
      "CallbackPath": "/signin-oidc",
      "SignedOutCallbackPath": "/signout-callback-oidc",
      "PostLogoutRedirectUri": "/login",
      "RequireLinkedIdentity": true,
      "AllowInvitationLinking": true,
      "InvitationExpiryHours": 24
    }
  }
}
```

```json
{
  "Authentication": {
    "Mode": "Hybrid",
    "Local": {
      "Enabled": true,
      "HybridAccessAcknowledged": true,
      "BreakGlassEnabled": true,
      "BreakGlassAccountConfigured": true,
      "LoginRateLimitPerMinute": 10,
      "BreakGlass": {
        "MaximumAttempts": 5,
        "LockoutMinutes": 15,
        "RateLimitPerMinute": 3
      }
    },
    "Entra": {
      "Enabled": true,
      "Authority": "https://login.microsoftonline.com/{tenant-guid}/v2.0",
      "TenantId": "{tenant-guid}",
      "ClientId": "{application-guid}",
      "ClientSecretReference": "docker-secret:convolab-entra-client-secret",
      "PublicOrigin": "https://studio.example.invalid"
    }
  }
}
```

Production validation exercised valid configuration and rejection of cross-tenant authorities and plaintext secret configuration. Static validation also covers mode, required IDs, paths, supported secret schemes, local/Hybrid acknowledgements, break-glass acknowledgement/account presence, invitation expiry, HTTPS/proxy posture, and callback host allow-listing.

## Identity evidence

Deterministic linked-user OIDC flow:

| Evidence | Result |
|---|---|
| Provider | Microsoft Entra (`Entra`) |
| Issuer classification | Deterministic tenant-specific stub authority; raw issuer omitted |
| Tenant classification | Matches configured tenant; raw tenant omitted |
| External identity ID | `70000000-0000-0000-0000-000000000201` (test fixture) |
| ConvoLab user ID | `70000000-0000-0000-0000-000000000101` (test fixture) |
| Authentication provider | `Entra` |
| Session reference | Ephemeral database-backed test session; random cookie token stored only as a hash |
| Workspace access | Default workspace association asserted on the created session |
| Audit event | `Authentication.EntraLogin`, `Succeeded` |
| Analytics event | Trusted audit outbox mapping for `UserLoggedIn` |
| Dependency evidence | `StubValidated` |

The deterministic invitation flow asserted one new external identity, consumed invitation state, stable subject persistence, matching-email success, missing-email success, email-mismatch rejection, and no authority from `preferred_username`, `upn`, `email_verified`, or email alone.

## Rejection evidence

| Case | Executed result |
|---|---|
| Unknown identity | Safe external-login denial; no session |
| Wrong tenant | Token/callback rejected; no session |
| Invalid issuer | Framework token validation rejected it; exactly one safe failed-login audit/outbox event persisted |
| Invalid audience | Framework token validation rejected it; exactly one safe failed-login audit/outbox event persisted |
| Expired token | Framework lifetime validation rejected it; exactly one safe failed-login audit/outbox event persisted |
| Invalid state | Framework state/correlation validation rejected it; exactly one safe failed-login audit/outbox event persisted |
| Invalid nonce | Framework nonce validation rejected it; exactly one safe failed-login audit/outbox event persisted |
| Token exchange/client authentication | Safe bounded failure evidence persisted once; no code, token, secret, or reference persisted |
| Disabled identity | Callback rejected it |
| Inactive user | Callback rejected it |
| Open redirect | Absolute, protocol-relative, backslash, control-character, and encoded bypass cases defaulted to `/` |

All external callback failures use `/login?error=authentication.external_login_failed`; response bodies do not disclose subject or account-existence information. Persisted failure detail is restricted to an allow-listed internal classification such as `authentication.entra.remote_failure` or `authentication.entra.client_authentication_unavailable`. Raw exception text, tokens, authorization codes, state, nonce, issuer, subject, email, tenant ID, client-secret material, references, and callback query data are excluded. Existing dependency-state updates and bounded failure metrics remain intact.

## Break-glass evidence

| Evidence | Result |
|---|---|
| Disabled state | Endpoint unavailable unless explicitly enabled in `Entra` or `Hybrid` mode |
| Unauthorised user | Denied unless the credential belongs to an active Platform Administrator |
| Authorised Platform Administrator | Separate route creates a one-hour opaque `BreakGlass` session |
| Audit | Every denial emits `Authentication.BreakGlassFailure`; the threshold transition alone emits `Authentication.BreakGlassLocked`; active-lockout retries remain failures with `lockoutState=Locked`; success emits `Authentication.BreakGlassLogin` |
| Telemetry | `convolab.auth.break_glass.total` with bounded `outcome`, `failure_code`, and `lockout_state` only |
| Session | Provider `BreakGlass`; idle and absolute expiry both one hour |
| Account state | Five attempts, 15-minute break-glass-only lockout, deterministic expiry/reset, optimistic-concurrency retry |
| Endpoint limiter | Dedicated three-per-minute policy; ordinary local-login limiter remains independent |

Production startup additionally verifies that an enabled break-glass path has an active Platform Administrator local credential. No default credential is present.

## Sensitive-data scan

Deterministic sentinels covered ID/access token, authorization code, nonce/state, client secret, invitation token, email, and subject paths. Refresh tokens are not requested or saved. The runtime uses parameter-redacted EF logging and bounded telemetry labels.

| Surface | Result |
|---|---|
| Container logs | PASS — zero token/code/secret/subject/email sentinel matches |
| Generated logs, traces, and browser artifacts | PASS — zero matches outside fixture source; binary screenshots/trace archives were excluded from text search |
| Metrics | PASS — code inspection/tests confirm bounded outcome/failure labels; no identity values |
| Analytics | PASS — trusted event mapping contains event classification and safe IDs only; no claims/tokens/invitation value |
| Audit metadata | PASS — safe action/outcome/resource classification; no raw claims or token values |
| PostgreSQL | PASS for raw token/code/client-secret sentinels; external subject and safe profile fields are intentionally persisted by the approved identity model |
| API errors | PASS — deterministic rejection responses contain only safe error codes |
| Operations Center | PASS — only classifications, booleans, counts, state, and secret-provider scheme are returned |

## Pre-Sign-Off Corrections

The correction implementation and deterministic focused tests cover:

- linking without `email_verified`, valid invitation success without `email`, and non-authority of `preferred_username`/`upn`;
- `authentication.invitation_email_mismatch` with the invitation left active and no identity created;
- exactly one successful concurrent callback, one consumed invitation, one identity, and one committed session;
- database commit before `TicketReceived` cookie issuance, plus forced transaction-commit failure with rollback and no `convolab_session` cookie;
- dedicated break-glass threshold, lockout, correct-password denial while locked, deterministic expiry, successful reset, independent limiter, ordinary-login isolation, concurrency preservation, generic Problem Details, safe audits, and bounded metric labels;
- threshold-only `Authentication.BreakGlassLocked` transition evidence and continued `Authentication.BreakGlassFailure` evidence with `lockoutState=Locked` for attempts during the active lockout;
- correction migration discovery and SQLite preservation coverage, with PostgreSQL preservation coverage included in the disposable-database suite;
- combined sanitized Entra/identity and break-glass Operations contract, database-side aggregate queries, and serial Playwright configuration (`workers: 1`, `retries: 0`).
- one persisted Entra failure-evidence path across framework `RemoteFailure`, unavailable client authentication, and application `RejectAsync`; request-local deduplication proves unknown identity and invitation-email mismatch produce one audit/outbox event rather than two, and Operations counts the same persisted failures.

## Incidental acceptance defects

Acceptance also uncovered two unrelated, tightly scoped defects. The Analytics worker reused an in-memory checkpoint instance incorrectly when one outbox batch contained multiple events for the same workspace/granularity; the correction is limited to restart-safe checkpoint reuse. The Studio shell had incomplete stable-screen/API-connectivity presentation and the Workflow Designer desktop workbench was mis-sized; corrections are limited to global maturity/connectivity indicators and the bounded three-pane/compact-drawer layout. Neither defect expands the Entra/hybrid-authentication tranche or begins a new Analytics or Studio redesign workstream.

Final lifecycle evidence, dependency audits, sentinel scans, and all four serial Playwright positions are recorded below only after execution.

## Live validation declaration

Live Microsoft Entra validation was not executed; identity-provider acceptance remains StubValidated.

## Verification results

| Check | Actual result |
|---|---|
| `dotnet restore` | PASS |
| `dotnet build --configuration Release --no-restore` | PASS — 0 warnings, 0 errors |
| `dotnet test` | PASS — 423/423 (Domain 188, Application 42, Architecture 16, Infrastructure 86, API 91) |
| Deterministic external-authentication correction | PASS — 24/24 focused mock-OIDC tests, including framework failures, application-rejection deduplication, trusted outbox mapping, Operations aggregation, and sensitive sentinels |
| Final combined authentication Operations contract | PASS — sanitized Entra/identity and break-glass evidence returned together; relational `COUNT`/`MAX` aggregation; forbidden configuration, claim, account, and credential fields absent |
| PostgreSQL fresh/upgrade migration | PASS — 9/9 tests against disposable PostgreSQL 16, including correction-upgrade preservation |
| PostgreSQL persistence/restart | PASS — persisted state survived restart; the final application and complete browser suite remained usable after database restart |
| `npm ci` | PASS — reproducible lockfile install |
| `npm run lint` | PASS |
| `npm run test` | PASS — frontend contracts plus interaction audit of 36 TSX files |
| `npm run build` | PASS — TypeScript, Vite production build, and aggregate bundle budgets |
| `npm audit --omit=dev` | PASS — 0 production vulnerabilities |
| `npm audit` | PASS — 0 vulnerabilities after compatible transitive lockfile updates |
| Docker builds | PASS — final API and web images rebuilt successfully |
| Playwright before restart | PASS — 23/23, one worker, zero retries |
| Playwright after API restart | PASS — 23/23, one worker, zero retries |
| Playwright after PostgreSQL restart | PASS — 23/23, one worker, zero retries |
| Playwright against final rebuilt/recreated images | PASS — 23/23, one worker, zero retries |
| Final Operations Playwright correction gate | PASS — 8/8, including combined Entra/identity and break-glass rendering |
| Final complete Playwright correction rerun | PASS — 23/23, one worker, zero retries, against the final healthy acceptance configuration |
| API restart | PASS — container healthy and protected browser flows remained usable |
| PostgreSQL restart | PASS — database/API/web healthy and browser verification completed |
| Final sentinel security scans | PASS — zero matches in API/Studio runtime logs, Playwright artifacts, and relevant persisted authentication/audit/outbox fields, with the intentional external-identity database-field qualification above |
| Operational readiness | BLOCKED — `/health/live` is Healthy, but `/health/ready` is Degraded because the active Development environment requires an external-provider credential and `GEMINI_API_KEY` is empty; Entra is correctly `NotConfigured` in this Local-mode runtime |
| Metadata/root guard | PASS — repository remains `convolab-main`; package/assembly/Studio metadata remains `1.0.0-alpha.14` |

## Sign-off status

Not ready for sign-off.

Concrete blocker: configure the required external-provider credential for the active Development environment, or deliberately select a provider classified as not requiring a secret, then rerun health/readiness. No provider configuration or credential was changed as part of this Entra correction.

No backup/restore, deployment promotion, release supply-chain automation, live channel integration, or alpha.15 release promotion was performed.
