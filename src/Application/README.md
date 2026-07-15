# Application Layer

The **Application Layer** orchestrates the domain logic and bridges the gap between the API/UI and the Domain Layer. It implements the use cases of the application using the CQRS (Command Query Responsibility Segregation) pattern with MediatR.

## Conversation Engine Service

The **ConversationEngine** is the primary application service responsible for orchestrating conversational workflows. It implements the `IConversationEngine` interface and operates on the `Conversation` Aggregate Root.

### Responsibilities

- **Orchestration**: Coordinating multiple domain entities (Sessions, Participants, Messages) within the Conversation Aggregate.
- **State Persistence**: Ensuring changes to the Conversation state are persisted through the `IConversationRepository`.
- **Capability Exposure**: Providing a clean, behavior-rich API for the UI and external integrations.

### Key Operations

- `CreateConversationAsync`: Initializes a new conversation with a creator and metadata.
- `StartSessionAsync`: Begins a new interaction session within a conversation.
- `AddMessageAsync`: Appends an immutable message and automatically links it to the active session.
- `UpdateMemoryAsync`: Updates conversation memory based on a specific strategy and window.
- `LinkWorkflow/Evaluation/TraceAsync`: Establishes cross-capability links for auditing and orchestration.

## Structure

```
Application/
├── Commands/           # Command definitions and handlers
├── Queries/            # Query definitions and handlers
├── DTOs/               # Data transfer objects
├── Validators/         # FluentValidation validators
├── Interfaces/         # Repository and service contracts (e.g., IConversationEngine)
├── Services/           # Implementation of application services (e.g., ConversationEngine)
├── Exceptions/         # Application-specific exceptions
└── Mappings/           # Object mapping configurations
```

## Key Principles

### 1. CQRS Pattern
- **Commands** modify state and return minimal results
- **Queries** retrieve data and never modify state
- Handlers implement the business logic for each command/query

### 2. Dependency Inversion
The Application Layer defines interfaces that the Infrastructure Layer implements. This maintains the dependency direction toward the domain.

### 3. Input Validation
All input should be validated using FluentValidation before being passed to handlers.

### 4. No Framework Dependencies
The Application Layer should not reference ASP.NET Core or other UI frameworks directly.

## Testing

Application Layer tests should focus on:
- Command/Query handler logic
- Validator rules
- Error handling and exceptions
- Integration with repositories (using mocks)

## Guidelines

1. **Keep Handlers Focused**: Each handler should implement a single use case
2. **Use Validators**: Always validate input at the handler level
3. **Leverage Domain Logic**: Handlers should orchestrate domain logic, not implement it
4. **Async/Await**: Use async patterns for I/O operations
5. **Error Handling**: Throw meaningful exceptions that the API can handle

## Related Documentation

- See `Domain/README.md` for the business logic and domain models
- See `Infrastructure/README.md` for repository implementations
- See `Api/README.md` for how handlers are invoked from endpoints
