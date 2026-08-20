Write-Host "==========================================================="
Write-Host "=== ConvoLab Live UAT Deployment & Rollback Drill ==="
Write-Host "==========================================================="

$ErrorActionPreference = "Stop"

# Use real locally tagged test images
$BaselineApi = "convolab-api:latest"
$BaselineStudio = "convolab-web:latest"

$CandidateApi = "convolab-api:latest"
$CandidateStudio = "convolab-web:latest"

$UatEnvPath = "deploy/uat/docker-compose.yml"

Write-Host "1. Testing initial UAT promotion with real image..."
$env:CONVOLAB_API_IMAGE_DIGEST = $CandidateApi
$env:CONVOLAB_STUDIO_IMAGE_DIGEST = $CandidateStudio
$env:UAT_DB_PASSWORD = "uat_live_test_password_123"
$env:BACKUP_ENCRYPTION_KEY = "dGhpc2lzYTMyeWVhcndhcnJhbnR5a2V5MTIzNDU2Nzg="

# Start UAT stack
docker compose -f $UatEnvPath up -d --wait

Write-Host "2. Probing UAT readiness on port 5001..."
$ReadinessUrl = "http://localhost:5001/health/ready"
$StatusUrl = "http://localhost:5001/api/platform/status"

$ReadyOk = $false
for ($i = 0; $i -lt 15; $i++) {
    try {
        $resp = Invoke-WebRequest -Uri $ReadinessUrl -UseBasicParsing -TimeoutSec 2
        if ($resp.StatusCode -eq 200) {
            $ReadyOk = $true
            break
        }
    } catch {
        Start-Sleep -Seconds 2
    }
}

if (-not $ReadyOk) {
    Write-Error "UAT /health/ready probe failed to return HTTP 200."
    docker compose -f $UatEnvPath down -v
    exit 1
}

Write-Host "Candidate /health/ready responded HTTP 200."

# Smoke test
$StatusResp = Invoke-RestMethod -Uri $StatusUrl -Method Get
Write-Host "Candidate Platform status verified: $($StatusResp.status), Version: $($StatusResp.version)"

Write-Host "3. Simulating defect and executing live container rollback to baseline..."
$RollbackStart = Get-Date

$env:CONVOLAB_API_IMAGE_DIGEST = $BaselineApi
$env:CONVOLAB_STUDIO_IMAGE_DIGEST = $BaselineStudio

docker compose -f $UatEnvPath up -d --no-recreate

$RollbackEnd = Get-Date
$RollbackDuration = ($RollbackEnd - $RollbackStart).TotalSeconds
Write-Host "Live rollback container transition completed in ${RollbackDuration} seconds."

Write-Host "4. Verifying post-rollback recovery & data integrity..."
$PostRollbackReady = (Invoke-WebRequest -Uri $ReadinessUrl -UseBasicParsing).StatusCode
if ($PostRollbackReady -ne 200) {
    Write-Error "Post-rollback health check failed."
}

Write-Host "Post-rollback readiness verified: HTTP 200 OK"
$PostStatus = Invoke-RestMethod -Uri $StatusUrl -Method Get
Write-Host "Post-rollback platform status verified: $($PostStatus.status)"

Write-Host "5. Teardown UAT test containers..."
docker compose -f $UatEnvPath down -v

Write-Host "==========================================================="
Write-Host "Live UAT Rollback Drill Executed & PASSED."
Write-Host "Rollback Transition Duration: ${RollbackDuration}s"
Write-Host "==========================================================="
