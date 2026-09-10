# ConvoLab Platform Manifest

## Vision

ConvoLab is the engineering platform where enterprise teams design, simulate, evaluate, observe, govern, and continuously improve conversational intelligence across providers, channels, and business systems.

## Mission

Provide a coherent, provider-neutral Platform Core and a suite of engineering products that make conversational AI systems reproducible, inspectable, governable, and safe to evolve.

## Product model

- **ConvoLab Platform** contains reusable domain and application capabilities.
- **ConvoLab Studio** is the visual engineering workspace that consumes Platform Core.
- **Adapters and plugins** connect providers, models, enterprise knowledge, tools, channels, and storage.

## Principles

1. Capabilities are reusable independently of any Studio page.
2. Domain language drives the architecture.
3. Business invariants live inside aggregates, not controllers or UI components.
4. Providers, channels, storage, and vendors are replaceable adapters.
5. Knowledge is governed before it is retrieved.
6. Prompts are versioned enterprise assets.
7. Intelligent execution is planned and policy-governed.
8. Conversation timeline and engineering trace remain distinct.
9. Every important execution can become reproducible and replayable.
10. Studio consumes Platform Core; it never duplicates core orchestration.

## Current capabilities

### Stable Platform Core & Capabilities

- Conversation Engine
- Workflow Engine
- Prompt Engine
- Knowledge Engine
- Intelligence Engine
- Policy Engine
- Evaluation Studio
- Trace Explorer
- Replay Studio
- Plugin Engine
- Workspace, Identity and Access Control v1
- Platform Analytics v1
- Backup, Restore & Disaster Recovery v1

### Product Workspaces

- ConvoLab Studio: Unified functional engineering workspace with simulation, governance, analytics, evaluation, trace inspection, replay, plugin governance, and workspace isolation.

## Current release

Platform Core and Studio are at `v1.0.0-alpha.18`. All core functional v1 capabilities are stable. Security & Compliance Hardening v1 (alpha.18) is delivered with hardened HTTP security response headers, an extended audit trail, a dedicated AuditController, SensitiveOutputSanitizerMiddleware, SensitiveTelemetryLogFilter, targeted rate-limiting on high-risk surfaces, SafeMode hardening, four new ProductionReadinessValidator rules, ThreatModel.md and SOC 2-aligned ComplianceControls.md. Deployment, Environment Promotion & Release Engineering v1 (alpha.17) is also delivered with immutable release manifests, dual SBOMs, cryptographic build provenance, container vulnerability gates, automated pre-migration backup enforcement, and an audited Environment Promotion control plane inside the Operations Center.

## Engineering Products

- Conversation Simulator
- Workflow Designer
- Prompt Studio
- Knowledge Studio
- Intelligence Center
- Policy Center
- Evaluation Studio
- Trace Explorer
- Replay Studio
- Plugin Center
- Workspace & Access Control
- Platform Analytics
- Operations Center

## Target users

- Conversational AI engineers
- Software and platform engineers
- Solution architects
- Conversation designers
- AI quality and evaluation teams
- Contact-centre technology teams
- Enterprise governance and risk teams
- Operations and support engineers

## Supported scenarios

- Provider-neutral conversational orchestration
- Enterprise knowledge retrieval with citations and governance
- Prompt lifecycle management and experimentation
- Conversation debugging and replay
- Model, provider, prompt, workflow, and knowledge comparison
- Human handoff and omnichannel integration through adapters
- Quality, safety, cost, latency, and reliability analysis

## Non-goals

Platform Core is not:

- an OpenAI-specific SDK wrapper;
- a vector database implementation;
- a contact-centre product;
- a general-purpose workflow engine;
- a UI-owned business application;
- a replacement for enterprise systems of record.

## Technology policy

- .NET 8 for Platform Core and API
- React and TypeScript for Studio
- PostgreSQL as the first production persistence adapter
- OpenTelemetry-aligned observability model
- Vendor SDKs isolated inside Infrastructure or plugins
- No framework types in Domain

## Current maturity

Platform Core and Studio are at `v1.0.0-alpha.18`. Conversation, Workflow, Prompt, Knowledge, Intelligence, Evaluation, Trace, Replay, Policy, Plugin Center, managed environments, trusted runtime attribution, workspace/environment Platform Analytics, Entra/Hybrid Authentication, Backup/Restore/Disaster Recovery v1, and Security & Compliance Hardening v1 (alpha.18) are functional capabilities. Production live enterprise SSO validation, managed vault adapters, enterprise operational rehearsal, and UAT sign-off remain beta work.
