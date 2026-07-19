# ConvoLab

**ConvoLab is an Enterprise Conversational AI Engineering Platform.** It gives engineering teams a provider-neutral foundation for modelling conversations, workflows, governed prompts, enterprise knowledge, intelligent execution, policy, evaluation, tracing, and plugins.

**ConvoLab Studio** is the visual workspace built on top of Platform Core. The Studio does not contain business orchestration; it consumes the ASP.NET Core platform API and presents each capability as an engineering workspace.

## Current milestone

- **Platform Core:** `v1.0.0-alpha`
- **Functional Intelligence Center release:** `v1.0.0-alpha.5`
- **Functional Evaluation Studio:** `v1`
- **Backend:** ASP.NET Core / .NET 8
- **Frontend:** React 19, TypeScript, Vite
- **Database adapter:** PostgreSQL-ready infrastructure
- **Architecture:** Clean Architecture and Domain-Driven Design

## Platform capabilities

| Capability | Purpose | Maturity |
| --- | --- | --- |
| Conversation Engine | Lifecycle, sessions, participants, messages, memory, context, and timeline | Stable |
| Workflow Engine | Versioned workflow definitions and governed runtime execution | Stable |
| Prompt Engine | Governed prompt assets, composition, versioning, approvals, and experiments | Stable |
| Knowledge Engine | Governed sources, retrieval strategies, citations, and sealed knowledge packages | Stable |
| Intelligence Engine | Provider-neutral execution planning, budgets, tools, streaming, retry, and fallback | Stable |
| Policy | Central runtime and governance decisions | Foundation |
| Evaluation | Persisted scorecards, quality gates, safety, relevance, and groundedness telemetry | Stable |
| Tracing | Cross-capability traces, spans, events, correlations, and artifacts | Foundation |
| Plugins | Extensible providers, tools, connectors, channels, and evaluators | Foundation |
| ConvoLab Studio | Visual engineering workspace consuming Platform Core | Active |

## Architecture

```text
ConvoLab Studio (React)
          |
          v
ASP.NET Core Platform API
          |
          v
Application contracts and use cases
          |
          v
Domain capabilities
          |
          v
Infrastructure adapters
```

The dependency rule is inward-facing:

```text
API -> Application -> Domain
Infrastructure -> Application + Domain
Domain -> nothing
```

Conversation does not select providers. Workflow does not implement retry. Prompt does not retrieve documents. Knowledge does not render prompts. Intelligence owns execution decisions. Policy owns governance decisions.

## Repository structure

```text
src/
  Api/              ASP.NET Core presentation layer
  Application/      capability contracts and application orchestration
  Domain/           aggregates, entities, value objects, events, and invariants
  Infrastructure/   persistence and external adapter implementations
  tests/            domain and architecture test projects
web/                 ConvoLab Studio

docs/
  Architecture/      architecture handbook and fitness rules
  adr/               architectural decision records
  capabilities/      capability-specific documentation
  diagrams/          Mermaid sources and generated diagrams
  releases/          platform release notes
```

## Run locally

### API

Requirements: a compatible .NET 8 SDK. `global.json` permits roll-forward to the latest installed .NET 8 feature band.

```bash
dotnet restore src/Api/ConvoLab.Api/ConvoLab.Api.csproj
dotnet run --project src/Api/ConvoLab.Api/ConvoLab.Api.csproj
```

The API listens using the local launch profile and exposes:

- `GET /health/live`
- `GET /health/ready`
- `GET /api/platform/status`
- Swagger in Development

### Studio

```bash
cd web
npm ci
npm run dev
```

Vite runs on `http://localhost:3000` and proxies `/api` and `/health` to the API on `http://localhost:5000`.

### Docker Compose

```bash
docker compose up --build
```

- Studio: `http://localhost:3000`
- API: `http://localhost:5000`
- PostgreSQL: `localhost:5432`

## Validate the Studio

```bash
cd web
npm ci
npm run lint
npm run build
npm run test -- --run
```

## Platform Hardening Sprint 1

The functional Simulator, Knowledge Studio, and Prompt Studio now use application-layer use cases, domain-owned lifecycle policies, EF-isolated repositories, optimistic revisions, RFC 7807 errors, liveness/readiness checks, and CI quality gates. See [`PLATFORM_HARDENING_SPRINT_1_REPORT.md`](PLATFORM_HARDENING_SPRINT_1_REPORT.md).


