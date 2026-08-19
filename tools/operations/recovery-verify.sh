#!/usr/bin/env bash
set -euo pipefail

# ConvoLab Deep Recovery Verification Tool
# Invokes the canonical IRecoveryVerifier to validate DB, documents, and Data Protection integrity.

API_URL="${CONVOLAB_API_URL:-http://localhost:5000}"
BACKUP_ID="${1:-current}"

echo "Executing deep recovery verification via ${API_URL}..."

RESPONSE=$(curl -sSf -X POST "${API_URL}/api/operations/backups/${BACKUP_ID}/verify")

echo "Verification Response:"
echo "${RESPONSE}"

IS_HEALTHY=$(echo "${RESPONSE}" | grep -o '"isHealthy":true' || true)

if [ -z "${IS_HEALTHY}" ]; then
    echo "FAILED: Recovery verification detected inconsistencies."
    exit 1
fi

echo "SUCCESS: Deep recovery verification passed."
