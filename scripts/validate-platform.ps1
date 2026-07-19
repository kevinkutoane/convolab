$ErrorActionPreference = "Stop"

dotnet restore src/Api/ConvoLab.Api/ConvoLab.Api.csproj
Get-ChildItem src/tests/*/*.csproj | ForEach-Object { dotnet restore $_.FullName }
dotnet build src/Api/ConvoLab.Api/ConvoLab.Api.csproj --configuration Release --no-restore
Get-ChildItem src/tests/*/*.csproj | ForEach-Object { dotnet build $_.FullName --configuration Release --no-restore }
Get-ChildItem src/tests/*/*.csproj | ForEach-Object { dotnet test $_.FullName --configuration Release --no-build }

Push-Location web
npm ci
npm run lint
npm run build
npm run test -- --run
Pop-Location

docker compose build
docker compose up -d
Invoke-WebRequest http://localhost:5000/health/live -UseBasicParsing
Invoke-WebRequest http://localhost:5000/health/ready -UseBasicParsing
Invoke-WebRequest http://localhost:5000/api/platform/status -UseBasicParsing
