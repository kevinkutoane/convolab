# ConvoLab Alpha.19 — Baseline & Operational Evidence Matrix

This matrix establishes the authoritative evidence baseline for ConvoLab during the active Alpha.19 operational-validation milestone. It differentiates between implemented features, automated unit/integration test evidence, local and container operational rehearsal, environment-dependent validation gates, and remaining work.

## Evidence Classification Model

* **🟢 Implemented / Reproducibly Proven**: Code exists, automated test suites execute deterministically, or operational behavior is proven through local/container rehearsal.
* **🟡 Environment-Dependent**: Implementation is complete, but conclusive operational verification requires external infrastructure (e.g. enterprise Azure/Entra tenant). Represented honestly as `Blocked (Environment Gate)` until executed.
* **🔴 Not Justified**: Claims of "Enterprise Validated" or "Production Ready" that cannot be made without live corporate infrastructure or long-term production telemetry.

---

## 1. Operational & Architectural Evidence Matrix

| Area | Implementation | Automated Evidence | Local/Container Evidence | Environment Evidence | Remaining Work |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Clean Architecture Foundation** | Domain, Application, Infrastructure, Api separation; MediatR-style handlers; ports & adapters; zero EF in Application. | `ConvoLab.Domain.Tests` (100% pass), `Verify Application has no EF dependency` CI step. | Verified via `dotnet test ConvoLab.sln`. | N/A | None. Baseline frozen. |
| **Workspace Isolation & Multi-Tenancy** | Multi-tenant schema with `WorkspaceId` enforcement across repositories, controllers, query filters. | `WorkspaceIsolationIntegrationTests.cs`, `CrossTenantSecurityTests.cs`. | Tested in Docker acceptance and integration suites against PostgreSQL. | N/A | None. Baseline frozen. |
| **RBAC & Authorization** | `PlatformAdministrator`, `Member` roles; session policies; role-scoped controllers (`AuditController`, `OperationsController`, `WorkspaceIdentityController`). | `RoleAuthorizationTests.cs`, `MockEntraOidcTests.cs` role assertions. | Tested via Playwright browser suites and integration test client. | N/A | None. Baseline frozen. |
| **Session Lifecycle & Tokens** | Opaque server-side sessions, SHA-256 token hashing, sliding and absolute expirations, logout invalidation. | `AuthenticationIntegrationTests.cs`, `MockEntraOidcTests.cs`. | Tested in container restart persistence verification. | N/A | None. Baseline frozen. |
| **Security Response Headers** | `SecurityHeadersMiddleware`: COOP (`same-origin`), COEP (`require-corp`), XCDO (`nosniff`), extended CSP, 2-year HSTS, Cache-Control. | `SecurityHeadersMiddlewareTests.cs`. | Verified on API and Studio HTTP responses in Docker container. | N/A | None. Baseline frozen. |
| **Data Sanitization & Redaction** | `SensitiveOutputSanitizerMiddleware` (transport layer), `SensitiveTelemetryLogFilter` (Serilog backstop) redacting credentials and tokens. | `SensitiveOutputSanitizerTests.cs`, `SensitiveTelemetryLogFilterTests.cs`. | Log outputs and response bodies verified clean in test harness. | N/A | None. Baseline frozen. |
| **Rate Limiting & DoS Protection** | ASP.NET Core RateLimiter configured on invitation creation, identity mutation, authentication, break-glass. | `RateLimiterTests.cs`, load runner security workload. | Rate limits enforced (HTTP 429) under concurrency testing. | N/A | None. Baseline frozen. |
| **Audit Logging & Compliance** | Dedicated `AuditController` with paginated read, export, PlatformAdmin scoping; immutable audit log records for all mutations. | `AuditTrailIntegrationTests.cs`, `AuditExportTests.cs`. | Export format and permission checks verified in Docker container. | N/A | None. Baseline frozen. |
| **Break-Glass Authentication** | Dedicated break-glass endpoint requiring local admin credentials, strict rate limiting, 15m session, dedicated audit trail. | `BreakGlassAuthenticationTests.cs`. | Verified through local authentication controller tests. | N/A | None. Baseline frozen. |
| **Hybrid Authentication (Entra ID OIDC)** | Tenant-specific OIDC auth-code flow, PKCE, nonce validation, explicit identity linking (provider+issuer+subject), single-use invitations. | `MockEntraOidcTests.cs` (comprehensive mock OIDC provider testing positive & negative cases). | `EntraDependencyEvidence` reports `StubValidated` locally without live tenant. | **Blocked (Environment Gate)**: Corporate Azure tenant and live credentials (`CONVOLAB_ENTRA_*`) required for live token exchange. | Execute against non-prod corporate tenant when credentials are provided. |
| **Backup, Restore & Disaster Recovery** | PostgreSQL custom dump (`pg_dump -Fc`), document archive (`tar`), Data Protection key ring archive, SHA-256 checksums, isolated mode enforcement. | `PostgresBackupRestoreTests.cs`, `BackupServiceIntegrationTests.cs`. | Rehearsed via `docker-compose.recovery.yml` isolated drill harness (`run-recovery-drill.ps1`/`.sh`). | N/A | Recovery drill automated execution and documentation. |
| **Supply Chain & Build Integrity** | Multi-arch Docker builds, immutable image digests (`@sha256:`), CycloneDX SBOMs (API & Studio), Trivy CRITICAL scans, GitHub provenance attestation. | `check-no-reveal.mjs`, `release-build.yml` workflow gates. | Alpha.18 release workflow `34586715291` passed all gates; artifact SHA `23c5232f...` verified. | N/A | Deterministic release artifact verifier (`verify-release-artifacts.mjs`). |
| **Load & Concurrency Performance** | Asynchronous HTTP endpoints, connection pooling, EF Core compiled queries, memory caching. | `tools/load-test/load-runner.mjs` native Node test suite (Read, Write, Execution, Security workloads). | Local and container baseline measurements captured; provisional targets evaluated. | N/A | Endurance/soak test harness and reporting. |
| **Endurance & Stability** | Memory leak prevention, socket reuse, background service resiliency, database connection recovery. | `tools/load-test/soak-runner.mjs` soak runner. | Monitored RSS drift and error rate over sustained duration. | N/A | Scheduled/workflow endurance CI job. |
| **Deployment & Promotion Control Plane** | Operations Center environment promotion plane, pre-migration backup gate, rollback controls, safe mode switches. | `OperationsControllerTests.cs`, Playwright UI browser tests. | Verified in Operations Center Studio page. | N/A | None. Baseline frozen. |

