# ConvoLab

> Design, test, govern, and understand intelligent conversations.

ConvoLab is an enterprise-grade conversational AI platform that provides a complete lifecycle for building, evaluating, governing, and operating production conversational experiences. It pairs a high-performance .NET 8 API backend (Clean Architecture) with a React 19 + Vite frontend (ConvoLab Studio).

**Enterprise Release:** `v1.0.0-enterprise` ([Release Notes](docs/releases/PlatformCore-v1.0.0-enterprise.md))  
**Operational Baselines:** 100% test pass rate across 5 test projects (526 tests passed, 0 failed).

---

## Strategic Enterprise Pillars

ConvoLab delivers tier-1 enterprise conversational AI capabilities across four foundational pillars:

1. **Enterprise Governance, Security & Compliance:**
   - **Automated PII/PHI Redaction Engine:** Real-time inline sanitization masking national IDs, credit cards, emails, phone numbers, and SSNs before LLM prompt dispatch (POPIA, GDPR, HIPAA compliant).
   - **Cryptographic Tamper-Evident Audit Trail:** SHA-256 forward-linked cryptographic hash chain (`AuditHashChain`) sealing all platform mutations and administrative actions.
   - **Pre-Execution Prompt Guardrails:** Pre-flight inspection detecting jailbreaks, prompt injection, and system prompt leakage prior to external provider dispatch.

2. **Omnichannel Enterprise Connectors & Integrations:**
   - **Native Infobip & WhatsApp Business API:** Inbound webhook handling with HMAC-SHA256 signature verification and rich messaging (interactive buttons, quick replies, list messages, media).
   - **Human Handoff & Escalation Protocol:** Intent and sentiment analysis automatically routing frustrated or escalating customers to live contact center queues.
   - **Enterprise RAG 2.0 Hybrid Retrieval:** Reciprocal Rank Fusion (RRF $k=60$) combining 1024-dimensional dense semantic vector similarity and BM25 lexical keyword matching with citation token budgets.

3. **Multi-Environment Lifecycle & Release Management (ALM):**
   - **Multi-Environment Promotion Pipeline:** Automated manifest promotion across environments (`Development` → `Staging` → `Production`).
   - **Source Health Gating:** Prevents promotion unless the source environment passes all active operational readiness and health checks.
   - **Automated Rollback Recovery:** Instant zero-downtime rollback deploying the most recent healthy release manifest.

4. **Executive Observability, ROI & FinOps:**
   - **Executive FinOps Dashboard:** Total Cost of Ownership (TCO in ZAR) and Human Parity ROI against configurable contact center benchmarks (default `R45.00 ZAR` per resolved conversation).
   - **Multi-Dimensional Attribution:** Cost and token breakdown by Provider, Model, and Capability.
   - **Budget Health Projections:** Real-time run-rate tracking with month-end projections and proactive anomaly alerts.
   - **Automated Golden-Dataset Regression CI/CD:** Automated regression quality gate ensuring prompt or model upgrades never degrade production accuracy.

---

## Architecture

ConvoLab follows **Clean Architecture** with four layers enforced by architecture tests:

```
src/
├── Domain/          Pure domain model — entities, value objects, events, specifications, guardrails, PII redaction
├── Application/     Use cases — MediatR commands/queries, FluentValidation, service interfaces, FinOps, ALM
├── Infrastructure/  Adapters — EF Core, Gemini, hybrid RAG retriever, audit hash chain, backup/restore
└── Api/             ASP.NET Core — controllers, webhook receivers, middleware, security, health checks

web/                 ConvoLab Studio — React 19 + Vite + TypeScript + TailwindCSS
```

## Prerequisites

