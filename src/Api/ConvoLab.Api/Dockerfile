FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Api/ConvoLab.Api/ConvoLab.Api.csproj", "src/Api/ConvoLab.Api/"]
COPY ["src/Application/ConvoLab.Application/ConvoLab.Application.csproj", "src/Application/ConvoLab.Application/"]
COPY ["src/Domain/ConvoLab.Domain/ConvoLab.Domain.csproj", "src/Domain/ConvoLab.Domain/"]
COPY ["src/Infrastructure/ConvoLab.Infrastructure/ConvoLab.Infrastructure.csproj", "src/Infrastructure/ConvoLab.Infrastructure/"]
RUN dotnet restore "src/Api/ConvoLab.Api/ConvoLab.Api.csproj"

COPY . .
WORKDIR "/src/src/Api/ConvoLab.Api"
RUN dotnet publish "ConvoLab.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates lsb-release gnupg && \
    mkdir -p /etc/apt/keyrings && \
    curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc | gpg --dearmor -o /etc/apt/keyrings/postgresql.gpg && \
    echo "deb [signed-by=/etc/apt/keyrings/postgresql.gpg] http://apt.postgresql.org/pub/repos/apt $(lsb_release -cs)-pgdg main" > /etc/apt/sources.list.d/pgdg.list && \
    apt-get update && apt-get install -y --no-install-recommends postgresql-client-16 && \
    rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ConvoLab.Api.dll"]
