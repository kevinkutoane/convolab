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

### Stable Platform Core

- Conversation Engine
- Workflow Engine
- Prompt Engine
- Knowledge Engine
- Intelligence Engine
- Execution

### Capability foundations

- Policy
- Tracing
- Plugins
- Identity

### Active product

- ConvoLab Studio with functional Conversation Simulator, Knowledge Studio, and Prompt Studio

## Planned products

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
- Analytics and Operations Console

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

Platform Core is tagged conceptually as `v1.0.0-alpha`: its architecture is stable enough for Studio and adapter development. Evaluation Studio now provides persisted scorecards and quality-gate telemetry; Policy, Tracing, Plugins, authentication, generated API clients, and production operations remain incomplete. Prompt, Knowledge, Simulation, and Evaluation persistence are functional and under alpha hardening.
