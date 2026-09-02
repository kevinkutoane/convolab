# ConvoLab Roadmap

Current release: `v1.0.0-alpha.17`.

Delivered workstream: `alpha.16 — Backup, Restore & Disaster Recovery`. The scope covers defining the DR runbook, safe PostgreSQL snapshot restoration, handling of data protection keys across environments, active Operations Center telemetry for RPO compliance, and the Operations Center UI overhaul.

Delivered workstream: `alpha.15 — Microsoft Entra ID, External Identities & Hybrid Authentication`. The scope covers tenant-specific OIDC, explicit identity linking (provider + issuer + subject), opaque application sessions, hybrid/local policy, break glass, and truthful identity-provider Operations evidence. Live tenant validation is not executed (provider acceptance is StubValidated) and remains an environment gate before live enterprise tenant readiness can be claimed.

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
| Workspace, Identity and Access | Implemented; security and isolation acceptance in progress |
| Platform Analytics v1 | Complete; controlled UAT candidate |

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
- Identity, authorization, workspaces, memberships, service identities, and audit: Implemented; acceptance in progress
- Persistence repositories and migrations
- Secret management and configuration governance: Complete
- Workspace/environment Platform Analytics: Complete; organisation-wide rollups reserved

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

## Alpha.17 baseline closure

- Live Entra organisational validation: **environment-dependent**; implementation exists, but live tenant acceptance remains an environment gate.
- Backup and restore implementation and verified recovery objectives: **implemented**; final artifact and recovery evidence remains subject to the release/operations evidence set.
- Deployment promotion and hardened UAT/Production manifests: **implemented**; authoritative release evidence remains subject to CI artifact assembly.
- Supply-chain artifacts and SBOM release workflow: **implemented in CI**; successful evidence for the authoritative baseline remains to be attached and verified.
- Full load/endurance evidence and the final operational readiness report: **pending**.

## Next planning gate

- `alpha.18 — Security & Compliance Hardening`: **planning only until the alpha.17 baseline is green, reproducibly built, and frozen**.
- The alpha.18 plan is maintained separately and must not be treated as delivered functionality.

## Phase 6 — Developer ecosystem

- .NET SDK
- TypeScript SDK
- Python SDK
- CLI
- Plugin templates
- Marketplace and reusable capability packs
- Deployment and operations console
