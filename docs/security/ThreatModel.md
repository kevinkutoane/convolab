# ConvoLab Alpha.18 — Threat Model

Version: `1.0.0-alpha.18`. Scope: runtime threats against the running platform and CI/supply-chain.
This document maps each identified threat to the existing or new control and its validation status.

---

## Methodology

Each threat is evaluated against the STRIDE model:
**S**poofing | **T**ampering | **R**epudiation | **I**nformation Disclosure | **D**oS | **E**levation of Privilege

Validation status:
- **Implemented** — control exists in code; integration tests cover it.
- **StubValidated** — control exists; acceptance requires a live environment or live credential.
- **Documented** — control is specified in documentation; code enforcement is in progress.

---

## 1. Authentication Bypass

| STRIDE | T, S |
|---|---|
| **Threat** | An attacker obtains or forges a ConvoLab session cookie without completing authentication |
| **Controls** | Opaque session cookies (`HttpOnly`, `Secure=Always`, `SameSite=Strict`); session record stored as SHA-256 hash only; no raw token persisted; break-glass isolated from ordinary login; `ProductionReadinessValidator` enforces Entra-only mode disables local login |
| **Test coverage** | `AuthenticationRegressionTests`, `BreakGlassAuthenticationTests`, `MockEntraOidcTests` |
| **Status** | Implemented |

## 2. Session Fixation

| STRIDE | S |
|---|---|
| **Threat** | An attacker pre-sets a known session identifier that a victim then authenticates against |
| **Controls** | Session IDs are Guid.NewGuid() generated server-side at login; no client-supplied session identifier is honoured; antiforgery token required for all state-changing requests |
| **Status** | Implemented |

## 3. CSRF (Cross-Site Request Forgery)

| STRIDE | T, E |
|---|---|
| **Threat** | A malicious page causes an authenticated browser to make an unintended mutating request |
| **Controls** | `CookieAntiforgeryMiddleware` enforces `X-XSRF-TOKEN` header on all mutating requests; antiforgery cookie is `HttpOnly`, `Secure=Always`, `SameSite=Strict`; CSP `default-src 'self'` prevents cross-origin script loading |
| **Test coverage** | `AuthenticationRegressionTests`, `ApiContractTests` |
| **Status** | Implemented |

## 4. Clickjacking

| STRIDE | T |
|---|---|
| **Threat** | A malicious page renders the ConvoLab Studio in an iframe to trick users into unintended actions |
| **Controls** | `X-Frame-Options: DENY`; `Content-Security-Policy: frame-ancestors 'none'`; `Cross-Origin-Opener-Policy: same-origin` (Alpha.18 addition) |
| **Status** | Implemented |

## 5. OIDC Token Replay

| STRIDE | S, E |
|---|---|
| **Threat** | An attacker captures a valid OIDC ID/access token and replays it to obtain a ConvoLab session |
| **Controls** | PKCE enforced; `SaveTokens=false` (no raw token persisted); application session uses opaque token hash only; nonce and state validated by OIDC middleware; `RequireHttpsMetadata=true`; token lifetime and signature validated (`ValidateLifetime=true`, `RequireSignedTokens=true`) |
| **Test coverage** | `MockEntraOidcTests` |
| **Status** | Implemented |

## 6. Data Exfiltration via Analytics Exports

| STRIDE | I |
|---|---|
| **Threat** | A privileged user or misconfiguration causes sensitive platform data to be exported via the Analytics export endpoint |
| **Controls** | `SafeMode:BlockAnalyticsExports` flag — now explicitly set to `true` in `appsettings.Production.json` (Alpha.18); `ProductionReadinessValidator` enforces an explicit decision; `AuditController.Export` separately controlled by `SafeMode:BlockAuditExports` |
| **Status** | Implemented |

## 7. Audit Log Tampering / Repudiation

