# ADR 0010: Conversation Engine as Central Aggregate Root

## Status
Accepted

## Context
ConvoLab is evolving from a technical pipeline orchestrator into an Enterprise Conversational AI Platform. In the previous architecture, "Conversation" was a simple domain model, often treated as a side-effect of workflow execution. To support complex enterprise requirements (multimodality, session management, long-term memory, and auditing), the Conversation domain needs to be the central business capability.

## Decision
We will transform the Conversation domain into a behavior-rich Aggregate Root that owns its lifecycle and coordinates all conversational entities.

### 1. Conversation as the Heart
The Conversation Aggregate Root will now be the primary entry point for all conversational interactions. Every future capability (Prompt Engine, Knowledge Engine, AI Orchestrator) will interact with or reference the Conversation.

### 2. Explicit Lifecycle Management
We introduce a formal state machine for the Conversation lifecycle:
- **Created**: Initial state.
- **Started**: Conversation has begun.
- **Active**: Ready for messages.
- **Waiting**: Awaiting external input.
- **Processing**: AI or system is generating a response.
- **Completed**: Business goal achieved.
- **Archived**: Read-only historical state.
- **SoftDeleted**: Removed from active view.

### 3. Rich Session Management
Conversations now contain multiple **Sessions**, allowing for a single long-running conversation to span multiple interactions across different channels (WhatsApp, Teams, Voice) while maintaining a single business context.

### 4. Strategic Memory Model
Conversation Memory is modeled as a first-class capability with explicit strategies (Short-Term, Long-Term, Summary) and windows (Message-based, Token-based, Time-based).

### 5. Business Timeline
A **Conversation Timeline** is introduced to tell the "business story" of the conversation, distinct from technical tracing. It records significant business events like "Participant Joined", "Workflow Linked", and "AI Response Received".

### 6. Immutability and Behavior-Rich Interface
- **Messages** are immutable.
- The **Conversation Engine** exposes business capabilities (e.g., `StartConversation`, `AddParticipant`, `LinkWorkflow`) instead of CRUD operations.

## Consequences

### Positive
- **Clear Business Ownership**: Conversation rules are enforced in one place.
- **Extensibility**: New participant roles and message types can be added without changing the core aggregate.
- **Auditability**: The timeline provides a high-level business audit trail.
- **Separation of Concerns**: Conversation manages state; Workflows manage execution logic.

### Negative
- **Increased Complexity**: The aggregate root is now larger and more complex.
- **State Management**: Careful management of status transitions is required to prevent invalid states.
- **Persistence Complexity**: Mapping a rich aggregate with many collections to a database (Drizzle/SQL) will require more sophisticated repository logic.
