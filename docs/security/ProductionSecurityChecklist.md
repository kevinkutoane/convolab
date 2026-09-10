# Production security checklist

This checklist applies to `v1.0.0-alpha.18` production deployments.

- Supply PostgreSQL credentials externally; reject SQLite, placeholders, and automatic Production migrations.
- Set explicit non-wildcard hosts, keep HTTPS redirection and HSTS enabled, and suppress server headers.
- Enable forwarded headers only with bounded forward limit, header symmetry, and explicit proxy/network trust.
- Keep session and antiforgery cookies `Secure=Always`, `SameSite=Strict`, and `HttpOnly=true`; obtain the request token from the same-origin no-store antiforgery endpoint.
- Use `Authentication:Mode=Local` only with explicit `Authentication:Local:ProductionAllowed=true`; apply every Entra/Hybrid gate below when those modes are selected.
- Use a writable absolute shared data-protection key-ring path and protected mounted X.509 PEM certificate/private-key files. Keep application name `ConvoLab`.
- Restrict Key Vault credentials to workload or managed identity and use exact vault allowlists, bounded timeouts, and retries.
- Set `SafeMode:BlockAnalyticsExports` explicitly (now defaults to `true` in Production). Review the deliberate decision in Operations Center.
- Set `SafeMode:BlockAuditExports` explicitly (defaults to `false` in Production — enable if the audit export endpoint must be restricted).
- Keep OTLP headers and credentials external. Treat exporter reachability as operational evidence, not proof of durable delivery.
- Confirm anonymous health output remains minimal and all Operations APIs require Platform Administrator.
- Confirm `Serilog:MinimumLevel` is `Warning` or higher in Production; validator enforces this — `Information`/`Debug`/`Verbose` are rejected.
- Run sentinel leakage scans across logs, traces, and metrics before promotion; `SensitiveOutputSanitizerMiddleware` and `SensitiveTelemetryLogFilter` provide runtime enforcement and a backstop.
- Confirm security response headers are present on all responses: COOP, COEP, X-Permitted-Cross-Domain-Policies, CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, Permissions-Policy, HSTS (2 years).
- Review `docs/security/ThreatModel.md` before promotion and confirm all `Implemented` controls are verified in the target environment.
- Review `docs/security/ComplianceControls.md` and confirm all `Implemented` items are operationally active.
- Do not claim live Entra validation, backup/restore, deployment promotion, supply-chain artifacts, or final release completion in this tranche without executed evidence.

## Entra and hybrid authentication

- [ ] Mode is exactly Local, Entra, or Hybrid and ordinary local access is explicitly approved.
- [ ] Entra uses a specific tenant v2 authority; `common` and `organizations` are absent.
- [ ] Tenant, client, HTTPS public origin, callback paths, trusted proxy boundary, and AllowedHosts agree.
- [ ] Client authentication uses an `env:`, `docker-secret:`, or `azure-key-vault:` reference; no plaintext secret is present.
- [ ] Unknown identities are rejected and linking requires an expected-tenant, single-use invitation; usable `email` must match, while `preferred_username`, `upn`, and `email_verified` provide no authority.
- [ ] OIDC state, nonce, correlation, issuer, audience, signature, and lifetime validation remain enabled.
- [ ] Application sessions store only a token hash, provider, and external identity reference; raw provider tokens are absent.
- [ ] Break glass is disabled or has an active authorised Platform Administrator, vault ownership, alerting, and a completed runbook exercise.
- [ ] Dedicated break-glass attempts, lockout, rate limit, concurrency, generic denial, reset, and ordinary-login isolation have been verified.
- [ ] Operations authentication evidence renders sanitized Entra/identity classifications and aggregate counts together with break-glass state, availability, recent uses/failures, and last success.
- [ ] Operations evidence distinguishes `StubValidated` from `LiveValidated` and exposes no authority, subject, email, token, or secret reference.
