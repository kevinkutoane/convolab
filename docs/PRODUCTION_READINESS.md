# Production Readiness Brief: ConvoLab Conversational AI Platform

## 1. Technical Viability
- **Clean Architecture Foundation**: Domain logic is strictly isolated from infrastructure and web frameworks ([`src/Domain/ConvoLab.Domain`](../src/Domain/ConvoLab.Domain), [`src/Application/ConvoLab.Application`](../src/Application/ConvoLab.Application)). Boundary compliance is continuously enforced by architecture fitness functions ([`src/tests/ConvoLab.ArchitectureTests/ArchitectureFitnessTests.cs`](../src/tests/ConvoLab.ArchitectureTests/ArchitectureFitnessTests.cs)).
- **Dual-Stack Modern Topology**: High-throughput ASP.NET Core 8 Web API paired with a React 19 + Vite frontend reverse-proxied via Nginx ([`web/nginx.conf`](../web/nginx.conf)).
- **Comprehensive Test Suite**: 553 automated tests passing with 0 failures across 5 test projects (Domain: 226, Application: 53, Architecture: 16, Infrastructure: 124, API: 134) verified in [`ConvoLab.sln`](../ConvoLab.sln).
- **Bundle Governance**: Studio client bundles adhere to aggregate gzip budget thresholds (<450 KiB initial JS) enforced during build ([`web/scripts/check-bundle-budget.mjs`](../web/scripts/check-bundle-budget.mjs)).

## 2. Security & Privacy
- **Static Startup Security Gate**: `ProductionReadinessValidator` executes on launch, failing closed if placeholder passwords, default connection strings, unmanaged migrations, or insecure hosts are detected in production ([`src/Api/ConvoLab.Api/Operations/ProductionReadinessValidator.cs`](../src/Api/ConvoLab.Api/Operations/ProductionReadinessValidator.cs)).
- **Zero-Secret Storage Model**: Raw credentials and API keys are never stored in the database or transmitted to client browsers; they are referenced through `env:` or vault pointers resolved in memory ([`src/Infrastructure/ConvoLab.Infrastructure/Settings/OperationalSecretStore.cs`](../src/Infrastructure/ConvoLab.Infrastructure/Settings/OperationalSecretStore.cs)).
- **Defense-in-Depth HTTP Headers**: Enforces strict CSP, `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, and conditional 2-year HSTS with preloading for production ([`src/Api/ConvoLab.Api/Middleware/SecurityHeadersMiddleware.cs`](../src/Api/ConvoLab.Api/Middleware/SecurityHeadersMiddleware.cs)).
- **Cryptographic Audit Trail**: Platform events and configuration changes are recorded into an append-only, tamper-evident hash chain using SHA-256 ([`src/Infrastructure/ConvoLab.Infrastructure/Audit/EfAuditTrailRepository.cs`](../src/Infrastructure/ConvoLab.Infrastructure/Audit/EfAuditTrailRepository.cs)).
- **Granular Rate Limiting**: Endpoint-specific fixed-window rate limiters protect authentication, break-glass logins, and member mutations against brute force ([`src/Api/ConvoLab.Api/Program.cs`](../src/Api/ConvoLab.Api/Program.cs#L65-L94)).

## 3. Data Flow and Privacy
- **Egress Boundary**: External network calls are strictly restricted to:
  1. Infobip REST API (`https://api.infobip.com`) for WhatsApp outbound delivery ([`src/Infrastructure/ConvoLab.Infrastructure/Omnichannel/InfobipOutboundAdapter.cs`](../src/Infrastructure/ConvoLab.Infrastructure/Omnichannel/InfobipOutboundAdapter.cs)).
  2. Google Gemini API (`https://generativelanguage.googleapis.com`) for LLM inference ([`src/Infrastructure/ConvoLab.Infrastructure/Intelligence/GeminiIntelligenceExecutor.cs`](../src/Infrastructure/ConvoLab.Infrastructure/Intelligence/GeminiIntelligenceExecutor.cs)).
- **PII Redaction Pipeline**: Customer messages pass through `PiiDataSanitizer` before reaching model providers or trace storage ([`src/Application/ConvoLab.Application/Governance/PiiDataSanitizer.cs`](../src/Application/ConvoLab.Application/Governance/PiiDataSanitizer.cs)).
  - Pattern coverage: South African National ID numbers (13 digits with checksum), email addresses, international and local phone numbers, and payment card numbers.
  - Sensitive prompt tokens are substituted with deterministic masking identifiers (`[ID_NUMBER_1]`, `[EMAIL_1]`) preserving conversational coherence without exposing personal data.