| Tool | Version |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0+ |
| [Node.js](https://nodejs.org/) | 22.22.0+ |
| [Docker](https://www.docker.com/) | 20.10+ |
| [PostgreSQL](https://www.postgresql.org/) | 16 (via Docker) |

## Getting Started

### 1. Clone and configure

```bash
git clone https://github.com/kevinkutoane/convolab.git
cd convolab

# Create your local environment file
cp .env.example .env

# Generate a backup encryption key
openssl rand -base64 32
# → paste the output as BACKUP_ENCRYPTION_KEY in .env
```

### 2. Run with Docker Compose (recommended)

```bash
docker compose up --build
```

This starts three services:

| Service | URL | Description |
|---|---|---|
| **API** | http://localhost:5000 | .NET 8 backend (Swagger at `/swagger`) |
| **Studio** | http://localhost:3000 | React frontend |
| **Database** | localhost:5432 | PostgreSQL 16 |

### 3. Run locally (development)

**Backend:**
```bash
dotnet run --project src/Api/ConvoLab.Api
```

**Frontend:**
```bash
cd web
npm ci
npm run dev
```

The Vite dev server proxies `/api` and `/health` to the API on port 5000.

### 4. Login

Use the bootstrap credentials from your `.env` file:
- **Email:** `admin@convolab.test`
- **Password:** `Ephemeral-Alpha12!`

## Testing

### Backend (.NET)

```bash
# Run all test projects across the entire solution
dotnet test ConvoLab.sln
```

The solution includes 5 test projects (526 tests, 100% pass rate):

| Project | Scope |
|---|---|
| `ConvoLab.Domain.Tests` | Domain logic, PII redaction, prompt guardrails, human handoff |
| `ConvoLab.Application.Tests` | Use cases, golden dataset regression runner, validators |
| `ConvoLab.ArchitectureTests` | Clean Architecture boundary and dependency enforcement |
| `ConvoLab.Infrastructure.IntegrationTests` | EF Core, audit hash chain, hybrid RAG retriever, FinOps |
| `ConvoLab.Api.IntegrationTests` | API contracts, webhooks, ALM promotion, auth, production readiness |

### Frontend (Studio)

```bash
cd web
npm run lint           # ESLint
npm run build          # TypeScript + Vite build + bundle budget
npm test               # Unit tests + interaction audit
npm run test:browser   # Playwright E2E (requires running Docker stack)
```

## Key Capabilities

| Capability | Description |
|---|---|
| **Conversation Simulator** | Design and test conversational flows with multi-turn personas |
| **Omnichannel Connectors** | Native WhatsApp/SMS via Infobip with interactive buttons and human handoff |
| **Knowledge Studio (RAG 2.0)** | Ingest, chunk, and retrieve documents via BM25 + Dense Semantic RRF |
| **Prompt Studio** | Author, version, and govern prompt templates with pre-execution guardrails |
| **Workflow Designer** | Visual workflow and execution state machine orchestration |
| **Intelligence Center** | Multi-provider AI execution with budget controls and model routing |
| **Evaluation Studio** | Automated quality gates with versioned scorecards and Golden Dataset CI/CD |
| **Trace Explorer** | OpenTelemetry-aligned distributed conversation tracing |
| **Replay Studio** | Replay, compare, and regression-test historical conversations |
| **Policy Center** | Runtime policy decisions, PII masking, and prompt injection shields |
| **Plugin Center** | Plugin registry, health probes, and isolated execution |
| **Platform Analytics & FinOps** | Executive TCO & ROI in ZAR, unit economics, and spend attribution |
| **Operations Center (ALM)** | Multi-environment promotion, source health gates, backups, and safe mode |

## Documentation

- [Architecture Overview](docs/project/ARCHITECTURE.md)
- [Enterprise Release Notes](docs/releases/PlatformCore-v1.0.0-enterprise.md)
- [Omnichannel & WhatsApp Connectors](docs/Omnichannel.md)
- [Knowledge Studio & Hybrid RAG](docs/KnowledgeStudio.md)
- [Platform Analytics & Executive FinOps](docs/PlatformAnalytics.md)
- [Evaluation Studio & Regression CI/CD](docs/EvaluationStudio.md)
- [Deployment, ALM & Promotion](docs/operations/Deployment.md)
- [Compliance Controls & AI Governance](docs/security/ComplianceControls.md)
- [Capability Map](docs/CapabilityMap.md)
- [Roadmap](docs/Roadmap.md)
- [ADRs](docs/adr/)

## License

[MIT](LICENSE)
