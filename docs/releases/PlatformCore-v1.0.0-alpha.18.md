# ConvoLab Platform and Studio v1.0.0-alpha.18

> **Status: Engineering Complete — Not Yet Formally Released**
> This document describes engineering work present on `main` at the `1.0.0-alpha.18` development version.
> Alpha.18 has **not** been formally tagged, built as a release artifact, or published as a GitHub Release.
> The last formally released milestone is `v1.0.0-alpha.17` (source commit `f8090674651056e90ddb437d12a5d2680038ae34`).
> See [`release-alpha17-evidence.md`](../../release-alpha17-evidence.md) and [`docs/reports/ARTIFACT_VERIFICATION.md`](../reports/ARTIFACT_VERIFICATION.md) for the Alpha.17 release chain.

Alpha 18 delivers **Security & Compliance Hardening v1**, establishing defense-in-depth HTTP security response headers, an immutable platform-wide audit trail API with SafeMode export gates, transport-level problem details sanitization, and expanded production readiness validation rules.

## Delivered

- **Hardened HTTP Security Response Headers:**
  - `SecurityHeadersMiddleware` enforcing `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Cross-Origin-Opener-Policy: same-origin`, `Cross-Origin-Embedder-Policy: require-corp`, `X-Permitted-Cross-Domain-Policies: none`, `Permissions-Policy`, and strict `Content-Security-Policy`.
  - Conditional HSTS (`Strict-Transport-Security: max-age=63072000; includeSubDomains; preload`) enforced strictly in Production environments and suppressed in non-Production environments.

- **Platform Audit Trail & Compliance:**
  - Dedicated `AuditController` (`GET /api/audit/events`, `GET /api/audit/events/{id}`, `GET /api/audit/summary`, `GET /api/audit/export`) requiring `PlatformAdministrator` policy.
  - SafeMode export gate (`SafeMode:BlockAuditExports`) blocking compliance audit dumps when engaged.
  - Zero exposure of raw credentials, tokens, subjects, passwords, authorization codes, or private claims in audit DTO records.

- **Transport Sanitization & Defense-in-Depth:**
  - `SensitiveOutputSanitizerMiddleware` scrubbing sensitive token patterns and credential sentinels from outgoing problem JSON responses.

- **Production Readiness Verification:**
  - Extended `ProductionReadinessValidator` asserting audit export governance and deterministic verification constraints.
