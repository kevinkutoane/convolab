# Production security checklist

This checklist applies to `alpha.15 Operational Foundation — Final Sign-Off` while active release metadata remains `1.0.0-alpha.14`.

- Supply PostgreSQL credentials externally; reject SQLite, placeholders, and automatic Production migrations.
- Set explicit non-wildcard hosts, keep HTTPS redirection and HSTS enabled, and suppress server headers.
- Enable forwarded headers only with bounded forward limit, header symmetry, and explicit proxy/network trust.
- Keep session and antiforgery cookies `Secure=Always`, `SameSite=Strict`, and `HttpOnly=true`; obtain the request token from the same-origin no-store antiforgery endpoint.
- Use `Authentication:Mode=Local` only with explicit `Authentication:Local:ProductionAllowed=true`. Entra/Hybrid remains rejected and deferred.
- Use a writable absolute shared data-protection key-ring path and protected mounted X.509 PEM certificate/private-key files. Keep application name `ConvoLab`.
- Restrict Key Vault credentials to workload or managed identity and use exact vault allowlists, bounded timeouts, and retries.
- Set `SafeMode:BlockAnalyticsExports` explicitly and review the deliberate decision in Operations Center.
- Keep OTLP headers and credentials external. Treat exporter reachability as operational evidence, not proof of durable delivery.
- Confirm anonymous health output remains minimal and all Operations APIs require Platform Administrator.
- Run sentinel leakage scans across logs, traces, and metrics before promotion.
- Do not claim Entra, backup/restore, deployment promotion, supply-chain artifacts, or final readiness completion in this tranche.
