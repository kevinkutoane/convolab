# API Layer

The **API Layer** is the entry point to the application, exposing the business logic through HTTP endpoints. It handles HTTP concerns such as routing, model binding, authentication, and response formatting while delegating business logic to the Application Layer.

## Purpose

The API Layer provides:

- **Endpoints**: HTTP endpoints that expose application functionality
- **Controllers**: Organize endpoints by feature or resource
- **Middleware**: Cross-cutting concerns (logging, error handling, authentication)
- **Filters**: Custom filters for validation, authorization, and exception handling
- **Configuration**: Dependency injection, service registration, and startup configuration
- **Swagger/OpenAPI**: API documentation and schema
- **Health Checks**: Application health monitoring endpoints

## Structure

```
Api/
├── Controllers/        # API endpoint controllers
├── Endpoints/          # Minimal API endpoints (alternative to controllers)
├── Middleware/         # Custom middleware
├── Filters/            # Action filters and result filters
├── Configuration/      # DI and service registration
├── Models/             # API request/response models
├── Program.cs          # Application startup and configuration
└── appsettings.json    # Configuration settings
```

## Key Principles

### 1. Minimal API Endpoints
Use ASP.NET Core Minimal APIs for clean, lightweight endpoint definitions:

```csharp
app.MapGet("/api/users/{id}", GetUserById)
    .WithName("GetUserById")
    .WithOpenApi()
    .Produces<UserDto>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

async Task<IResult> GetUserById(int id, IMediator mediator)
{
    var query = new GetUserByIdQuery(id);
    var result = await mediator.Send(query);
    return result is null ? Results.NotFound() : Results.Ok(result);
}
```

### 2. Dependency Injection
All dependencies are registered in the DI container and injected into endpoints:

```csharp
var builder = WebApplicationBuilder.CreateBuilder(args);

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddMediatR(typeof(Program));
builder.Services.AddSwaggerGen();

var app = builder.Build();
```

### 3. Error Handling
Global exception handling middleware catches and formats errors consistently:

```csharp
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var response = new ErrorResponse(exception?.Message ?? "An error occurred");
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(response);
    });
});
```

### 4. Logging and Tracing
Serilog and OpenTelemetry provide structured logging and distributed tracing:

```csharp
builder.Host.UseSerilog((context, config) =>
    config
        .MinimumLevel.Information()
        .WriteTo.Console()
        .Enrich.FromLogContext());

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());
```

### 5. Health Checks
Health check endpoints monitor application status:

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

app.MapHealthChecks("/health");
```

## Example: Minimal API Endpoint

```csharp
// Define the endpoint
app.MapPost("/api/users", CreateUser)
    .WithName("CreateUser")
    .WithOpenApi()
    .Produces<UserDto>(StatusCodes.Status201Created)
    .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
    .WithSummary("Create a new user")
    .WithDescription("Creates a new user with the provided email and name");

// Implement the handler
async Task<IResult> CreateUser(CreateUserRequest request, IMediator mediator, HttpContext context)
{
    var command = new CreateUserCommand(request.Email, request.Name);
    var result = await mediator.Send(command);
    return Results.Created($"/api/users/{result.Id}", result);
}
```

## Configuration

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=convolab.db"
  },
  "Jwt": {
    "SecretKey": "your-secret-key-here",
    "ExpirationMinutes": 60
  },
  "AllowedHosts": "*"
}
```

## Swagger/OpenAPI

Swagger documentation is automatically generated from endpoint definitions:

```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ConvoLab API",
        Version = "v1",
        Description = "Production-grade API for ConvoLab application"
    });
});

app.UseSwagger();
app.UseSwaggerUI();
```

Access Swagger UI at `/swagger/index.html`

## Testing

API Layer tests should focus on:
- Endpoint routing and HTTP status codes
- Request/response serialization
- Authentication and authorization
- Error handling and validation

Example test:
```csharp
[Fact]
public async Task CreateUser_WithValidRequest_ShouldReturn201()
{
    // Arrange
    var client = new WebApplicationFactory<Program>().CreateClient();
    var request = new CreateUserRequest("user@example.com", "John Doe");

    // Act
    var response = await client.PostAsJsonAsync("/api/users", request);

    // Assert
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
}
```

## Guidelines

1. **Keep Controllers Thin**: Business logic belongs in Application layer
2. **Use Minimal APIs**: Prefer Minimal APIs over traditional controllers for new endpoints
3. **Validate Input**: Use FluentValidation validators for request validation
4. **Consistent Responses**: Use consistent response formats for success and error cases
5. **Document Endpoints**: Add Swagger attributes for API documentation
6. **Handle Exceptions**: Use middleware for global exception handling

## Related Documentation

- See `Application/README.md` for the commands and queries this layer invokes
- See `Infrastructure/README.md` for the services this layer depends on
- See `Docker` section for containerization and deployment