## 4. Deployment Path & ALM
- **Containerized Workload**: Standard multi-stage Docker builds produce minimal runtime images for both API and Studio ([`Dockerfile`](../src/Api/ConvoLab.Api/Dockerfile), [`web/Dockerfile`](../web/Dockerfile), [`docker-compose.yml`](../docker-compose.yml)).
- **CI/CD Supply Chain Security**: Workflows generate dual CycloneDX SBOMs, sign builds via cryptographic provenance attestations, and run Trivy vulnerability scanners ([`.github/workflows/ci.yml`](../.github/workflows/ci.yml)).
- **Environment Promotion & Rehearsed Rollback**: Promotion pipelines enforce pre-migration backups before deploying to Production ([`src/Infrastructure/ConvoLab.Infrastructure/Operations/DeploymentService.cs`](../src/Infrastructure/ConvoLab.Infrastructure/Operations/DeploymentService.cs)), with rehearsed and verified container rollbacks executing in 15–24 seconds ([`tools/release/rehearse-rollback.sh`](../tools/release/rehearse-rollback.sh)).

## 5. Operational Support & Disaster Recovery
- **Health Probes**: Liveness (`/health/live`) and deep readiness (`/health/ready`) checks evaluate database connectivity, secret resolution, and storage availability ([`src/Api/ConvoLab.Api/Health/`](../src/Api/ConvoLab.Api/Health/)).
- **Encrypted Backups**: Scheduled and on-demand database snapshots are authenticated and encrypted using chunked AES-256-GCM with SHA-256 integrity validation ([`src/Infrastructure/ConvoLab.Infrastructure/Operations/Backups/EncryptedDatabaseBackupService.cs`](../src/Infrastructure/ConvoLab.Infrastructure/Operations/Backups/EncryptedDatabaseBackupService.cs)).
- **Observability**: Serilog structured JSON logging with sensitive token filters ([`SensitiveTelemetryLogFilter.cs`](../src/Api/ConvoLab.Api/Telemetry/SensitiveTelemetryLogFilter.cs)) and OpenTelemetry metrics and traces exportable via OTLP ([`tools/otel-collector.yaml`](../tools/otel-collector.yaml)).

## 6. Maintainability
- **Database Evolution**: Entity Framework Core with code-first migrations for PostgreSQL ([`src/Infrastructure/ConvoLab.Infrastructure/Data/ConvoLabDbContext.cs`](../src/Infrastructure/ConvoLab.Infrastructure/Data/ConvoLabDbContext.cs)).
- **RFC 7807 Error Contracts**: Standardized Problem Details responses prevent internal stack traces or database schema details from leaking to clients ([`src/Api/ConvoLab.Api/Middleware/ProblemDetailsMiddleware.cs`](../src/Api/ConvoLab.Api/Middleware/ProblemDetailsMiddleware.cs)).
- **Extensible Abstractions**: Provider ports (`IIntelligenceExecutor`, `IOmnichannelService`, `ISecretStore`) permit swapping backing services without modifying application logic.

## 7. Known Gaps (Transparent Architecture Review)
1. **Hash-Based Vector Retriever**: The Knowledge retriever utilizes 1024-dimensional FNV-1a hash clustering combined with BM25 via Reciprocal Rank Fusion ([`src/Infrastructure/ConvoLab.Infrastructure/Knowledge/HybridKnowledgeRetriever.cs`](../src/Infrastructure/ConvoLab.Infrastructure/Knowledge/HybridKnowledgeRetriever.cs)). Upgrading to a learned dense embedding model (e.g., `text-embedding-004`) is a planned enhancement.
2. **Single Active LLM Provider**: Currently, only Google Gemini has an operational executor implementation; Azure OpenAI / Copilot compatibility is architected behind `IIntelligenceExecutor` but requires adapter implementation.
3. **Microsoft Entra ID Environment Gate**: Hybrid Entra authentication and external identity invite linking are implemented and tested via test doubles, but live corporate tenant validation remains an environment gate pending external tenant provisioning ([`docs/operations/EntraLiveAcceptanceProtocol.md`](../docs/operations/EntraLiveAcceptanceProtocol.md)).
4. **Plugin Runtime Isolation**: The plugin center currently executes within the host process via reflection rather than within an out-of-process WebAssembly or gRPC security sandbox ([`src/Infrastructure/ConvoLab.Infrastructure/Plugins/PluginService.cs`](../src/Infrastructure/ConvoLab.Infrastructure/Plugins/PluginService.cs)).