## Functional Workflow Designer

ConvoLab Studio now includes a governed visual Workflow Designer at `/workflows`.

It supports:

- Persistent workflow definitions
- Immutable semantic versions
- Start, Knowledge, Prompt, Decision, Intelligence, Response, and End nodes
- Draggable node positioning and labelled transitions
- Simple deterministic branch conditions using `contains:<term>`
- Graph validation for start/end nodes, decision branches, unreachable nodes, and dead ends
- Draft, approval, publication, deprecation, archive, and restore lifecycle
- Optimistic concurrency through workflow and version revisions
- Published workflow selection in Conversation Simulator
- Immutable workflow-path snapshots persisted with every new simulation run

A published workflow definition is separated from its runtime simulation snapshot, preserving the platform distinction between definition and execution.

## Functional Intelligence Center

Open `/intelligence` in ConvoLab Studio to inspect provider and model readiness, test provider connections, review persisted execution history, monitor tokens, cost, latency, retries and fallbacks, inspect individual runs, and preview provider/model admission decisions before execution.

The monthly AI budget is configured natively in South African rand through `CONVOLAB_MONTHLY_AI_BUDGET_ZAR`. Gemini pricing is optional and can be supplied through `GEMINI_INPUT_PRICE_ZAR_PER_1K` and `GEMINI_OUTPUT_PRICE_ZAR_PER_1K`; ConvoLab does not invent provider pricing or exchange rates when they have not been configured. See [`docs/IntelligenceCenter.md`](docs/IntelligenceCenter.md).

## Functional Evaluation Studio

Open `/evaluation` in ConvoLab Studio to create reusable scorecards; review groundedness, relevance, safety, overall quality, failed gates, seven-day trends, and individual simulator runs; and preview a saved scorecard in the policy sandbox without duplicating evaluation logic in the browser.

Quality thresholds can be configured through the `Evaluation` appsettings section or the `CONVOLAB_EVALUATION_*` environment variables documented in [`docs/EvaluationStudio.md`](docs/EvaluationStudio.md).

## Product direction

Platform Core exists to support engineering products without embedding business logic inside those products:

- Conversation Explorer
- Workflow Designer
- Prompt Studio
- Knowledge Studio
- Intelligence Center
- Policy Center
- Evaluation Studio
- Trace Explorer
- Replay Studio
- AI Playground
- Enterprise operations and analytics

The signature long-term experience is **Conversation Replay**: re-run an immutable conversation snapshot with a different prompt, knowledge snapshot, workflow, model, provider, or policy, then compare quality, latency, cost, and trace output.

## Documentation

Start with:

- [`docs/PlatformManifest.md`](docs/PlatformManifest.md)
- [`docs/CapabilityMap.md`](docs/CapabilityMap.md)
- [`docs/ContextMap.md`](docs/ContextMap.md)
- [`docs/EventCatalog.md`](docs/EventCatalog.md)
- [`docs/Architecture/README.md`](docs/Architecture/README.md)
- [`docs/Roadmap.md`](docs/Roadmap.md)

## License

MIT — see [`LICENSE`](LICENSE).

## Functional Conversation Simulator

ConvoLab Studio now includes a functional end-to-end simulator backed by the ASP.NET Core Platform API and a deterministic local intelligence provider.

Start the API:

```bash
dotnet run --project src/Api/ConvoLab.Api/ConvoLab.Api.csproj
```

Start Studio in a second terminal:

```bash
cd web
npm install
npm run dev
```

Open `http://localhost:3000/conversations`, create a simulation, and try:

> Can I claim for hail damage?

The inspector displays the governed knowledge package, rendered prompt, execution plan, token and cost telemetry, evaluation scores, trace timeline, and replay controls. Select **Retry once** or **Fallback** to exercise the Intelligence Engine recovery policies without external API keys.

## Functional Knowledge Studio

Open `/knowledge` in ConvoLab Studio to create a collection, upload PDF/DOCX/TXT/Markdown documents, process and publish them, inspect chunks, and test retrieval. Published collections automatically appear in Conversation Simulator and replace hardcoded knowledge packages.

## Functional Prompt Studio

ConvoLab Studio now supports persistent, governed prompt authoring at `/prompts`. Prompt versions are immutable, move through an approval lifecycle, render with runtime variables, and become selectable in Conversation Simulator only after publication. See `docs/PromptStudio.md`.
