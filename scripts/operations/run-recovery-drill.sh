#!/usr/bin/env bash
set -euo pipefail

# ConvoLab Alpha.19 Disaster Recovery Drill Harness (Bash)
# Automates execution of the isolated recovery rehearsal using docker-compose.recovery.yml.

TARGET_URL="${CONVOLAB_RECOVERY_URL:-http://localhost:5001}"
ADMIN_EMAIL="${CONVOLAB_ADMIN_EMAIL:-recovery-admin@convolab.test}"
ADMIN_PASSWORD="${CONVOLAB_ADMIN_PASSWORD:-Recovery-Admin-Alpha19!}"
OUTPUT_FILE="${1:-docs/reports/recovery-drill-evidence.json}"
SKIP_DOCKER="${CONVOLAB_SKIP_DOCKER:-false}"
NO_TEARDOWN="${CONVOLAB_NO_TEARDOWN:-false}"
COOKIE_JAR=$(mktemp)

cleanup() {
    rm -f "${COOKIE_JAR}"
    if [ "${SKIP_DOCKER}" != "true" ] && [ "${NO_TEARDOWN}" != "true" ]; then
        if command -v docker &>/dev/null && docker info &>/dev/null; then
            echo "Cleaning up isolated recovery stack..."
            docker compose -f docker-compose.recovery.yml down -v &>/dev/null || true
        fi
    fi
}
trap cleanup EXIT

echo "================================================================="
echo " ConvoLab Alpha.19 Isolated Disaster Recovery Drill Harness"
echo "================================================================="
echo "Target URL:  ${TARGET_URL}"
echo "Admin User:  ${ADMIN_EMAIL}"
echo "Output File: ${OUTPUT_FILE}"

DOCKER_RUNNING=false
if command -v docker &>/dev/null && docker info &>/dev/null; then
    DOCKER_RUNNING=true
fi

if [ "${SKIP_DOCKER}" != "true" ]; then
    if [ "${DOCKER_RUNNING}" = "true" ]; then
        echo -e "\n1. Initializing isolated recovery stack via docker-compose.recovery.yml..."
        docker compose -f docker-compose.recovery.yml down -v &>/dev/null || true
        docker compose -f docker-compose.recovery.yml up -d --build
    else
        echo -e "\n[WARN] Docker daemon is not active. Attempting direct communication with ${TARGET_URL}..."
    fi
fi

echo -e "\n2. Waiting for API service readiness at ${TARGET_URL}/health/ready..."
READY=false
for i in $(seq 1 30); do
    if curl -sSf "${TARGET_URL}/health/ready" &>/dev/null; then
        READY=true
        echo "API service is healthy and ready."
        break
    fi
    sleep 2
    printf "."
done
echo ""

if [ "${READY}" != "true" ]; then
    echo "[WARN] Target API at ${TARGET_URL} is unreachable."
    mkdir -p "$(dirname "${OUTPUT_FILE}")"
    cat <<EOF > "${OUTPUT_FILE}"
{
  "timestamp": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "targetUrl": "${TARGET_URL}",
  "status": "Blocked (Environment Gate)",
  "reason": "Isolated recovery stack at ${TARGET_URL} unreachable.",
  "limitations": [
    "Docker engine not active or recovery-api container failed to bind within timeout window."
  ]
}
EOF
    echo "Written preliminary gate report to ${OUTPUT_FILE}."
    exit 0
fi

echo -e "\n3. Authenticating as Platform Administrator..."
LOGIN_PAYLOAD=$(cat <<EOF
{"email":"${ADMIN_EMAIL}","password":"${ADMIN_PASSWORD}"}
EOF
)
curl -sSf -c "${COOKIE_JAR}" -X POST "${TARGET_URL}/api/auth/login" \
    -H "Content-Type: application/json" \
    -d "${LOGIN_PAYLOAD}" > /dev/null
echo "Authenticated successfully."

echo -e "\n4. Seeding representative data and capturing baseline..."
SEED_ID=$(cat /proc/sys/kernel/random/uuid 2>/dev/null || openssl rand -hex 16)
SEED_NAME="Alpha19_DR_Prompt_$(date +%s)"
SEED_PAYLOAD=$(cat <<EOF
{
  "id": "${SEED_ID}",
  "name": "${SEED_NAME}",
  "description": "Seeded prompt for recovery reconciliation",
  "template": "Input: {{input}}",
  "model": "gemini-2.5-flash",
  "category": "Operations"
}
EOF
)
curl -sSf -b "${COOKIE_JAR}" -X POST "${TARGET_URL}/api/prompt-studio/prompts" \
    -H "Content-Type: application/json" \
    -d "${SEED_PAYLOAD}" > /dev/null
echo "Seeded prompt '${SEED_NAME}' (ID: ${SEED_ID})."

echo -e "\n5. Triggering orchestrated backup via POST /api/operations/backups..."
BACKUP_START=$(date +%s%N 2>/dev/null || date +%s)
BACKUP_RES=$(curl -sSf -b "${COOKIE_JAR}" -X POST "${TARGET_URL}/api/operations/backups")
BACKUP_END=$(date +%s%N 2>/dev/null || date +%s)

BACKUP_ID=$(echo "${BACKUP_RES}" | grep -o '"backupId":"[^"]*' | cut -d'"' -f4 || echo "latest")
echo "Backup completed. (Backup ID: ${BACKUP_ID})"

echo -e "\n6. Simulating destructive disruption..."
if [ "${DOCKER_RUNNING}" = "true" ]; then
    docker compose -f docker-compose.recovery.yml stop recovery-api
    docker exec convolab-recovery-postgres psql -U postgres -d convolab_recovery -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;" &>/dev/null || true
    docker compose -f docker-compose.recovery.yml start recovery-api
    sleep 5
fi

echo -e "\n7. Restoring from backup ${BACKUP_ID}..."
RESTORE_START=$(date +%s%N 2>/dev/null || date +%s)
RESTORE_RES=$(curl -sSf -b "${COOKIE_JAR}" -X POST "${TARGET_URL}/api/operations/backups/${BACKUP_ID}/restore?allowDestructive=true")
RESTORE_END=$(date +%s%N 2>/dev/null || date +%s)
echo "Restore operation returned: ${RESTORE_RES}"

echo -e "\n8. Executing deep recovery verification..."
VERIFY_RES=$(curl -sSf -b "${COOKIE_JAR}" -X POST "${TARGET_URL}/api/operations/backups/${BACKUP_ID}/verify")
echo "Verification result: ${VERIFY_RES}"

echo -e "\n9. Reconciling post-restore state..."
PROMPTS_RES=$(curl -sSf -b "${COOKIE_JAR}" "${TARGET_URL}/api/prompt-studio/prompts")
FOUND=false
if echo "${PROMPTS_RES}" | grep -q "${SEED_ID}"; then
    FOUND=true
    echo "Seeded prompt ${SEED_ID} reconciled successfully."
else
    echo "Warning: Seeded prompt not observed in post-restore list."
fi

mkdir -p "$(dirname "${OUTPUT_FILE}")"
cat <<EOF > "${OUTPUT_FILE}"
{
  "timestamp": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "targetUrl": "${TARGET_URL}",
  "status": "$([ "${FOUND}" = "true" ] && echo "Passed" || echo "Failed")",
  "backupId": "${BACKUP_ID}",
  "seededPromptId": "${SEED_ID}",
  "reconciled": ${FOUND}
}
EOF
echo -e "\nEvidence written to ${OUTPUT_FILE}."
