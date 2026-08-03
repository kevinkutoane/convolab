# alpha.15 Operational Foundation (in progress)

This repository remains versioned `1.0.0-alpha.14`. The changes described here are the in-progress alpha.15 Operational Foundation tranche and do not constitute an alpha.15 release.

The tranche adds static Production configuration validation, explicit forwarded-header trust, secure centralized cookies, filesystem/X.509 data protection, asynchronous environment/Docker/Azure Key Vault secret providers, optional OTLP export, sanitized operational health, PostgreSQL-authoritative Analytics worker leases, persisted safe mode, and the administrator-only Operations Center.

## Production configuration boundary

Production values must be supplied externally. Startup rejects SQLite, absent or placeholder PostgreSQL configuration, wildcard hosts, automatic migrations, unsupported authentication modes, unacknowledged local authentication, disabled HTTPS redirection, invalid proxy/OTLP settings, unsafe data protection, or an unspecified `SafeMode__BlockAnalyticsExports` decision.

Local authentication is the only implemented mode. Set `Authentication__Mode=Local` and explicitly acknowledge it with `Authentication__Local__ProductionAllowed=true`. Entra and Hybrid are rejected until the Entra workstream is implemented.

Forwarded headers are accepted only when `Proxy__Enabled=true` and the immediate proxy or network is present in `Proxy__KnownProxies` or `Proxy__KnownNetworks`. Header symmetry is mandatory and `Proxy__ForwardLimit` is bounded from one through five.

Production data-protection keys use `SharedFileSystem`, an absolute writable key-ring directory, and mounted certificate/private-key PEM files. The application discriminator is always `ConvoLab`.

`OTEL_EXPORTER_OTLP_ENDPOINT` enables OTLP trace and metric export. Collector failure does not prevent API startup. For local inspection, run `docker compose --profile telemetry up` and set the API endpoint to `http://otel-collector:4317`.

## Dependency evidence

Operational evidence uses only these states: `NotConfigured`, `Configured`, `StubValidated`, `LiveValidated`, `Unavailable`, and `Degraded`. Stub adapters are never presented as live integrations. Backups remain `NotConfigured`; no RPO, RTO, age, or verification measurement is invented.

Routine Operations Center polling produces structured telemetry and no database audit rows. Safe-mode changes, explicitly opened readiness evidence, live validations, sensitive evidence access, and deliberate administrative actions remain audited.

## Deferred alpha.15 workstreams

Entra/OIDC, external identities, backup and restore, deployment promotion, supply-chain controls, performance evidence, and `FUNCTIONAL_OPERATIONAL_READINESS_V1_REPORT.md` are still required before alpha.15 can be declared complete.
