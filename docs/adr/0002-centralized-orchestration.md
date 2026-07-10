# 0002-centralized-orchestration

## Title: Centralized Orchestration of Conversational AI Workflow

## Status: Accepted

## Context

In an enterprise conversational AI platform like ConvoLab, a user request typically involves multiple steps: understanding the user\'s intent, retrieving relevant knowledge, generating an AI response, evaluating the response, and logging the entire process. Without a centralized orchestration mechanism, each component might directly call others, leading to a tangled web of dependencies and making the system difficult to manage, extend, and observe.

## Decision

We will centralize the orchestration of the conversational AI workflow within the `Execution` bounded context, specifically through the `IWorkflowEngine` interface and its implementation (`ConvoLabWorkflowEngine`). This engine will be responsible for defining the sequence of operations and coordinating interactions between various domain-specific engines (e.g., `IConversationEngine`, `IPromptEngine`, `IAIOrchestrator`).

## Consequences

*   **Simplified Workflow Management**: The entire flow of a conversational request is defined and managed in one place, making it easier to understand, modify, and debug.
*   **Reduced Coupling**: Individual engines do not need to know about each other\'s existence or implementation details. They only expose their capabilities through well-defined interfaces, which the `IWorkflowEngine` consumes.
*   **Enhanced Observability**: Centralized orchestration provides a single point where comprehensive tracing and logging can be implemented for the entire workflow, improving visibility into system behavior and performance.
*   **Improved Extensibility**: New steps or alternative implementations of existing steps can be introduced into the pipeline without affecting other engines, as long as the `IWorkflowEngine` is updated to incorporate them.
*   **Consistency**: Ensures that every conversational request follows a consistent and predictable path through the system.

## Alternatives Considered

*   **Decentralized Orchestration (Choreography)**: Allowing engines to communicate directly with each other (e.g., via domain events) was considered. While this can lead to more resilient systems in some distributed contexts, for the core conversational flow, it would introduce significant complexity in managing the sequence, error handling, and overall state of a request. The explicit control offered by centralized orchestration is preferred for this critical path.
*   **Service Bus for Orchestration**: Using a message broker or service bus to orchestrate the flow was considered. This is a valid approach for highly distributed systems, but for the initial architecture, it would introduce unnecessary infrastructure complexity. The current in-process orchestration provides sufficient flexibility and performance while keeping the architecture lean.
