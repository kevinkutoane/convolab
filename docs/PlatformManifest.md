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

Platform Core and Studio are at `v1.0.0-enterprise — Release Candidate`. All core functional v1 capabilities are stable, and the four strategic enterprise pillars are fully delivered:
1. **Enterprise Governance, Security & Compliance**: Automated PII/PHI redaction engine (POPIA/GDPR aligned), forward-linked SHA-256 cryptographic audit trail (`AuditHashChain`), and pre-execution prompt guardrails.
2. **Omnichannel Enterprise Connectors**: Native Infobip & WhatsApp Business integration (HMAC-SHA256 signature verification), rich messaging, real-time human escalation protocol, and Enterprise Hybrid RAG 2.0 (BM25 + Dense Semantic RRF $k=60$).
3. **Multi-Environment ALM**: Automated multi-environment promotion pipeline (`Dev` → `Staging` → `Prod`), source health gating, and instant rollback recovery.
4. **Executive Observability & FinOps**: Executive TCO & Human Parity ROI in ZAR (`R45.00 ZAR` benchmark), unit economics, multi-dimensional spend attribution, and automated golden-dataset regression CI/CD quality gates.

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

Platform Core and Studio are at `v1.0.0-enterprise — Release Candidate`. All foundational capabilities (Conversation, Workflow, Prompt, Knowledge, Intelligence, Evaluation, Trace, Replay, Policy, Plugin Center, IAM, Platform Analytics, Entra/Hybrid Authentication, and Backup/Restore/DR) and the four strategic enterprise pillars are fully delivered and verified with 100% test pass rate (526 tests). Live corporate tenant Entra validation remains an environment gate pending external tenant provisioning.
