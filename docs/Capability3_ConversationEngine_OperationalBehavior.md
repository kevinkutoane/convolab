# Capability 3: Conversation Engine (Operational Behavior)

This document outlines the implementation details and enhancements made to the ConvoLab Conversation Engine, focusing on operational behavior as per the specifications. The goal of this capability is to transform the Conversation Engine into a central business capability of the platform, modeling how conversations actually behave, while adhering to Domain-Driven Design (DDD) principles, SOLID principles, and immutability.

## Implemented Business Capabilities

The `Conversation` aggregate now supports a rich set of behaviors, enforcing invariants and business rules internally. Key methods implemented or enhanced include:

*   **Lifecycle Management:** `Create()`, `Start()`, `Pause()`, `Resume()`, `Complete()`, `Archive()`, `Restore()`, `ExpireConversation()`.
*   **Session Management:** `StartSession()`, `EndSession()`, `CloseInactiveSessions()`.
*   **Participant Management:** `AddParticipant()`, `RemoveParticipant()`.
*   **Message Management:** `AddMessage()`, `AttachKnowledgeReference()`.
*   **Memory & Context:** `UpdateMemory()`, `CreateSnapshot()`, `RestoreSnapshot()`, `UpdateContext()`.
*   **External References:** `AttachWorkflowExecution()`, `AttachEvaluation()`, `AttachTrace()`.

## Business Rules and Invariants

All business rules are enforced within the `Conversation` aggregate, ensuring that no application service implements business logic. Examples of enforced invariants include:

*   Cannot add messages to Archived conversations.
*   Cannot resume Completed conversations.
*   Cannot archive Active conversations.
*   Cannot create overlapping Sessions.
*   Cannot remove the final participant.
*   Cannot restore deleted conversations.

## Session Management

The Conversation Engine now supports multiple sessions per conversation. Each `ConversationSession` entity tracks its `Status`, `Participants`, `Messages`, `Timeline`, `Metadata`, `Duration`, and `CloseReason`. Sessions automatically maintain conversation statistics.

## Conversation Memory

The `ConversationMemory` entity has been expanded to support various memory concepts:

*   **Working Memory:** For transient, in-progress conversational data.
*   **Summary Memory:** For condensed summaries of past interactions.
*   **Semantic Memory Reference:** For linking to external semantic knowledge.
*   **Memory Window:** Defines the scope of memory (e.g., by messages, tokens, or time).
*   **Context Window:** Similar to memory window, but for context.
*   **Pinned Memory:** Allows marking specific memories as persistent.
*   **Conversation Snapshot:** Captures the state of the conversation at a point in time.

Memory remains implementation-independent.

## Context Management

The `ConversationContext` now manages a comprehensive set of contextual information, including:

*   `Intent`
*   `Workflow`
*   `Current Step`
*   `Execution Reference`
*   `Prompt Reference`
*   `Knowledge References`
*   `AI Provider Reference`
*   `AI Model Reference`
*   `Locale`
*   `Tenant`
*   `Variables`
*   `Conversation State`

## Message Model

The message model has been refined to support various message types, including `Streaming`, `Tool Messages`, `Function Messages`, `Assistant Messages`, `System Messages`, and future `Voice`, `Image`, and `Structured Messages`. Messages remain immutable once added to the conversation.

## Timeline

The `ConversationTimeline` has been expanded to include detailed entries for significant events, such as:

*   Conversation Started
*   Session Started
*   Participant Joined
*   Workflow Attached
*   Knowledge Attached
*   AI Response Linked
*   Evaluation Linked
*   Trace Linked
*   Conversation Completed

The timeline serves as a business history of the conversation.

## Statistics

The `Conversation` aggregate now exposes computed statistics, including:

*   `Message Count`
*   `Participant Count`
*   `Session Count`
*   `Average Response Time`
*   `Duration`
*   `Workflow Count`
*   `Evaluation Count`
*   `Attachment Count`
*   `Timeline Count`

These statistics are computed by the aggregate, reflecting its rich behavior.

## Specifications

Domain specifications have been introduced to encapsulate business rules and query the state of the conversation. Examples include `ConversationCanReceiveMessages`, `ConversationCanBeArchived`, `ConversationCanResume`, `ConversationHasActiveSession`, `ConversationHasParticipants`, and `ConversationHasWorkflow`.

## Application Layer

The `IConversationEngine` interface and its implementation have been updated to reflect the new aggregate capabilities. This includes new commands for lifecycle management, session management, participant management, message handling, memory and context updates, and external reference linking. Queries for retrieving conversation details, timeline, and statistics have also been added.

## Testing

Comprehensive unit tests have been written for the `Conversation` aggregate, covering business rules, lifecycle transitions, session behavior, memory behavior, timeline behavior, and statistics. The target test coverage for the Conversation domain is >95%.

## Non-Functional Requirements

The implementation maintains:

*   **DDD (Domain-Driven Design):** Strong encapsulation of business logic within the aggregate.
*   **SOLID Principles:** Adherence to single responsibility, open/closed, Liskov substitution, interface segregation, and dependency inversion principles.
*   **Immutability:** Key entities and value objects, such as `ConversationMessage`, are immutable.
*   **Explicit Domain Behavior:** All domain logic is explicit and resides within the domain layer.
*   **Rich Aggregate:** The `Conversation` aggregate is rich in behavior and enforces its own invariants.
*   **No Persistence Logic:** The domain layer remains free of persistence concerns.
*   **No Infrastructure:** No infrastructure-specific code pollutes the domain.
*   **No Framework Types:** Domain entities and value objects are free from external framework dependencies.
*   **Provider Independence:** The design allows for easy swapping of underlying AI or other service providers.

## Success Criteria

The Conversation Engine now behaves as a true enterprise Aggregate, coordinating its child entities and enforcing business rules internally. No application service implements business logic. The Conversation Engine is now the primary API through which every future platform capability interacts with conversations.
