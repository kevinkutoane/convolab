# Infrastructure Layer

The **Infrastructure Layer** handles all external concerns including data persistence, external services, logging, and other cross-cutting concerns. It implements the interfaces defined by the Application and Domain layers.

## Purpose

The Infrastructure Layer provides:

- **Database Context**: Entity Framework Core DbContext for data persistence
- **Repositories**: Implementation of repository interfaces for data access
- **Entity Configurations**: EF Core entity mappings and configurations
- **Migrations**: Database schema migrations
- **Services**: Implementation of external service interfaces
- **Logging**: Serilog configuration and structured logging
- **Caching**: Caching mechanisms and strategies
- **Configuration**: Application configuration and settings

## Structure

```
Infrastructure/
├── Data/
│   ├── Context/        # Entity Framework DbContext
│   ├── Repositories/   # Repository implementations
│   └── Configurations/ # Entity configurations
├── Services/           # External service implementations
├── Logging/            # Logging configuration
├── Caching/            # Caching implementations
└── Configuration/      # Infrastructure setup
```

## Key Principles

### 1. Dependency Inversion
Infrastructure implements interfaces defined in Application and Domain layers. The application never directly depends on infrastructure.

### 2. Repository Pattern
Data access is abstracted through repositories, allowing easy switching between data sources.

### 3. Entity Framework Core
- Supports multiple databases (SQLite for development, PostgreSQL for production)
- Uses migrations for schema versioning
- Configured through fluent API in entity configurations

### 4. Separation of Concerns
Each infrastructure concern (data, logging, caching) is isolated in its own module.

## Database Configuration

### Development (SQLite)

```csharp
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=convolab.db"));
```

### Production (PostgreSQL)

```csharp
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
```

## Example: Repository Implementation

```csharp
public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Users.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await _context.Users.AddAsync(user, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
```

## Example: Entity Configuration

```csharp
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email)
            .HasConversion(x => x.Value)
            .IsRequired();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValue(DateTime.UtcNow);

        builder.HasIndex(x => x.Email)
            .IsUnique();
    }
}
```

## Logging Configuration

Serilog is configured to provide structured logging across all layers:

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/convolab-.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ConvoLab")
    .CreateLogger();
```

## Testing

Infrastructure Layer tests should focus on:
- Repository data access logic
- Entity Framework mappings
- Database migrations
- External service integrations

Example test:
```csharp
[Fact]
public async Task AddAsync_WithValidUser_ShouldPersistToDatabase()
{
    // Arrange
    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase("test-db")
        .Options;

    using (var context = new ApplicationDbContext(options))
    {
        var repository = new UserRepository(context);
        var user = new User(Email.Create("test@example.com").Value, "Test User");

        // Act
        await repository.AddAsync(user, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        var savedUser = await context.Users.FirstOrDefaultAsync();
        Assert.NotNull(savedUser);
        Assert.Equal(user.Email, savedUser.Email);
    }
}
```

## Guidelines

1. **Use Repositories**: Never access DbContext directly from Application layer
2. **Entity Configurations**: Keep entity mappings in separate configuration classes
3. **Migrations**: Create migrations for schema changes, never modify the database directly
4. **Async Operations**: Use async methods for all I/O operations
5. **Error Handling**: Wrap database exceptions in meaningful application exceptions

## Related Documentation

- See `Application/README.md` for the interfaces this layer implements
- See `Api/README.md` for how infrastructure is registered in dependency injection
- See `Docker` section for database setup in containers
