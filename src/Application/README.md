# Application Layer

The **Application Layer** orchestrates the domain logic and bridges the gap between the API/UI and the Domain Layer. It implements the use cases of the application using the CQRS (Command Query Responsibility Segregation) pattern with MediatR.

## Purpose

The Application Layer provides:

- **Commands**: Operations that modify state (Create, Update, Delete)
- **Queries**: Operations that retrieve data without modifying state
- **Command/Query Handlers**: Implementation of business use cases
- **DTOs (Data Transfer Objects)**: Contracts for data transfer between layers
- **Validators**: Input validation using FluentValidation
- **Exceptions**: Application-specific exceptions
- **Interfaces**: Contracts for repositories, services, and other infrastructure dependencies

## Structure

```
Application/
├── Commands/           # Command definitions and handlers
├── Queries/            # Query definitions and handlers
├── DTOs/               # Data transfer objects
├── Validators/         # FluentValidation validators
├── Interfaces/         # Repository and service contracts
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

## Example: Command Definition

```csharp
public record CreateUserCommand(string Email, string Name) : IRequest<UserDto>;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MinimumLength(2);
    }
}

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    public CreateUserCommandHandler(IUserRepository userRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _mapper = mapper;
    }

    public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email).Value;
        var user = new User(email, request.Name);

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return _mapper.Map<UserDto>(user);
    }
}
```

## Example: Query Definition

```csharp
public record GetUserByIdQuery(int UserId) : IRequest<UserDto?>;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto?>
{
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    public GetUserByIdQueryHandler(IUserRepository userRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _mapper = mapper;
    }

    public async Task<UserDto?> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        return user is null ? null : _mapper.Map<UserDto>(user);
    }
}
```

## Example: DTO Definition

```csharp
public record UserDto(
    int Id,
    string Email,
    string Name,
    DateTime CreatedAt);
```

## Testing

Application Layer tests should focus on:
- Command/Query handler logic
- Validator rules
- Error handling and exceptions
- Integration with repositories (using mocks)

Example test:
```csharp
[Fact]
public async Task Handle_WithValidCommand_ShouldCreateUser()
{
    // Arrange
    var command = new CreateUserCommand("user@example.com", "John Doe");
    var mockRepository = new Mock<IUserRepository>();
    var mockMapper = new Mock<IMapper>();
    var handler = new CreateUserCommandHandler(mockRepository.Object, mockMapper.Object);

    // Act
    var result = await handler.Handle(command, CancellationToken.None);

    // Assert
    mockRepository.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    Assert.NotNull(result);
}
```

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
