# Multi-stage build for ConvoLab API
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy solution and project files
COPY ["ConvoLab.sln", "./"]
COPY ["src/Domain/ConvoLab.Domain/ConvoLab.Domain.csproj", "src/Domain/ConvoLab.Domain/"]
COPY ["src/Application/ConvoLab.Application/ConvoLab.Application.csproj", "src/Application/ConvoLab.Application/"]
COPY ["src/Infrastructure/ConvoLab.Infrastructure/ConvoLab.Infrastructure.csproj", "src/Infrastructure/ConvoLab.Infrastructure/"]
COPY ["src/Api/ConvoLab.Api/ConvoLab.Api.csproj", "src/Api/ConvoLab.Api/"]

# Restore dependencies
RUN dotnet restore "ConvoLab.sln"

# Copy source code
COPY . .

# Build the solution
RUN dotnet build "ConvoLab.sln" -c Release -o /app/build

# Publish
RUN dotnet publish "src/Api/ConvoLab.Api/ConvoLab.Api.csproj" -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=build /app/publish .

# Create logs directory
RUN mkdir -p /app/logs

# Expose port
EXPOSE 8080

# Set environment
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Run the application
ENTRYPOINT ["dotnet", "ConvoLab.Api.dll"]
