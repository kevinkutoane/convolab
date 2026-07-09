FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Api/ConvoLab.Api/ConvoLab.Api.csproj", "src/Api/ConvoLab.Api/"]
COPY ["src/Application/ConvoLab.Application/ConvoLab.Application.csproj", "src/Application/ConvoLab.Application/"]
COPY ["src/Domain/ConvoLab.Domain/ConvoLab.Domain.csproj", "src/Domain/ConvoLab.Domain/"]
COPY ["src/Infrastructure/ConvoLab.Infrastructure/ConvoLab.Infrastructure.csproj", "src/Infrastructure/ConvoLab.Infrastructure/"]
RUN dotnet restore "src/Api/ConvoLab.Api/ConvoLab.Api.csproj"
COPY . .
WORKDIR "/src/src/Api/ConvoLab.Api"
RUN dotnet build "ConvoLab.Api.csproj" -c Release -o /app/build
FROM build AS publish
RUN dotnet publish "ConvoLab.Api.csproj" -c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConvoLab.Api.dll"]
