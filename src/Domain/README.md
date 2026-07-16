# Domain Layer

The **Domain Layer** is the core of the application, containing all business logic and domain models that are independent of any framework or external dependencies. This layer represents the heart of the application and should remain pure, testable, and framework-agnostic.

## Conversation Engine (Core Aggregate)

The **Conversation** domain has been transformed into a behavior-rich **Aggregate Root**, serving as the central business capability of the ConvoLab platform.

### Key Capabilities

- **Lifecycle Management**: Explicit state transitions (Created -> Started -> Active -> Waiting -> Processing -> Completed -> Archived).
- **Session Management**: Support for multiple sessions within a single conversation, enabling cross-channel continuity.
- **Strategic Memory**: Memory modeled as a strategic capability with short-term, long-term, and summary types.
- **Business Timeline**: A rich audit trail of business-significant events, distinct from technical tracing.
- **Participant Roles**: Extensible model supporting Customer, Assistant, Human Agent, Supervisor, Observer, System, and Tool roles.
- **Immutable Messages**: Messages are immutable entities with rich references to attachments, workflows, evaluations, and traces.

### Structure

```
Domain/ConvoLab.Domain/Conversation/
├── Aggregates/         # Conversation.cs (Aggregate Root)
├── Entities/           # ConversationMessage, Session, Participant, Memory, Attachment, Snapshot
├── Enums/              # ConversationStatus, ParticipantRole, SessionStatus, MemoryType
├── Events/             # Rich Domain Events (e.g., MessageAdded, SessionStarted)
├── Interfaces/         # IConversationRepository, IConversationFactory
├── Specifications/     # Business invariants (e.g., ActiveConversationSpecification)
└── ValueObjects/       # ConversationId, ConversationContext, ConversationTimeline, Metadata
```

## Structure (General)

```
Domain/
├── Entities/           # Core business entities with identity
├── ValueObjects/       # Immutable value objects
├── Aggregates/         # Aggregate roots
├── Events/             # Domain events
├── Specifications/     # Business logic specifications
├── Interfaces/         # Contracts for repositories and services
└── Exceptions/         # Domain-specific exceptions
```

## Key Principles

### 1. No External Dependencies
The Domain Layer must never reference any external frameworks, libraries, or other layers. It should only depend on .NET Base Class Library (BCL) types.

### 2. Business Logic First
All business rules and validations should be expressed in the domain model. The domain should be a complete representation of the business.

### 3. Immutability Where Possible
Value Objects should be immutable. Entities should minimize mutable state and encapsulate business logic.

### 4. Rich Domain Model
Entities should contain behavior, not just data. Business logic should live in the domain model, not in application services.

## Testing

Domain Layer tests should focus on:
- Business logic correctness
- Entity behavior and state transitions
- Value Object creation and validation
- Aggregate invariants
- Domain Event generation

For the Conversation domain, tests are located in `src/tests/ConvoLab.Domain.Tests/Conversation/`.

## Guidelines

1. **Keep it Pure**: No I/O, no external calls, no framework dependencies
2. **Express Business Rules**: Make implicit business rules explicit in code
3. **Use Meaningful Names**: Domain language should be reflected in class and method names
4. **Encapsulate State**: Use private setters and factory methods to maintain invariants
5. **Document Assumptions**: Add XML comments explaining non-obvious business logic

## Related Documentation

- See `Application/README.md` for how the Domain Layer is used by application services
- See `Infrastructure/README.md` for how the Domain Layer is persisted
- See `Api/README.md` for how the Domain Layer is exposed through endpoints
