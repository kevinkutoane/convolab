# ConvoLab Project Review Report

## 1. Project Overview & Aim
**ConvoLab** is an Enterprise Conversational AI Engineering Platform designed to provide a provider-neutral foundation for building and governing complex AI-driven conversations and workflows. 

Its primary aim is to empower engineering teams to model, execute, and evaluate AI interactions with enterprise-grade governance, observability, and control.

## 2. Architecture & Design Patterns
The project follows a modern, decoupled architecture:

- **Backend (ASP.NET Core / .NET 8):**
    - **Clean Architecture:** Strictly enforced separation of concerns into `Api`, `Application`, `Domain`, and `Infrastructure`.
    - **Domain-Driven Design (DDD):** Business logic is centered around rich domain models (entities, value objects, aggregates) organized by bounded contexts (Capabilities).
    - **Dependency Rule:** Inward-facing dependencies (API -> Application -> Domain).
- **Frontend (React 19 / TypeScript / Vite):**
    - **Single Page Application (SPA):** A visual workspace called "ConvoLab Studio" that consumes the platform API.
    - **Component-Based:** Organized into pages, components, hooks, and services.
    - **Lazy Loading:** Routes and components are loaded on demand for performance.

## 3. Core Capabilities (Platform Engines & Studios)
ConvoLab is composed of several high-level functional areas:

| Capability | Functional Scope |
| :--- | :--- |
| **Conversation Engine** | Manages sessions, participants, messages, and memory. |
| **Workflow Engine** | Visual designer and runtime for governed AI workflows. |
| **Prompt Engine** | Governed authoring, versioning, and rendering of prompts. |
| **Knowledge Engine** | Retrieval-Augmented Generation (RAG) with document processing. |
| **Intelligence Engine** | Provider-neutral execution planning, retry, and fallback. |
| **Policy Center** | Governance rules, cost caps, and safety constraints. |
| **Evaluation Studio** | Scorecards, metric tracking, and human review of AI responses. |
| **Trace Explorer** | Full observability with span waterfalls and redaction support. |
| **Replay Studio** | Immutable baselines for testing configuration changes. |
| **Plugin Center** | Governance registry for providers, tools, and connectors. |
| **Platform Analytics** | Cost, usage, and quality telemetry for workspaces. |

## 4. Current Technical Status
- **Current Milestone:** `v1.0.0-alpha.14`
- **Active Workstream:** `alpha.15 — Microsoft Entra ID, External Identities & Hybrid Authentication`.
- **Database:** PostgreSQL-ready (uses SQLite for local development).
- **Frontend State:** Active development on "Active" maturity features like Analytics and Workspace Identity.

## 5. Technology Stack
- **Languages:** C# (Backend), TypeScript (Frontend).
- **Frameworks:** .NET 8, React 19, Vite, Tailwind CSS.
- **ORM:** Entity Framework Core.
- **Testing:** Playwright (Browser smoke tests), Domain/Architecture test projects.
- **Infrastructure:** Docker Compose for local orchestration.

## 6. Key Directories
- `src/Api/`: ASP.NET Core presentation layer (Controllers, Middleware).
- `src/Application/`: Use cases and orchestration logic.
- `src/Domain/`: Core business entities and domain events.
- `src/Infrastructure/`: Database persistence and external API adapters.
- `web/src/`: React frontend source (Pages, Components, Hooks).
- `docs/`: Architecture handbook, ADRs, and capability deep-dives.

## 7. Observations & Recommendations
- **Sophisticated Governance:** The platform places a heavy emphasis on immutability and approval lifecycles for prompts, workflows, and policies.
- **Clean Separation:** The "Intelligence Engine" abstraction allows switching between AI providers (e.g., Gemini) without affecting business logic.
- **Production Readiness:** While in alpha, the project has a very high standard of engineering, including RFC 7807 error handling, optimistic concurrency, and extensive telemetry.
- **Local Dev Setup:** Well-documented `README.md` and `global.json` make it easy to start.
