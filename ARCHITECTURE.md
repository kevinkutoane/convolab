# ConvoLab Architecture Overview

This document provides a comprehensive overview of the ConvoLab application architecture, design principles, and organizational structure.

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Directory Structure](#directory-structure)
3. [Technology Stack](#technology-stack)
4. [Clean Architecture Principles](#clean-architecture-principles)
5. [Dependency Flow](#dependency-flow)
6. [Data Flow](#data-flow)
7. [Development Workflow](#development-workflow)
8. [Deployment](#deployment)

## Architecture Overview

ConvoLab follows **Clean Architecture** principles with a clear separation of concerns across four distinct layers:

```
┌─────────────────────────────────────────────────────────┐
│                    API Layer                             │
│        (HTTP Endpoints, Controllers, Middleware)         │
└────────────────────────┬────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────┐
│                Application Layer                         │
│         (Commands, Queries, Handlers, DTOs)             │
└────────────────────────┬────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────┐
│                  Domain Layer                            │
│        (Entities, Value Objects, Business Logic)        │
└────────────────────────┬────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────┐
│              Infrastructure Layer                        │
│    (Data Access, External Services, Logging, Config)    │
└─────────────────────────────────────────────────────────┘
```

### Layer Responsibilities

| Layer | Responsibility | Dependencies |
|-------|---|---|
| **API** | HTTP endpoints, routing, request/response handling | Application, Domain |
| **Application** | Use cases, command/query handlers, validation | Domain |
| **Domain** | Business logic, entities, value objects | None (pure .NET) |
| **Infrastructure** | Data persistence, external services, logging | Application, Domain |

## Directory Structure

```
ConvoLab/
├── src/
│   ├── Api/
│   │   └── ConvoLab.Api/
│   │       ├── Controllers/           # API endpoints
│   │       ├── Middleware/            # Cross-cutting concerns
│   │       ├── Configuration/         # DI and startup
│   │       ├── Program.cs             # Entry point
│   │       ├── appsettings.json       # Configuration
│   │       └── README.md              # Layer documentation
│   ├── Application/
│   │   └── ConvoLab.Application/
│   │       ├── Commands/              # Command definitions and handlers
│   │       ├── Queries/               # Query definitions and handlers
│   │       ├── DTOs/                  # Data transfer objects
│   │       ├── Validators/            # FluentValidation validators
│   │       ├── Interfaces/            # Repository contracts
│   │       ├── Exceptions/            # Application exceptions
│   │       └── README.md              # Layer documentation
│   ├── Domain/
│   │   └── ConvoLab.Domain/
│   │       ├── Entities/              # Domain entities
│   │       ├── ValueObjects/          # Value objects
│   │       ├── Events/                # Domain events
│   │       ├── Interfaces/            # Domain contracts
│   │       ├── Exceptions/            # Domain exceptions
│   │       └── README.md              # Layer documentation
│   └── Infrastructure/
│       └── ConvoLab.Infrastructure/
│           ├── Data/
│           │   ├── Context/           # DbContext
│           │   ├── Repositories/      # Repository implementations
│           │   └── Configurations/    # Entity configurations
│           ├── Services/              # External service implementations
│           ├── Logging/               # Logging setup
│           └── README.md              # Layer documentation
├── web/
│   ├── src/                           # React TypeScript frontend
│   ├── public/                        # Static assets
│   └── README.md                      # Frontend documentation
├── shared/
│   ├── Constants/                     # Shared constants
│   ├── Models/                        # Shared DTOs
│   └── README.md                      # Shared documentation
├── docker-compose.yml                 # Local development environment
├── Dockerfile                         # API container image
├── ConvoLab.sln                       # Solution file
├── global.json                        # .NET SDK version
└── ARCHITECTURE.md                    # This file
```

## Technology Stack

### Backend

| Technology | Version | Purpose |
|---|---|---|
| **.NET** | 10.0 | Runtime and framework |
| **ASP.NET Core** | 10.0 | Web API framework |
| **Entity Framework Core** | 8.0+ | ORM for data persistence |
| **MediatR** | Latest | CQRS pattern implementation |
| **FluentValidation** | Latest | Input validation |
| **Serilog** | Latest | Structured logging |
| **OpenTelemetry** | Latest | Distributed tracing |
| **PostgreSQL** | 15+ | Production database |
| **SQLite** | Latest | Development database |
| **Docker** | Latest | Containerization |

### Frontend

| Technology | Version | Purpose |
|---|---|---|
| **React** | 19+ | UI framework |
| **TypeScript** | 5.0+ | Type-safe JavaScript |
| **Vite** | Latest | Build tool and dev server |
| **Tailwind CSS** | 4.0+ | Utility-first CSS |
| **React Router** | 6+ | Client-side routing |
| **TanStack Query** | 5+ | Server state management |
| **Axios** | Latest | HTTP client |

### DevOps

| Technology | Purpose |
|---|---|
| **GitHub Actions** | CI/CD workflow |
| **Docker** | Containerization |
| **docker-compose** | Local development orchestration |

## Clean Architecture Principles

### 1. Dependency Inversion

Dependencies always point inward toward the domain. The outer layers depend on inner layers, never the reverse.

```
API → Application → Domain ← Infrastructure
```

### 2. Independence of Frameworks

The domain and application layers are completely independent of any framework. They can be tested and understood without any framework knowledge.

### 3. Testability

Each layer can be tested in isolation:
- **Domain tests**: Pure business logic, no mocks needed
- **Application tests**: Mock repositories and external services
- **API tests**: Mock mediator and services
- **Integration tests**: Real database and services

### 4. Business Rule Independence

Business rules are expressed in the domain layer and are independent of:
- Database implementation
- Web framework
- UI technology
- External services

## Dependency Flow

### Request Flow (Inbound)

```
HTTP Request
    ↓
API Controller/Endpoint
    ↓
MediatR Handler (Application)
    ↓
Domain Logic (Entities, Value Objects)
    ↓
Repository Interface (Application)
    ↓
Repository Implementation (Infrastructure)
    ↓
Database
```

### Response Flow (Outbound)

```
Database
    ↓
Repository Implementation
    ↓
Domain Entity
    ↓
DTO Mapping (Application)
    ↓
HTTP Response (API)
    ↓
Client
```

## Data Flow

### Command Execution

```
1. Client sends POST request with data
2. API endpoint receives and validates request
3. Creates command from request
4. Sends command through MediatR
5. Handler executes business logic
6. Domain entity is created/updated
7. Repository persists to database
8. Domain events are published
9. Response DTO is returned to client
```

### Query Execution

```
1. Client sends GET request
2. API endpoint receives request
3. Creates query from request parameters
4. Sends query through MediatR
5. Handler retrieves data from repository
6. Maps domain entity to DTO
7. Returns DTO to client
```

## Development Workflow

### Adding a New Feature

1. **Define Domain Model**
   - Create entity in `Domain/Entities/`
   - Add value objects if needed
   - Define domain events

2. **Create Application Layer**
   - Create command/query in `Application/Commands/` or `Application/Queries/`
   - Create validator in `Application/Validators/`
   - Create handler in `Application/Commands/` or `Application/Queries/`
   - Create DTO in `Application/DTOs/`

3. **Implement Infrastructure**
   - Create repository interface in `Application/Interfaces/`
   - Implement repository in `Infrastructure/Data/Repositories/`
   - Create entity configuration in `Infrastructure/Data/Configurations/`
   - Create database migration

4. **Expose API**
   - Create endpoint in `Api/Controllers/` or `Api/Endpoints/`
   - Add Swagger documentation
   - Add input validation

5. **Test**
   - Write unit tests for domain logic
   - Write tests for application handlers
   - Write integration tests for API endpoints

### Database Migrations

```bash
# Create migration
dotnet ef migrations add MigrationName --project src/Infrastructure/ConvoLab.Infrastructure

# Apply migration
dotnet ef database update --project src/Infrastructure/ConvoLab.Infrastructure
```

## Deployment

### Docker

The application is containerized for consistent deployment across environments.

```bash
# Build image
docker build -t convolab:latest .

# Run container
docker run -p 5000:8080 convolab:latest
```

### docker-compose

For local development, use docker-compose to run the API and PostgreSQL:

```bash
docker-compose up
```

### CI/CD Workflow

GitHub Actions automatically:
1. Builds the solution
2. Runs tests
3. Lints code
4. Builds Docker image
5. Pushes to registry (if configured)

## Configuration

### Environment-Specific Settings

Configuration is managed through:
- `appsettings.json` - Default settings
- `appsettings.Development.json` - Development overrides
- `appsettings.Production.json` - Production overrides
- Environment variables - Runtime overrides

### Connection Strings

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=convolab;User Id=postgres;Password=password;"
  }
}
```

## Logging and Monitoring

### Serilog Configuration

Structured logging is configured in `Program.cs`:

```csharp
builder.Host.UseSerilog((context, config) =>
    config
        .MinimumLevel.Information()
        .WriteTo.Console()
        .WriteTo.File("logs/convolab-.txt", rollingInterval: RollingInterval.Day)
        .Enrich.FromLogContext());
```

### OpenTelemetry

Distributed tracing is configured for observability:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddConsoleExporter());
```

## Best Practices

1. **Keep Layers Focused**: Each layer has a single responsibility
2. **Use Interfaces**: Define contracts for dependencies
3. **Validate Input**: Validate at the API and Application layers
4. **Handle Errors**: Use consistent error handling and logging
5. **Write Tests**: Aim for high test coverage
6. **Document Code**: Add XML comments to public APIs
7. **Follow Naming Conventions**: Use clear, descriptive names
8. **Avoid Shortcuts**: Never bypass layers for convenience

## Related Documentation

- See `src/Api/ConvoLab.Api/README.md` for API layer details
- See `src/Application/README.md` for Application layer details
- See `src/Domain/README.md` for Domain layer details
- See `src/Infrastructure/README.md` for Infrastructure layer details
- See `web/README.md` for frontend documentation
