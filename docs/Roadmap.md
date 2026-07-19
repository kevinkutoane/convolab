# ConvoLab Roadmap

## Phase 1 — Platform Core

| Capability | Status |
| --- | --- |
| Clean Architecture foundation | Complete |
| Workflow and Execution | Complete |
| Conversation Engine | Complete |
| Prompt Engine | Complete |
| Knowledge Engine | Complete |
| Intelligence Engine | Complete |
| Platform Architecture Review v1 | Complete for alpha baseline |

## Phase 2 — ConvoLab Studio

| Product surface | Status |
| --- | --- |
| Studio shell and navigation | Complete |
| Platform dashboard | Complete |
| Capability workspaces and empty states | Complete |
| Command palette and responsive shell | Complete |
| Live platform-status API | Complete |
| Conversation Simulator | Complete |
| Workflow Designer editor | Complete |
| Prompt Studio editor | Complete and hardened |
| Knowledge Studio ingestion and retrieval | Complete and hardened |
| Intelligence execution inspector | Next |

## Platform Hardening Sprint 1

- Canonical Prompt and Knowledge lifecycle policies: Complete
- Application repository ports and EF isolation: Complete
- Optimistic concurrency and structured errors: Complete
- Liveness/readiness endpoints: Complete
- Layered test projects and CI gates: Implemented; backend/Docker execution pending environment validation

## Phase 3 — Platform maturity

- Policy Engine behaviour and runtime decisions
- Evaluation Engine behaviour and scorecards
- Trace Engine persistence and OpenTelemetry adapter
- Plugin SDK and adapter discovery
- Identity, authorization, tenants, teams, and audit
- Persistence repositories and migrations
- Secret management and configuration governance

## Phase 4 — Signature engineering products

- Conversation Simulator
- Conversation Replay Studio
- Side-by-side execution comparison
- Prompt experiments against recorded conversations
- Knowledge snapshot comparison
- Model and provider evaluation
- Cost and latency explorer

## Phase 5 — Enterprise adapters

- OpenAI and Azure OpenAI
- Gemini and Anthropic
- Local and internal models
- SharePoint and Microsoft Graph
- Dynamics 365
- Infobip
- Genesys Cloud
- SQL and REST data sources
- Enterprise identity providers

## Phase 6 — Developer ecosystem

- .NET SDK
- TypeScript SDK
- Python SDK
- CLI
- Plugin templates
- Marketplace and reusable capability packs
- Deployment and operations console
