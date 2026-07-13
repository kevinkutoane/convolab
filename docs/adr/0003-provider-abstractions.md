# 0003-provider-abstractions

## Title: Abstraction of External AI Providers

## Status: Accepted

## Context

The ConvoLab platform aims to be an Enterprise Conversational AI platform, which implies supporting various AI models and providers (e.g., OpenAI, Gemini, custom on-premise models). Tightly coupling the application logic to a specific AI provider\'s SDK or API would lead to vendor lock-in, make it difficult to switch providers, and complicate testing and maintenance. This would hinder the platform\'s extensibility and long-term viability.

## Decision

We will abstract all external AI providers behind well-defined interfaces within the `AI` bounded context and consumed by the `IAIOrchestrator` in the `Execution` bounded context. These interfaces will define the core capabilities required from an AI model, such as text completion, embedding generation, and potentially function calling, without exposing provider-specific details. Implementations of these interfaces will reside in the `Infrastructure` layer.

## Consequences

*   **Vendor Agnostic**: The core application logic remains independent of any specific AI provider. Switching providers becomes a matter of implementing a new adapter for the defined interfaces and configuring the dependency injection.
*   **Enhanced Testability**: The application can be tested against mock or fake implementations of the AI provider interfaces, allowing for robust unit and integration testing without making actual calls to external services.
*   **Flexibility and Extensibility**: New AI models or providers can be integrated with minimal impact on the existing codebase. This supports the platform\'s goal of being an adaptable enterprise solution.
*   **Cost Optimization**: The ability to easily switch between providers allows for strategic decisions based on cost, performance, and feature sets, preventing vendor lock-in and enabling better resource management.
*   **Security and Compliance**: Centralizing AI interactions through an abstraction layer provides a single point for implementing security measures, data governance, and compliance requirements related to AI usage.

## Alternatives Considered

*   **Direct Integration**: Directly integrating with AI provider SDKs throughout the application was rejected due to the high coupling, vendor lock-in, and increased complexity in managing multiple providers.
*   **Separate Microservices per Provider**: While a valid approach for very large-scale systems, creating separate microservices for each AI provider was deemed an over-engineering for the current stage. The abstraction layer within the existing architecture provides sufficient flexibility without introducing the overhead of distributed systems for this specific concern.
