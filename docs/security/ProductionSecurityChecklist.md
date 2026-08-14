# Production security checklist

This checklist applies to `alpha.15 — Microsoft Entra ID, External Identities & Hybrid Authentication` while active release metadata remains `1.0.0-alpha.14`.

- Supply PostgreSQL credentials externally; reject SQLite, placeholders, and automatic Production migrations.
- Set explicit non-wildcard hosts, keep HTTPS redirection and HSTS enabled, and suppress server headers.
- Enable forwarded headers only with bounded forward limit, header symmetry, and explicit proxy/network trust.
- Keep session and antiforgery cookies `Secure=Always`, `SameSite=Strict`, and `HttpOnly=true`; obtain the request token from the same-origin no-store antiforgery endpoint.
- Use `Authentication:Mode=Local` only with explicit `Authentication:Local:ProductionAllowed=true`; apply every Entra/Hybrid gate below when those modes are selected.
- Use a writable absolute shared data-protection key-ring path and protected mounted X.509 PEM certificate/private-key files. Keep application name `ConvoLab`.
- Restrict Key Vault credentials to workload or managed identity and use exact vault allowlists, bounded timeouts, and retries.
- Set `SafeMode:BlockAnalyticsExports` explicitly and review the deliberate decision in Operations Center.
- Keep OTLP headers and credentials external. Treat exporter reachability as operational evidence, not proof of durable delivery.
- Confirm anonymous health output remains minimal and all Operations APIs require Platform Administrator.
- Run sentinel leakage scans across logs, traces, and metrics before promotion.
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
- [ ] Operations authentication evidence exposes only aggregate break-glass state, availability, last success, and recent failure count.
- [ ] Operations evidence distinguishes `StubValidated` from `LiveValidated` and exposes no authority, subject, email, token, or secret reference.
