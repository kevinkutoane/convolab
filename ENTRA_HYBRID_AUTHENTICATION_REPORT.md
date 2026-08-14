# Entra Hybrid Authentication Evidence Report

Workstream: `alpha.15 — Microsoft Entra ID and Hybrid Authentication`  
Repository root: `convolab-main`  
Active release metadata: `1.0.0-alpha.14`  
Evidence date: 2026-08-13

## Implementation summary

- Strongly typed `Local`, `Entra`, and `Hybrid` modes control the public authentication options and login experience.
- ASP.NET Core OpenID Connect uses tenant-specific authorization-code flow, PKCE, framework state/nonce/correlation validation, strict issuer/audience/signature/lifetime validation, secure temporary cookies, and asynchronous client-secret resolution.
- External identities are keyed uniquely by provider, issuer, and subject. Email is mutable profile/invitation evidence and is never the permanent identity key.
- Existing linked identities resolve only to active ConvoLab users. Unknown identities are denied unless a valid, verified-email, tenant-bound, single-use invitation is present. Email equality alone never links a local account.
- Successful OIDC callbacks create the existing opaque, hashed-token ConvoLab session with provider, external identity, session family, idle expiry, and absolute expiry. Entra tokens are not saved.
- Local logout always revokes the ConvoLab session; an Entra session can additionally initiate framework-managed external logout.
- Break glass is off by default, uses a separate endpoint and UI action, is restricted to an active Platform Administrator with a local credential, creates a one-hour `BreakGlass` session, and emits audit/metric evidence.
- Platform Administrators can list, invite, enable, disable, and logically remove external identities. Disablement revokes associated sessions, uses revision checks, requires final-method confirmation, and prevents self-lockout.
- Operations and health expose sanitized mode, counts, break-glass state, secret-provider classification, and truthful `NotConfigured`, `Configured`, `StubValidated`, `LiveValidated`, `Unavailable`, or `Degraded` evidence.
- PostgreSQL and SQLite are supported by the focused `202608040001_EntraHybridAuthenticationV1` migration. Existing sessions are backfilled as `Local`; no identity or secret is synthesized.

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
      "BreakGlassAccountConfigured": true
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

The deterministic invitation flow also asserted one new external identity, consumed invitation state, stable subject persistence, and no email-only linking.

## Rejection evidence

| Case | Executed result |
|---|---|
| Unknown identity | Safe external-login denial; no session |
| Wrong tenant | Token/callback rejected; no session |
| Invalid issuer | Framework token validation rejected it |
| Invalid audience | Framework token validation rejected it |
| Expired token | Framework lifetime validation rejected it |
| Invalid state | Framework state/correlation validation rejected it |
| Invalid nonce | Framework nonce validation rejected it |
| Disabled identity | Callback rejected it |
| Inactive user | Callback rejected it |
| Open redirect | Absolute, protocol-relative, backslash, control-character, and encoded bypass cases defaulted to `/` |

All external callback failures use `/login?error=authentication.external_login_failed`; response bodies do not disclose subject or account-existence information.

## Break-glass evidence

| Evidence | Result |
|---|---|
| Disabled state | Endpoint unavailable unless explicitly enabled in `Entra` or `Hybrid` mode |
| Unauthorised user | Denied unless the credential belongs to an active Platform Administrator |
| Authorised Platform Administrator | Separate route creates a one-hour opaque `BreakGlass` session |
| Audit | `Authentication.BreakGlassLogin` for denied and successful attempts |
| Telemetry | `convolab.auth.break_glass.total` with bounded outcome only |
| Session | Provider `BreakGlass`; idle and absolute expiry both one hour |

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

## Live validation declaration

Live Microsoft Entra validation was not executed; all identity-provider evidence is deterministic StubValidated evidence.

## Verification results

| Check | Actual result |
|---|---|
| `dotnet restore` | PASS |
| `dotnet build --configuration Release --no-restore` | PASS — 0 warnings, 0 errors |
| `dotnet test` | PASS — 389/389 (Domain 188, Application 42, Architecture 16, Infrastructure 84, API 59) |
| Deterministic OIDC matrix | PASS — 11/11 |
| PostgreSQL fresh/upgrade migration | PASS — 8/8 migration tests against PostgreSQL |
| PostgreSQL persistence/restart | PASS — persisted marker survived restart; rebuilt application remained usable after database restart |
| `npm ci` | PASS — 200 packages audited by install |
| `npm run lint` | PASS |
| `npm run test` | PASS — frontend contracts plus interaction audit of 36 TSX files |
| `npm run build` | PASS — TypeScript, Vite production build, and aggregate bundle budgets |
| `npm audit --omit=dev` | PASS — 0 production vulnerabilities |
| `npm audit` | REVIEW REQUIRED — 20 development-tooling findings (4 moderate, 16 high) through `brace-expansion`/ESLint and `nanoid`/PostCSS; npm reports no fix available |
| Docker builds | PASS — final API and web images rebuilt successfully |
| Playwright before restart | PASS — 21/21 |
| Playwright after API restart | PASS with isolated visual retry — functional suite passed; transient visual comparison rerun 2/2 |
| Playwright after PostgreSQL restart, rebuilt images | 20/21 on parallel run due an 8-second Settings readiness timeout; isolated dark/light visual suite immediately passed 2/2. No functional failure reproduced |
| API restart | PASS — container healthy and protected browser flows remained usable |
| PostgreSQL restart | PASS — database/API/web healthy and browser verification completed |
| Sentinel security scans | PASS with the intentional external-identity database-field qualification above |
| Operational readiness | PASS — `/health/live` Healthy and `/health/ready` Healthy in final Local-mode runtime; Entra correctly `NotConfigured` there |
| Metadata/root guard | PASS — repository remains `convolab-main`; package/assembly/Studio metadata remains `1.0.0-alpha.14` |

No backup/restore, deployment promotion, release supply-chain automation, live channel integration, or alpha.15 release promotion was performed.
