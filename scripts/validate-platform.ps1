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
npm run test:baseline
Pop-Location

docker compose build
docker compose up -d
Invoke-WebRequest http://localhost:5000/health/live -UseBasicParsing
Invoke-WebRequest http://localhost:5000/health/ready -UseBasicParsing
Invoke-WebRequest http://localhost:5000/api/platform/status -UseBasicParsing

Push-Location web
$evidencePath = Join-Path ([System.IO.Path]::GetTempPath()) "convolab-cross-capability-evidence.json"
$env:CONVOLAB_EVIDENCE_PATH = $evidencePath
npm run test:cross-capability
npm run test:browser
Pop-Location

docker compose restart db api
$deadline = (Get-Date).AddMinutes(2)
do {
    Start-Sleep -Seconds 2
    try { $ready = Invoke-WebRequest http://localhost:5000/health/ready -UseBasicParsing } catch { $ready = $null }
} until ($ready -or (Get-Date) -gt $deadline)
if (-not $ready) { throw "API did not become ready after the database/API restart." }
Push-Location web
npm run test:restart
npm run test:browser
Pop-Location
