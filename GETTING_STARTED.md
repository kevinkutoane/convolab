# Getting Started with ConvoLab

Welcome to ConvoLab! This guide will help you get up and running quickly with the application.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Quick Start](#quick-start)
3. [Project Structure](#project-structure)
4. [First Steps](#first-steps)
5. [Common Tasks](#common-tasks)
6. [Troubleshooting](#troubleshooting)

## Prerequisites

Before you begin, ensure you have the following installed:

### Required

- **[.NET 8 SDK](https://dotnet.microsoft.com/download)** - Backend runtime
- **[Node.js 18+](https://nodejs.org/)** - Frontend runtime
- **[Git](https://git-scm.com/)** - Version control

### Optional (Recommended)

- **[Docker Desktop](https://www.docker.com/products/docker-desktop)** - Containerization
- **[Visual Studio Code](https://code.visualstudio.com/)** - Code editor
- **[Visual Studio 2024](https://visualstudio.microsoft.com/)** - Full IDE for .NET
- **[PostgreSQL 15+](https://www.postgresql.org/)** - Production database

## Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/your-org/convolab.git
cd convolab
```

### 2. Start Backend

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run API (runs on http://localhost:5000)
dotnet run --project src/Api/ConvoLab.Api/ConvoLab.Api.csproj
```

### 3. Start Frontend (in new terminal)

```bash
cd web

# Install dependencies
npm install

# Start development server (runs on http://localhost:3000)
npm run dev
```

### 4. Access the Application

- **Frontend**: http://localhost:3000
- **API**: http://localhost:5000
- **Swagger UI**: http://localhost:5000/swagger

## Project Structure

```
convolab/
├── src/                          # Backend .NET solution
│   ├── Api/                      # ASP.NET Core API
│   ├── Application/              # Application layer (CQRS)
│   ├── Domain/                   # Domain layer (business logic)
│   └── Infrastructure/           # Infrastructure layer (data access)
├── web/                          # React TypeScript frontend
├── shared/                       # Shared code and constants
├── docker-compose.yml            # Local development with Docker
├── ARCHITECTURE.md               # Architecture documentation
├── DEPLOYMENT.md                 # Deployment guide
├── CONTRIBUTING.md               # Contribution guidelines
└── README.md                     # Project overview
```

## First Steps

### Understanding the Architecture

1. **Read the Architecture Overview**
   ```bash
   cat ARCHITECTURE.md
   ```
   This explains the Clean Architecture principles and layer structure.

2. **Explore the Layers**
   - `src/Domain/README.md` - Business logic and entities
   - `src/Application/README.md` - Use cases and handlers
   - `src/Infrastructure/README.md` - Data persistence
   - `src/Api/ConvoLab.Api/README.md` - HTTP endpoints

3. **Review the Frontend**
   ```bash
   cat web/README.md
   ```

### Running with Docker Compose

For a complete local environment with PostgreSQL:

```bash
# Start all services
docker-compose up

# Stop services
docker-compose down

# View logs
docker-compose logs -f api
```

### Accessing the Database

**SQLite (Development)**
```bash
# Database file: convolab.db
sqlite3 convolab.db

# List tables
.tables

# Query users
SELECT * FROM users;
```

**PostgreSQL (Docker)**
```bash
# Connect to database
docker-compose exec postgres psql -U convolab_user -d convolab

# List tables
\dt

# Query users
SELECT * FROM users;
```

## Common Tasks

### Adding a New Feature

Follow this workflow:

1. **Define Domain Model**
   ```bash
   # Create entity in src/Domain/ConvoLab.Domain/Entities/
   # Example: src/Domain/ConvoLab.Domain/Entities/Conversation.cs
   ```

2. **Create Application Layer**
   ```bash
   # Create command in src/Application/ConvoLab.Application/Commands/
   # Create handler in src/Application/ConvoLab.Application/Commands/
   # Create DTO in src/Application/ConvoLab.Application/DTOs/
   ```

3. **Implement Infrastructure**
   ```bash
   # Create repository in src/Infrastructure/ConvoLab.Infrastructure/Data/Repositories/
   # Create entity configuration in src/Infrastructure/ConvoLab.Infrastructure/Data/Configurations/
   ```

4. **Expose API**
   ```bash
   # Create endpoint in src/Api/ConvoLab.Api/Controllers/ or Endpoints/
   ```

5. **Build Frontend**
   ```bash
   # Create components in web/src/components/
   # Create pages in web/src/pages/
   ```

### Running Tests

**Backend Tests**
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test src/Domain/ConvoLab.Domain.Tests/

# Run with coverage
dotnet test /p:CollectCoverage=true
```

**Frontend Tests**
```bash
cd web

# Run tests
npm run test

# Run with coverage
npm run test:coverage

# Watch mode
npm run test:watch
```

### Database Migrations

**Create Migration**
```bash
dotnet ef migrations add MigrationName \
  --project src/Infrastructure/ConvoLab.Infrastructure/ConvoLab.Infrastructure.csproj \
  --startup-project src/Api/ConvoLab.Api/ConvoLab.Api.csproj
```

**Apply Migration**
```bash
dotnet ef database update \
  --project src/Infrastructure/ConvoLab.Infrastructure/ConvoLab.Infrastructure.csproj \
  --startup-project src/Api/ConvoLab.Api/ConvoLab.Api.csproj
```

**Reset Database**
```bash
dotnet ef database drop --force
dotnet ef database update
```

### Code Formatting

**Backend**
```bash
# Format code
dotnet format

# Check formatting
dotnet format --verify-no-changes
```

**Frontend**
```bash
cd web

# Format code
npm run format

# Check formatting
npm run lint
```

### Building for Production

**Backend**
```bash
# Build release
dotnet build -c Release

# Publish
dotnet publish -c Release -o ./publish
```

**Frontend**
```bash
cd web

# Build production bundle
npm run build

# Output in dist/ directory
```

**Docker**
```bash
# Build image
docker build -t convolab:latest .

# Run container
docker run -p 5000:8080 convolab:latest
```

## Troubleshooting

### Backend Issues

**Build Fails**
```bash
# Clean and rebuild
dotnet clean
dotnet build

# Clear NuGet cache
dotnet nuget locals all --clear
```

**Database Connection Error**
```bash
# Check connection string in appsettings.json
cat src/Api/ConvoLab.Api/appsettings.json

# Verify database exists
sqlite3 convolab.db ".tables"
```

**Port Already in Use**
```bash
# Find process using port 5000
lsof -i :5000

# Kill process
kill -9 <PID>

# Or use different port
dotnet run --project src/Api/ConvoLab.Api/ConvoLab.Api.csproj -- --urls "http://localhost:5001"
```

### Frontend Issues

**Dependencies Not Installing**
```bash
# Clear cache and reinstall
rm -rf node_modules package-lock.json
npm install
```

**Port Already in Use**
```bash
# Kill process using port 3000
lsof -i :3000
kill -9 <PID>

# Or use different port in vite.config.ts
```

**Hot Reload Not Working**
```bash
# Restart development server
npm run dev
```

### Docker Issues

**Container Won't Start**
```bash
# Check logs
docker-compose logs api

# Rebuild image
docker-compose up --build

# Remove containers and volumes
docker-compose down -v
docker-compose up
```

**Database Connection Failed**
```bash
# Verify PostgreSQL is running
docker-compose ps

# Check logs
docker-compose logs postgres

# Restart services
docker-compose restart
```

## Next Steps

1. **Explore the Code**
   - Start with `src/Domain/` to understand the business logic
   - Review `src/Application/` to see how use cases are implemented
   - Check `web/src/` for frontend components

2. **Read Documentation**
   - [ARCHITECTURE.md](./ARCHITECTURE.md) - System design
   - [DEPLOYMENT.md](./DEPLOYMENT.md) - Production deployment
   - [CONTRIBUTING.md](./CONTRIBUTING.md) - Contribution guidelines

3. **Make Your First Change**
   - Create a simple feature following the workflow above
   - Write tests for your changes
   - Submit a pull request

4. **Join the Community**
   - Check GitHub Issues for tasks to work on
   - Participate in discussions
   - Help other contributors

## Useful Resources

- [.NET Documentation](https://docs.microsoft.com/dotnet)
- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core)
- [Entity Framework Core](https://docs.microsoft.com/ef/core)
- [React Documentation](https://react.dev)
- [TypeScript Documentation](https://www.typescriptlang.org/docs)
- [Tailwind CSS Documentation](https://tailwindcss.com/docs)

## Getting Help

- **Documentation**: Check README files in each layer
- **Issues**: Search existing GitHub Issues
- **Discussions**: Start a GitHub Discussion
- **Email**: Contact the maintainers

## Tips for Success

1. **Understand the Architecture First**
   - Spend time reading ARCHITECTURE.md
   - Understand why each layer exists

2. **Follow Conventions**
   - Use consistent naming
   - Follow the established patterns
   - Keep code organized

3. **Write Tests**
   - Test your changes
   - Aim for high coverage
   - Test edge cases

4. **Document Your Code**
   - Add comments for complex logic
   - Write clear commit messages
   - Update README files

5. **Ask Questions**
   - Don't hesitate to ask for help
   - Review existing code for examples
   - Discuss design decisions

---

**Happy coding! Welcome to ConvoLab! 🚀**
