# ConvoLab Compliance Controls

Version: `1.0.0-alpha.18`. Framework: SOC 2 Trust-Service Categories (general-purpose hardening pass).
This document is a foundation for future ISO 27001 / SOC 2 formal control mapping. It is not a
certification claim.

---

## Trust-Service Category Mapping

### CC1 — Control Environment

| Control ID | Description | Implementation | Status |
|---|---|---|---|
| CC1.1 | Security commitments documented | `ExternalIdentitySecurity.md`, `ProductionSecurityChecklist.md`, `ThreatModel.md` | Implemented |
| CC1.2 | Security policies enforced at startup | `ProductionReadinessValidator.ValidateStaticOrThrow` — 25+ rules, throws on violation | Implemented |
| CC1.3 | Roles and responsibilities | `WorkspacePermissions`, `PlatformAdministrator` policy, RBAC via claim-based authorization | Implemented |

---

### CC2 — Communication and Information

| Control ID | Description | Implementation | Status |
|---|---|---|---|
| CC2.1 | Operational status visible to administrators | Operations Center (`/api/operations/status`), health endpoints (`/health/live`, `/health/ready`) | Implemented |
| CC2.2 | Sensitive output prohibited from error responses | `SensitiveOutputSanitizerMiddleware` scrubs problem+json; `GlobalExceptionMiddleware` returns generic messages | Implemented |
| CC2.3 | Sensitive data prohibited from log output | `SensitiveTelemetryLogFilter` filters Serilog events; `SensitiveTelemetryHttpRequestOptions` filters OTEL traces | Implemented |

---

### CC3 — Risk Assessment

| Control ID | Description | Implementation | Status |
|---|---|---|---|
| CC3.1 | Threat model maintained | `docs/security/ThreatModel.md` — 13 threats, STRIDE-mapped, linked to controls | Implemented |
| CC3.2 | Risk of sensitive data exposure assessed | `ExternalIdentitySecurity.md`, threat model items 8, 12 | Implemented |
| CC3.3 | Supply-chain risk assessed | Dual CycloneDX SBOMs, Trivy gates, cryptographic provenance | StubValidated |

---

### CC4 — Monitoring

| Control ID | Description | Implementation | Status |
|---|---|---|---|
| CC4.1 | Security-relevant events logged | `GovernedActivityAuditMiddleware` — asset lifecycle, members, identities, environments, settings | Implemented |
| CC4.2 | Audit trail tamper-proof | `ApplicationDbContext` rejects Modified/Deleted states on `AuditEventRecord` | Implemented |
| CC4.3 | Audit trail queryable by administrators | `AuditController` — paginated, filtered, PlatformAdministrator-scoped | Implemented |
| CC4.4 | Anomalous access signals logged | Login failures, break-glass attempts, rate-limit rejections (429 responses) | Implemented |

---

### CC5 — Control Activities

| Control ID | Description | Implementation | Status |
|---|---|---|---|
| CC5.1 | Least-privilege access | Claim-based `WorkspacePermissions`, capability-level middleware, `PlatformAdministrator` isolation | Implemented |
| CC5.2 | Data protection keys protected at rest | X.509 PEM-encrypted key ring; `PrivateKeyPemPath` permissions validated (no group/other read) | Implemented |
| CC5.3 | Client secrets never in plaintext config | `IsSupportedSecretReference` enforces `env:`, `docker-secret:`, `azure-key-vault:` references only | Implemented |
| CC5.4 | Antiforgery token enforced on mutations | `CookieAntiforgeryMiddleware`; antiforgery cookie `HttpOnly`, `Secure=Always`, `SameSite=Strict` | Implemented |
| CC5.5 | Session cookies hardened | Main session: `HttpOnly`, `Secure=Always`, `SameSite=Strict`; no raw token persisted | Implemented |

---

### CC6 — Logical Access Controls

| Control ID | Description | Implementation | Status |
|---|---|---|---|
| CC6.1 | Authentication required for all API endpoints | Fallback policy: `RequireAuthenticatedUser` on all routes | Implemented |
| CC6.2 | Multi-factor / Entra-mode available | Entra OIDC with PKCE; hybrid mode; invitation linking | StubValidated |
| CC6.3 | Account lockout on break-glass | Configurable lockout (1–1440 min); rate-limit (1–60/min); separate endpoint isolation | Implemented |
| CC6.4 | Identity lifecycle audit | Enable/disable/link/unlink audit events via `GovernedActivityAuditMiddleware` (Alpha.18) | Implemented |
| CC6.5 | Workspace member access audit | Invite/role-change/revoke audit events via `GovernedActivityAuditMiddleware` (Alpha.18) | Implemented |
| CC6.6 | High-risk operations rate-limited | Invitations: 5/IP/min; identity mutations: 10/IP/min; member mutations: 20/IP/min | Implemented |

---

### CC7 — System Operations

| Control ID | Description | Implementation | Status |
|---|---|---|---|
| CC7.1 | Startup validation enforces secure configuration | `ProductionReadinessValidator` — 25+ rules covering DB, auth, proxy, data-protection, SafeMode, telemetry | Implemented |
| CC7.2 | Health checks expose operational state | `/health/live`, `/health/startup`, `/health/ready`; anonymous endpoints return minimal output | Implemented |
| CC7.3 | Log verbosity controlled in Production | Validator rejects `Verbose`/`Debug`/`Information` levels in Production | Implemented |
| CC7.4 | Analytics exports blocked in Production by default | `SafeMode:BlockAnalyticsExports: true` in `appsettings.Production.json`; validator enforces explicit decision | Implemented |

---

### CC8 — Change Management

| Control ID | Description | Implementation | Status |
|---|---|---|---|
| CC8.1 | Settings mutations audited | `GovernedActivityAuditMiddleware` covers `/api/settings/*` (Alpha.18) | Implemented |
| CC8.2 | Environment promotions audited | Audited Environment Promotion control plane (Alpha.17); `GovernedActivityAuditMiddleware` covers `/api/environments/*` | Implemented |
| CC8.3 | Release pipeline integrity | Immutable GHCR tags, dual SBOMs, cryptographic provenance (Alpha.17) | StubValidated |

---

### CC9 — Risk Mitigation

| Control ID | Description | Implementation | Status |
|---|---|---|---|
| CC9.1 | Backup and recovery defined | DR runbook, PostgreSQL PITR, Operations Center RPO/RTO telemetry (Alpha.16) | Implemented |
| CC9.2 | Supply-chain vulnerabilities gated | Trivy CVE scanning in CI with enforcement gates (Alpha.17) | StubValidated |

---

## Open Items (not baseline-closure blockers)

- Live Entra tenant validation (`StubValidated` → `LiveValidated`) — environment gate.
- Load/endurance test evidence — pending.
- SOC 2 formal readiness review — post-GA.
- Multi-tenant Entra / SCIM — deferred to Phase 5.