| STRIDE | T, R |
|---|---|
| **Threat** | An actor performs a high-privilege action and then removes or modifies the audit record |
| **Controls** | `ApplicationDbContext` rejects `Modified` or `Deleted` EF states on `AuditEventRecord` (immutable audit entries); only `PlatformAdministrator` can read audit records; no delete endpoint exists; entries are also enqueued into the Analytics outbox for durable external storage |
| **Status** | Implemented |

## 8. Sensitive Data in Logs / Error Responses

| STRIDE | I |
|---|---|
| **Threat** | JWTs, email addresses, authorization codes, or client secrets appear in log output or HTTP error bodies, leading to credential exposure via SIEM/log-aggregation pipelines |
| **Controls** | `SensitiveOutputSanitizerMiddleware` (Alpha.18) intercepts `application/problem+json` responses and redacts JWT and sensitive key-value patterns; `SensitiveTelemetryLogFilter` (Alpha.18) suppresses Serilog events containing JWT-shaped values or sensitive property names; `SensitiveTelemetryHttpRequestOptions` filters HTTP client traces; `ExternalIdentitySecurity.md` prohibits inclusion at call sites |
| **Status** | Implemented |

## 9. Invitation Abuse / Token Enumeration

| STRIDE | E |
|---|---|
| **Threat** | An attacker brute-forces invitation tokens or floods invitation creation to enumerate valid user accounts |
| **Controls** | Invitation tokens are cryptographically random (256-bit via `RandomNumberGenerator`); stored only as SHA-256 hash; single-use and expiring; `invitations` rate-limit policy (Alpha.18): 5 requests/IP/minute; `identity-mutations` rate-limit policy (Alpha.18): 10 requests/IP/minute |
| **Status** | Implemented |

## 10. Workspace Member Privilege Escalation

| STRIDE | E |
|---|---|
| **Threat** | A workspace member modifies their own role to gain Administrator privileges |
| **Controls** | `WorkspacePermissions.ManageMembers` policy required; self-demotion of the final Administrator is rejected; `member-mutations` rate-limit policy (Alpha.18): 20 requests/IP/minute; all member mutations produce audit events |
| **Status** | Implemented |

## 11. Supply-Chain / Container Integrity

| STRIDE | T |
|---|---|
| **Threat** | A malicious actor substitutes a tampered container image in the deployment pipeline |
| **Controls** | Immutable GHCR image tags with SHA digest pinning (Alpha.17); dual CycloneDX SBOMs (Alpha.17); cryptographic build provenance attestations (Alpha.17); Trivy vulnerability scanning gates (Alpha.17) |
| **Status** | StubValidated (live CI artifact evidence pending authoritative freeze) |

## 12. Information Disclosure via Error Detail

| STRIDE | I |
|---|---|
| **Threat** | Unhandled exceptions return stack traces, internal paths, or connection strings to clients |
| **Controls** | `GlobalExceptionMiddleware` maps all exceptions to structured `ProblemDetails` with generic messages; only `correlationId` and `code` are returned; stack traces never serialised; `SensitiveOutputSanitizerMiddleware` (Alpha.18) provides a final layer of defence |
| **Status** | Implemented |

## 13. Spectre / Cross-Origin Side-Channel

| STRIDE | I |
|---|---|
| **Threat** | A same-process attacker in a shared browser context reads ConvoLab memory via Spectre-class timing attacks |
| **Controls** | `Cross-Origin-Opener-Policy: same-origin` (Alpha.18) isolates the browsing context; `Cross-Origin-Embedder-Policy: require-corp` (Alpha.18) prevents cross-origin sub-resource loading without explicit opt-in |
| **Status** | Implemented |

---

## Deferred / Out-of-Scope for Alpha.18

- Multi-tenant Entra (SCIM, automatic onboarding) — deferred to Phase 5.
- Live Entra organisational validation — environment gate (StubValidated).
- Load/endurance testing and formal DDoS envelope — deferred (Alpha.18 baseline closure item).
- SOC 2 / ISO 27001 formal audit — Post-GA.