---

## 2. Environment Gate Summary

ConvoLab remains **100% independently demonstrable** in local and container environments. The table below documents capabilities where external enterprise infrastructure is the only missing component:

| Capability | Local / Mock State | Enterprise Environment Dependency | Gate Status |
| :--- | :--- | :--- | :--- |
| **Live Microsoft Entra ID Tenant** | Implemented & verified via `MockEntraOidcTests` (OIDC, PKCE, claim validation, negative cases). | Requires live `Tenant ID`, `Client ID`, `Client Secret`, and corporate redirect URI. | 🟡 **Blocked (Environment Gate)** |
| **Corporate Single Sign-On** | Application sessions, token validation, and account linking proven deterministically. | Requires enterprise Azure AD / Entra ID tenant administration. | 🟡 **Blocked (Environment Gate)** |
| **Live Production Monitoring (Otel / APM)** | OpenTelemetry collector config exists (`tools/otel-collector.yaml`), internal metrics logged. | Requires production Grafana / Azure Monitor / Datadog endpoints. | 🟡 **Blocked (Environment Gate)** |

---

## 3. Operational Integrity Principles

1. **No Enterprise Hostage-Taking**: The platform must never require enterprise credentials to pass local CI or verify internal behavior.
2. **Honest Reporting**: Missing credentials are never converted into a false "pass" or simulated success. They are classified as `Blocked (Environment Gate)`.
3. **Reproducibility**: Any reviewer can reproduce the local and container test results with standard `dotnet`, `node`, and `docker` commands.
