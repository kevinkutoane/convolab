# ConvoLab

A **production-grade, elegantly crafted full-stack enterprise application foundation** built with Clean Architecture principles. ConvoLab provides a robust, well-documented starter template for building scalable applications with .NET 10 and React.

## Overview

ConvoLab is a complete monorepo solution featuring:

- **Clean Architecture Backend**: .NET 10 with ASP.NET Core, MediatR, Entity Framework Core, and comprehensive logging
- **Modern React Frontend**: React 19, TypeScript, Vite, Tailwind CSS with React Router and TanStack Query
- **Production-Ready Infrastructure**: Docker, docker-compose, PostgreSQL, SQLite, and CI/CD pipeline
- **Comprehensive Documentation**: Every layer and component is thoroughly documented
- **Enterprise-Grade Features**: Health checks, Swagger/OpenAPI, structured logging, distributed tracing

## Quick Start

### Prerequisites

- **.NET 10 SDK** - [Download](https://dotnet.microsoft.com/download)
- **Node.js 18+** - [Download](https://nodejs.org/)
- **Docker & Docker Compose** - [Download](https://www.docker.com/products/docker-desktop)
- **PostgreSQL 15+** (optional for production)

### Local Development

#### Backend

```bash
# Navigate to project root
cd /home/ubuntu/convolab

# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run tests
dotnet test

# Start API (runs on http://localhost:5000)
dotnet run --project src/Api/ConvoLab.Api/ConvoLab.Api.csproj
```

#### Frontend

```bash
# Navigate to frontend directory
cd web

# Install dependencies
npm install

# Start development server (runs on http://localhost:3000)
npm run dev

# Build for production
npm run build

# Run tests
npm run test
```

#### Using Docker Compose

```bash
# Start all services (API, PostgreSQL)
docker-compose up

# Stop services
docker-compose down

# View logs
docker-compose logs -f
```

## Project Structure

```
ConvoLab/
├── src/                          # Backend .NET solution
│   ├── Api/                      # ASP.NET Core API layer
│   ├── Application/              # Application layer (CQRS)
│   ├── Domain/                   # Domain layer (business logic)
│   └── Infrastructure/           # Infrastructure layer (data, services)
├── web/                          # React TypeScript frontend
│   ├── src/
│   │   ├── components/           # Reusable React components
│   │   ├── pages/                # Page components
│   │   ├── hooks/                # Custom React hooks
│   │   └── lib/                  # Utilities and helpers
│   ├── public/                   # Static assets
│   └── package.json
├── shared/                       # Shared code and constants
├── docker-compose.yml            # Local development orchestration
├── Dockerfile                    # API container image
├── ConvoLab.sln                  # .NET solution file
├── ARCHITECTURE.md               # Architecture documentation
└── README.md                     # This file
```

## Architecture

ConvoLab follows **Clean Architecture** principles with clear separation of concerns:

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

### Key Principles

1. **Dependency Inversion**: Dependencies always point inward toward the domain
2. **Independence of Frameworks**: Business logic is independent of any framework
3. **Testability**: Each layer can be tested in isolation
4. **Business Rule Independence**: Rules are expressed in the domain layer

For detailed architecture information, see [ARCHITECTURE.md](./ARCHITECTURE.md).

## Technology Stack

### Backend

| Technology | Version | Purpose |
|---|---|---|
| .NET | 10.0 | Runtime and framework |
| ASP.NET Core | 10.0 | Web API framework |
| Entity Framework Core | 8.0+ | ORM for data persistence |
| MediatR | Latest | CQRS pattern implementation |
| FluentValidation | Latest | Input validation |
| Serilog | Latest | Structured logging |
| OpenTelemetry | Latest | Distributed tracing |
| PostgreSQL | 15+ | Production database |
| SQLite | Latest | Development database |

### Frontend

| Technology | Version | Purpose |
|---|---|---|
| React | 19+ | UI framework |
| TypeScript | 5.0+ | Type-safe JavaScript |
| Vite | Latest | Build tool and dev server |
| Tailwind CSS | 4.0+ | Utility-first CSS |
| React Router | 6+ | Client-side routing |
| TanStack Query | 5+ | Server state management |
| Axios | Latest | HTTP client |

## Features

### Backend Features

- ✅ **Clean Architecture**: Organized into Domain, Application, Infrastructure, and API layers
- ✅ **CQRS Pattern**: Command Query Responsibility Segregation with MediatR
- ✅ **Entity Framework Core**: Support for PostgreSQL and SQLite
- ✅ **Dependency Injection**: Built-in ASP.NET Core DI container
- ✅ **Input Validation**: FluentValidation for robust input validation
- ✅ **Structured Logging**: Serilog for comprehensive logging
- ✅ **Distributed Tracing**: OpenTelemetry for observability
- ✅ **Health Checks**: ASP.NET Core health check endpoints
- ✅ **Swagger/OpenAPI**: Auto-generated API documentation
- ✅ **Error Handling**: Global exception handling middleware
- ✅ **Configuration Management**: Environment-specific settings

### Frontend Features

- ✅ **React 19**: Latest React with hooks and concurrent features
- ✅ **TypeScript**: Full type safety across the application
- ✅ **Vite**: Lightning-fast development and build experience
- ✅ **Tailwind CSS**: Utility-first CSS framework
- ✅ **React Router**: Client-side routing and navigation
- ✅ **TanStack Query**: Powerful server state management
- ✅ **Axios**: HTTP client with interceptors
- ✅ **Component Library**: Pre-built UI components
- ✅ **Responsive Design**: Mobile-first responsive layouts
- ✅ **Dark Mode Support**: Built-in theme switching

### DevOps Features

- ✅ **Docker**: Containerized API and database
- ✅ **docker-compose**: Local development environment
- ✅ **GitHub Actions**: CI/CD pipeline for build, test, and lint
- ✅ **Database Migrations**: Entity Framework Core migrations
- ✅ **Environment Configuration**: Multi-environment support

## API Documentation

The API includes comprehensive Swagger/OpenAPI documentation:

1. **Start the API**:
   ```bash
   dotnet run --project src/Api/ConvoLab.Api/ConvoLab.Api.csproj
   ```

2. **Access Swagger UI**:
   - Navigate to `http://localhost:5000/swagger`

3. **View API Schema**:
   - JSON schema available at `http://localhost:5000/swagger/v1/swagger.json`

## Health Checks

Monitor application health:

```bash
# Check API health
curl http://localhost:5000/health
```

## Configuration

### Environment Variables

Configuration is managed through:
- `appsettings.json` - Default settings
- `appsettings.Development.json` - Development overrides
- `appsettings.Production.json` - Production overrides
- Environment variables - Runtime overrides

### Database Configuration

**Development (SQLite)**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=convolab.db"
  }
}
```

**Production (PostgreSQL)**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=convolab;User Id=postgres;Password=password;"
  }
}
```

## Database Migrations

### Create a Migration

```bash
dotnet ef migrations add MigrationName \
  --project src/Infrastructure/ConvoLab.Infrastructure/ConvoLab.Infrastructure.csproj \
  --startup-project src/Api/ConvoLab.Api/ConvoLab.Api.csproj
```

### Apply Migrations

```bash
dotnet ef database update \
  --project src/Infrastructure/ConvoLab.Infrastructure/ConvoLab.Infrastructure.csproj \
  --startup-project src/Api/ConvoLab.Api/ConvoLab.Api.csproj
```

## Testing

### Backend Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test src/Domain/ConvoLab.Domain.Tests/

# Run with coverage
dotnet test /p:CollectCoverage=true
```

### Frontend Tests

```bash
# Run tests
npm run test

# Run with coverage
npm run test:coverage

# Watch mode
npm run test:watch
```

## Building for Production

### Backend

```bash
# Build release configuration
dotnet build -c Release

# Publish
dotnet publish -c Release -o ./publish
```

### Frontend

```bash
# Build production bundle
npm run build

# Output in dist/ directory
```

### Docker

```bash
# Build image
docker build -t convolab:latest .

# Run container
docker run -p 5000:8080 convolab:latest
```

## CI/CD Pipeline

GitHub Actions automatically:
1. ✅ Builds the .NET solution
2. ✅ Runs backend tests
3. ✅ Builds the React frontend
4. ✅ Runs frontend tests
5. ✅ Lints code
6. ✅ Builds Docker image

See `.github/workflows/` for pipeline configuration.

## Documentation

Each layer includes comprehensive documentation:

- [ARCHITECTURE.md](./ARCHITECTURE.md) - Overall architecture and design decisions
- [src/Api/README.md](./src/Api/ConvoLab.Api/README.md) - API layer documentation
- [src/Application/README.md](./src/Application/README.md) - Application layer documentation
- [src/Domain/README.md](./src/Domain/README.md) - Domain layer documentation
- [src/Infrastructure/README.md](./src/Infrastructure/README.md) - Infrastructure layer documentation
- [web/README.md](./web/README.md) - Frontend documentation

## Development Guidelines

### Adding a New Feature

1. **Define Domain Model** in `src/Domain/`
2. **Create Application Layer** in `src/Application/` (Commands/Queries)
3. **Implement Infrastructure** in `src/Infrastructure/` (Repositories)
4. **Expose API** in `src/Api/` (Endpoints)
5. **Build Frontend** in `web/` (Components/Pages)
6. **Write Tests** for each layer
7. **Update Documentation**

### Code Style

- **C#**: Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- **TypeScript/React**: Follow [Airbnb JavaScript Style Guide](https://github.com/airbnb/javascript)
- **Formatting**: Use built-in formatters (dotnet format, prettier)

### Commit Messages

Use conventional commits:
```
feat: Add user authentication
fix: Resolve database connection issue
docs: Update architecture documentation
test: Add unit tests for user service
refactor: Simplify error handling
```

## Troubleshooting

### Build Issues

```bash
# Clean build
dotnet clean
dotnet build

# Clear NuGet cache
dotnet nuget locals all --clear
```

### Database Issues

```bash
# Reset database
dotnet ef database drop --force
dotnet ef database update
```

### Port Already in Use

```bash
# Find process using port 5000
lsof -i :5000

# Kill process
kill -9 <PID>
```

## Performance Considerations

- **Database**: Use indexes on frequently queried columns
- **Caching**: Implement caching for read-heavy operations
- **Logging**: Use appropriate log levels to avoid performance impact
- **Async/Await**: Use async patterns for I/O operations
- **Frontend**: Lazy load components and optimize bundle size

## Security Considerations

- **Authentication**: Implement JWT or OAuth2 for API authentication
- **Authorization**: Use role-based access control (RBAC)
- **Input Validation**: Always validate and sanitize user input
- **HTTPS**: Use HTTPS in production
- **Secrets**: Store sensitive data in environment variables or secret managers
- **CORS**: Configure CORS appropriately for your frontend domain

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

For issues, questions, or suggestions:
1. Check existing documentation in [ARCHITECTURE.md](./ARCHITECTURE.md)
2. Review layer-specific README files
3. Check GitHub Issues for similar problems
4. Create a new GitHub Issue with detailed information

## Roadmap

- [ ] Authentication and Authorization
- [ ] Real-time features with SignalR
- [ ] Advanced caching strategies
- [ ] Performance monitoring and analytics
- [ ] GraphQL API support
- [ ] Mobile app with React Native
- [ ] Kubernetes deployment configuration

## Acknowledgments

ConvoLab is built on proven patterns and best practices from the software architecture community:
- Clean Architecture by Robert C. Martin
- Domain-Driven Design by Eric Evans
- CQRS pattern by Greg Young
- ASP.NET Core best practices

---

**Built with ❤️ for scalable, maintainable applications.**
