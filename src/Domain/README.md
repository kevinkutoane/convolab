# Domain Layer

The **Domain Layer** is the core of the application, containing all business logic and domain models that are independent of any framework or external dependencies. This layer represents the heart of the application and should remain pure, testable, and framework-agnostic.

## Purpose

The Domain Layer defines:

- **Entities**: Core business objects with identity and lifecycle (e.g., User, Order, Conversation)
- **Value Objects**: Immutable objects that represent a value without identity (e.g., Money, Email, Address)
- **Aggregates**: Collections of entities and value objects that are treated as a single unit
- **Domain Events**: Events that represent something significant that happened in the business domain
- **Specifications**: Reusable business logic encapsulated as query specifications
- **Interfaces**: Contracts that other layers must implement (repositories, services, etc.)

## Structure

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

## Example: Entity Definition

```csharp
public abstract class Entity
{
    public int Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }

    protected Entity() { }

    protected Entity(int id)
    {
        Id = id;
        CreatedAt = DateTime.UtcNow;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other) return false;
        return Id == other.Id;
    }

    public override int GetHashCode() => Id.GetHashCode();
}
```

## Example: Value Object Definition

```csharp
public record Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Result<Email> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains("@"))
            return Result<Email>.Failure("Invalid email format");

        return Result<Email>.Success(new Email(value));
    }
}
```

## Example: Domain Event

```csharp
public record UserCreatedEvent(int UserId, string Email) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
```

## Testing

Domain Layer tests should focus on:
- Business logic correctness
- Entity behavior and state transitions
- Value Object creation and validation
- Aggregate invariants
- Domain Event generation

Example test:
```csharp
[Fact]
public void CreateUser_WithValidEmail_ShouldSucceed()
{
    // Arrange
    var email = Email.Create("user@example.com").Value;

    // Act
    var user = new User(email);

    // Assert
    Assert.NotNull(user);
    Assert.Equal(email, user.Email);
}
```

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
