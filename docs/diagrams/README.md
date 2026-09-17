# ConvoLab Architecture Diagrams

This directory contains the primary Mermaid architecture, context, dependency, and sequence diagrams for the ConvoLab platform.

All diagrams are updated to reflect the platform's current state as of **`v1.0.0-alpha.18`** and the active **`alpha.19`** operational-validation baseline.

---

## Diagram Index

### 1. System Topology & Architecture
* **[component_diagram.mmd](component_diagram.mmd)**:
  Comprehensive component topology depicting the React 19 Vite Studio, ASP.NET Core API host (including `SecurityHeadersMiddleware`, `SensitiveOutputSanitizerMiddleware`, and rate limiting), all 15 Application services, and Infrastructure adapters (EF Core PostgreSQL 16, Gemini, Deterministic executor, AES-GCM backup tooling, Data Protection archiver, and OpenTelemetry logging).
* **[dependency_graph.mmd](dependency_graph.mmd)**:
  Clean Architecture layer dependency graph illustrating strict inward dependency flow (`API` → `Application` → `Domain` ← `Infrastructure`) and the API layer's role as the dependency injection composition root.

### 2. Context & Capability Boundaries
* **[context_diagram.mmd](context_diagram.mmd)**:
  System context diagram illustrating external actors (engineers, Microsoft Entra ID OIDC, deployment promotion runner) interacting with ConvoLab Studio and Platform Core, as well as connections to external LLMs, document stores, PostgreSQL, and GitHub Container Registry (GHCR).
* **[bounded_context_relationships.mmd](bounded_context_relationships.mmd)**:
  Strategic Domain-Driven Design (DDD) bounded context mapping showing relationships between Core Orchestration (`Conversation`, `Workflow`, `Prompt`, `Knowledge`, `Intelligence`, `Evaluation`, `Plugins`), Governance & Observability (`IAM`, `Settings`, `Policy`, `Audit`, `Analytics`, `Tracing`), and Platform Operations (`Operations & DR Control Plane`).

### 3. Execution & Sequence Workflows
* **[workflow_execution_sequence_diagram.mmd](workflow_execution_sequence_diagram.mmd)**:
  End-to-end runtime simulation execution sequence showing turn submission, runtime configuration resolution, sealed `KnowledgePackage` retrieval, prompt rendering, Policy Center evaluation guardrails, Intelligence Engine planning/execution, normalized scoring in Evaluation Studio, and distributed trace persistence.
* **[workflow_event_flow.mmd](workflow_event_flow.mmd)**:
  Domain event lifecycle across bounded contexts during workflow/simulation runs, including trace spans, policy evaluation facts, execution telemetry, and safe transactional outbox emission to Platform Analytics.
* **[prompt_lifecycle.mmd](prompt_lifecycle.mmd)**:
  State transition model for governed prompt assets (`Draft` → `Review` → `Approved` → `Active` → `Deprecated` → `Archived`).

---

## Rendering Diagrams

Mermaid diagrams can be viewed directly in markdown viewers supporting Mermaid syntax (e.g. GitHub, GitLab, VS Code Markdown Preview).
