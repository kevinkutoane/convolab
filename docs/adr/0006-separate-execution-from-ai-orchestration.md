# 0006-separate-execution-from-ai-orchestration

## Status
Accepted

## Context
Initially, the `IAIOrchestrator` interface was part of the `Application` layer, implying a direct dependency of the core execution logic on AI orchestration. As ConvoLab evolves into an enterprise platform, it's crucial to maintain a clear separation of concerns, especially between the core workflow execution engine and the specific mechanisms for interacting with AI providers. The `Execution` bounded context should focus solely on managing the workflow lifecycle, state transitions, and overall orchestration, while AI-specific interactions should be delegated to a dedicated AI orchestration service.

## Decision
The `IAIOrchestrationService` interface will be introduced within the `AI` bounded context (or a dedicated `AI.Application` layer if needed) to encapsulate all AI-specific interaction logic. The `Execution` bounded context will interact with this service through its defined interface, thereby decoupling the core workflow execution from the intricacies of AI provider integration. The `ExecutionContext` will be expanded to carry necessary AI-related parameters without directly exposing AI provider details to the core execution logic.

## Consequences
*   **Clearer Separation of Concerns**: The `Execution` context focuses purely on workflow management, while the `AI` context handles AI provider interactions.
*   **Enhanced Extensibility**: New AI providers can be integrated by implementing the `IAIOrchestrationService` without modifying the core `Execution` logic.
*   **Improved Testability**: Both the `Execution` engine and the `AI` orchestration logic can be tested independently.
*   **Reduced Coupling**: The core workflow engine is no longer directly dependent on AI provider specifics.
*   **Increased Complexity in AI Context**: The `AI` context will become more complex as it centralizes all AI interaction logic, including model selection, request/response handling, and error management.
*   **Updated Interfaces**: The `IAIOrchestrator` in the `Application` layer will likely delegate to an implementation that uses `IAIOrchestrationService`.
