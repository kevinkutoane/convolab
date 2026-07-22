# ConvoLab Roadmap

Current stabilized baseline: `v1.0.0-alpha.11`.

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
| Intelligence Center and execution inspector | Complete and hardened |
| Evaluation Studio and persisted scorecards | Complete and hardened |
| Interaction and button audit gate | Complete |
| Policy Center | Complete and hardened |
| Trace Explorer | Complete and hardened |
| Replay Studio | Complete and hardened |
| Plugin Center | Complete and hardened |

## Platform Hardening Sprint 1

- Canonical Prompt and Knowledge lifecycle policies: Complete
- Application repository ports and EF isolation: Complete
- Optimistic concurrency and structured errors: Complete
- Liveness/readiness endpoints: Complete
- Layered test projects and CI gates: Complete and Docker-validated

## Phase 3 — Platform maturity

- Policy Engine behaviour and runtime decisions: Complete
- Evaluation Engine behaviour and persisted scorecards: Complete
- Trace Engine persistence and OpenTelemetry-aligned runtime model: Complete
- Plugin registry, versioning, compatibility and health: Complete
- Identity, authorization, tenants, teams, and audit: Next
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
