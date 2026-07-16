# Architecture Principles

The ConvoLab platform is guided by the following core architectural principles. These principles ensure the system remains maintainable, scalable, and adaptable as it evolves into an enterprise-grade platform.

## 1. Clean Architecture

We adhere strictly to Clean Architecture. Dependencies must always point inward toward the Domain layer.
*   **Domain**: Contains enterprise logic, aggregates, entities, and value objects. It has zero external dependencies.
*   **Application**: Contains business use cases (Commands/Queries) and interfaces for infrastructure.
*   **Infrastructure**: Contains implementations for data access, external APIs, and cross-cutting concerns.
*   **Presentation/Api**: The entry point, responsible only for HTTP routing and mapping.

## 2. Domain-Driven Design (DDD)

We model the software to match the business domain.
*   **Bounded Contexts**: Capabilities like Prompt, Workflow, and Conversation are strictly separated.
*   **Aggregates**: Data modifications occur only through Aggregate Roots to ensure consistency.
*   **Ubiquitous Language**: The code uses the same terminology as the domain experts (e.g., `Execution`, `Trace`, `PromptTemplate`).

## 3. Event-Driven Integration

Bounded contexts communicate asynchronously via Domain Events to minimize tight coupling.
*   When a `Conversation` starts, it emits an event. The `Tracing` engine listens to this event rather than the Conversation engine calling the Tracing engine directly.
*   This allows new capabilities (like an Audit Log) to be added without modifying existing core logic.

## 4. Provider Agnosticism

The core domain must never depend on specific AI providers (like OpenAI or Anthropic).
*   All interactions with LLMs or external services are abstracted behind interfaces (e.g., `IAIOrchestrator`).
*   This ensures we can swap providers, use local models, or implement routing logic without changing business rules.

## 5. Observability as a First-Class Citizen

Tracing and metrics are not afterthoughts.
*   Every significant operation generates a `TraceSpan`.
*   Token usage, latency, and model selection are captured for every AI interaction to enable cost analysis and performance tuning.

## 6. Immutable Assets

Key business artifacts are treated as immutable to ensure reproducibility and governance.
*   **Prompts**: Once a prompt version is published, it cannot be changed. Edits create a new version.
*   **Executions**: An execution record represents a historical fact and is append-only.
