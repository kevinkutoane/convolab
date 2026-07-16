# ConvoLab Domain Event Catalog

This catalog documents the key Domain Events within the ConvoLab platform. These events facilitate loose coupling between bounded contexts.

## Conversation Events

### `ConversationStartedEvent`
*   **Publisher**: Conversation Engine
*   **Consumers**: Tracing Engine, Workflow Engine (optional)
*   **Payload**: `ConversationId`, `UserId`, `Timestamp`
*   **Business Meaning**: A new conversational session has been initiated by a user.
*   **Lifecycle**: Emitted once per conversation aggregate creation.
*   **Version**: v1
*   **Related ADR**: ADR-0010 (Conversation Engine as Central Aggregate)

### `MessageReceivedEvent`
*   **Publisher**: Conversation Engine
*   **Consumers**: Workflow Engine, Tracing Engine
*   **Payload**: `ConversationId`, `MessageId`, `Content`, `Timestamp`
*   **Business Meaning**: The user has provided new input requiring processing.
*   **Lifecycle**: Emitted for every user turn.
*   **Version**: v1

## Workflow (Execution) Events

### `ExecutionStartedEvent`
*   **Publisher**: Workflow Engine
*   **Consumers**: Tracing Engine, Conversation Engine (for status updates)
*   **Payload**: `ExecutionId`, `WorkflowId`, `CorrelationId`, `Timestamp`
*   **Business Meaning**: A workflow instance has begun execution.
*   **Lifecycle**: Emitted when a workflow transitions to the 'Running' state.
*   **Version**: v1
*   **Related ADR**: ADR-0007 (Workflow Definition and Execution)

### `ExecutionCompletedEvent`
*   **Publisher**: Workflow Engine
*   **Consumers**: Tracing Engine, Evaluation Engine, Conversation Engine
*   **Payload**: `ExecutionId`, `ResultStatus`, `Output`, `Timestamp`
*   **Business Meaning**: A workflow has finished, successfully or otherwise.
*   **Lifecycle**: Emitted at terminal states (Completed, Failed, Cancelled).
*   **Version**: v1

## Prompt Events

### `PromptTemplateCreatedEvent`
*   **Publisher**: Prompt Engine
*   **Consumers**: Audit Log, Tracing Engine
*   **Payload**: `PromptTemplateId`, `Name`, `Type`, `Version`, `Timestamp`
*   **Business Meaning**: A new prompt template asset has been added to the platform.
*   **Lifecycle**: Emitted upon initial creation.
*   **Version**: v1

### `PromptVersionApprovedEvent`
*   **Publisher**: Prompt Engine
*   **Consumers**: Governance, Deployment Pipelines
*   **Payload**: `PromptTemplateId`, `Version`, `ApprovedBy`, `Timestamp`
*   **Business Meaning**: A specific version of a prompt is approved for production use.
*   **Lifecycle**: Emitted when governance policies are satisfied.
*   **Version**: v1

## Tracing Events

### `TraceStartedEvent`
*   **Publisher**: Tracing Engine
*   **Consumers**: Observability Platforms (e.g., OpenTelemetry exporters)
*   **Payload**: `TraceId`, `CorrelationId`, `OperationName`, `Timestamp`
*   **Business Meaning**: A new distributed trace has been initiated.
*   **Lifecycle**: Emitted at the start of a major operation.
*   **Version**: v1
*   **Related ADR**: ADR-0004 (Distributed Tracing Model)

### `TraceCompletedEvent`
*   **Publisher**: Tracing Engine
*   **Consumers**: Observability Platforms, Metrics Aggregators
*   **Payload**: `TraceId`, `TotalDuration`, `TokenUsage`, `Timestamp`
*   **Business Meaning**: A trace has concluded, finalizing performance and cost metrics.
*   **Lifecycle**: Emitted when the root span closes.
*   **Version**: v1
