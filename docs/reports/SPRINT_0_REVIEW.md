# Sprint 0: ConvoLab Architecture Foundation

This document outlines the architectural foundation built for ConvoLab during Sprint 0.

## Backend Architecture (.NET 8)
The backend follows **Clean Architecture** principles, divided into four distinct layers:

| Layer | Responsibility | Key Technologies |
|-------|----------------|------------------|
| **Domain** | Enterprise logic, entities, and domain events. | MediatR |
| **Application** | Use cases, request handlers, and interfaces. | MediatR, FluentValidation |
| **Infrastructure** | Data persistence and external services. | EF Core, PostgreSQL, SQLite |
| **Api** | Entry point, controllers, and middleware. | Minimal APIs, Serilog, OpenTelemetry, Swagger |

### Features Implemented:
- **Clean Architecture**: Strict dependency flow (Api -> Infrastructure -> Application -> Domain).
- **MediatR**: Decoupled command/query handling with workflow behaviors for validation.
- **Observability**: Structured logging with Serilog and distributed tracing with OpenTelemetry.
- **Health Checks**: Endpoint at `/health` providing component-level status.
- **Swagger**: API documentation available at `/swagger` in development.

## Frontend Architecture (React)
The frontend is a modern React application built with performance and developer experience in mind.

| Component | Technology |
|-----------|------------|
| **Framework** | React 19 + TypeScript |
| **Build Tool** | Vite |
| **Styling** | Tailwind CSS 4.0 |
| **State Management** | TanStack Query (React Query) |
| **Routing** | React Router |
| **API Client** | Axios |

## DevOps & Monorepo Structure
- **Monorepo**: Unified structure with `/src` (backend), `/web` (frontend), and `/shared`.
- **Docker**: Containerized services for API, Web, and Database.
- **Docker Compose**: Orchestration for local development.
- **CI/CD**: GitHub Actions workflow for automated builds and testing.

## Getting Started
1. **Backend**: `dotnet build src/Api/ConvoLab.Api/ConvoLab.Api.csproj`
2. **Frontend**: `cd web && pnpm install && pnpm run dev`
3. **Docker**: `docker-compose up --build`
