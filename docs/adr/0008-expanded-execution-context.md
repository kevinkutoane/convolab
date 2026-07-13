# 0008-expanded-execution-context

## Status
Accepted

## Context
In Sprint 1, the `ExecutionContext` was introduced as a value object to carry essential information throughout the workflow. As the system evolves and the complexity of conversational AI workflows increases, the need for a more comprehensive and flexible context object has become apparent. The `ExecutionContext` needs to encapsulate a broader range of parameters, references, and metadata to support advanced features such as multi-tenancy, localization, feature flagging, dynamic AI model selection, and memory management, without introducing direct dependencies between different engine implementations.

## Decision
The `ExecutionContext` value object will be significantly expanded to include a wider array of properties. It will remain immutable to ensure consistency throughout the workflow execution. The expanded context will include, but not be limited to, properties such as `ExecutionId`, `ConversationId`, `WorkflowId`, `TenantId`, `UserId`, `CorrelationId`, `Culture`, `Locale`, `Timezone`, `FeatureFlags`, `SelectedProvider`, `SelectedModel`, `ExecutionVariables`, `MemoryReference`, `PromptReference`, `KnowledgeReference`, `Metadata`, `Attachments`, `ExecutionStartTime`, and `ExecutionDeadline`.

## Consequences
*   **Comprehensive Context**: The expanded `ExecutionContext` provides a single, rich source of truth for all relevant parameters during a workflow run, reducing the need for engines to fetch information independently.
*   **Enhanced Flexibility**: Supports advanced features like dynamic AI model selection, A/B testing via feature flags, and multi-tenant operations by centralizing configuration.
*   **Improved Decoupling**: By passing a comprehensive context, individual engines can remain focused on their core responsibilities without needing to know the origin or management of all parameters.
*   **Increased Object Size**: The `ExecutionContext` will become larger, which might have minor implications for serialization and memory usage, though these are expected to be negligible given its value object nature.
*   **Maintainability**: Changes to the `ExecutionContext` will require careful consideration due to its widespread use, but its immutability helps manage complexity.
*   **Clarity**: The explicit inclusion of various references (e.g., `MemoryReference`, `PromptReference`) makes the data flow within the workflow more transparent.
