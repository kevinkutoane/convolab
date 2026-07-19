#!/usr/bin/env bash
set -euo pipefail

dotnet restore src/Api/ConvoLab.Api/ConvoLab.Api.csproj
for project in src/tests/*/*.csproj; do dotnet restore "$project"; done

dotnet build src/Api/ConvoLab.Api/ConvoLab.Api.csproj --configuration Release --no-restore
for project in src/tests/*/*.csproj; do dotnet build "$project" --configuration Release --no-restore; done
for project in src/tests/*/*.csproj; do dotnet test "$project" --configuration Release --no-build; done

(
  cd web
  npm ci
  npm run lint
  npm run build
  npm run test -- --run
)

docker compose build
docker compose up -d
curl --fail http://localhost:5000/health/live
curl --fail http://localhost:5000/health/ready
curl --fail http://localhost:5000/api/platform/status
