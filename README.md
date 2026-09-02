# ConvoLab

> Design, test, govern, and understand intelligent conversations.

ConvoLab is a conversational AI platform that provides a complete lifecycle for building, evaluating, and operating production-grade conversational experiences. It pairs a .NET 8 API backend with a React + Vite frontend (ConvoLab Studio).

**Current release:** `v1.0.0-alpha.17`

---

## Architecture

ConvoLab follows **Clean Architecture** with four layers enforced by architecture tests:

```
src/
├── Domain/          Pure domain model — entities, value objects, events, specifications
├── Application/     Use cases — MediatR commands/queries, FluentValidation, service interfaces
├── Infrastructure/  Adapters — EF Core, Gemini, file storage, secrets, backup/restore
└── Api/             ASP.NET Core — controllers, middleware, security, health checks

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
# Run all test projects
dotnet test ConvoLab.sln
```

The solution includes 5 test projects:

| Project | Scope |
|---|---|
| `ConvoLab.Domain.Tests` | Domain logic, entities, value objects |
| `ConvoLab.Application.Tests` | Use cases, services, validators |
| `ConvoLab.ArchitectureTests` | Clean Architecture boundary enforcement |
| `ConvoLab.Api.IntegrationTests` | API contracts, auth, production readiness |
| `ConvoLab.Infrastructure.IntegrationTests` | EF Core, repositories, external adapters |

### Frontend (Studio)

```bash
cd web
npm run lint           # ESLint
npm run build          # TypeScript + Vite build + bundle budget
npm test               # Unit tests + interaction audit
npm run test:browser   # Playwright E2E (requires running Docker stack)
```

## CI/CD

The GitHub Actions pipeline runs on every push/PR to `main`:

1. **Repository hygiene** — rejects tracked build output, committed secrets, duplicate migrations
2. **Platform Core** — builds .NET, runs all 5 test projects against PostgreSQL
3. **ConvoLab Studio** — lint, build, bundle budget, baseline verification, `npm audit`
4. **Docker acceptance** — full stack compose, cross-capability smoke tests, Playwright E2E, restart persistence

Release builds generate CycloneDX SBOMs, run Trivy container scans, and publish attested images to GHCR.

## Deployment

```bash
# UAT — requires immutable image digests
docker compose -f deploy/uat/docker-compose.yml up -d

# Production — requires immutable image digests + pre-migration backup gate
docker compose -f deploy/production/docker-compose.yml up -d

# Disaster recovery drill (isolated environment)
docker compose -f docker-compose.recovery.yml up -d
```

See [docs/project/DEPLOYMENT.md](docs/project/DEPLOYMENT.md) for the full deployment promotion workflow.

## Project Structure

```
convolab/
├── src/                    .NET backend (Clean Architecture)
│   ├── Api/                ASP.NET Core API host
│   ├── Application/        Use cases and service interfaces
│   ├── Domain/             Domain model
│   ├── Infrastructure/     Adapters and persistence
│   └── tests/              5 test projects
├── web/                    ConvoLab Studio (React + Vite)
├── deploy/                 UAT and Production compose files
├── docs/                   Architecture, ADRs, capabilities, reports
├── tools/                  OpenTelemetry collector config, SQL scripts
├── scripts/                CI validation scripts
└── .github/workflows/      CI, release build, release promotion
```

## Key Capabilities

| Capability | Description |
|---|---|
| **Conversation Simulator** | Design and test conversational flows |
| **Knowledge Studio** | Ingest, chunk, and retrieve knowledge documents |
| **Prompt Studio** | Author, version, and govern prompt templates |
| **Workflow Designer** | Visual workflow and execution orchestration |
| **Intelligence Center** | Multi-provider AI execution with budget controls |
| **Evaluation Studio** | Automated quality evaluation with persisted scorecards |
| **Trace Explorer** | OpenTelemetry-aligned conversation tracing |
| **Replay Studio** | Replay and compare historical conversations |
| **Policy Center** | Runtime policy decisions and governance rules |
| **Plugin Center** | Plugin registry, health probes, and versioning |
| **Platform Analytics** | Trusted runtime attribution and cost tracking |
| **Operations Center** | Health, backups, deployments, and safe mode |

## Documentation

- [Architecture Overview](docs/project/ARCHITECTURE.md)
- [Capability Map](docs/CapabilityMap.md)
- [Context Map](docs/ContextMap.md)
- [Ubiquitous Language](docs/UbiquitousLanguage.md)
- [Roadmap](docs/Roadmap.md)
- [ADRs](docs/adr/)

## License

[MIT](LICENSE)
