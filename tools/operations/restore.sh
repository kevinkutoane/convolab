#!/usr/bin/env bash
set -euo pipefail

# ConvoLab Canonical Restore Tool
# Restores backup archives with explicit isolated vs destructive mode enforcement.

BACKUP_ID="${1:-}"
MODE="${2:---isolated}"
API_URL="${CONVOLAB_API_URL:-http://localhost:5000}"

if [ -z "${BACKUP_ID}" ]; then
    echo "Usage: ./restore.sh <backup-id> [--isolated | --allow-destructive]"
    exit 1
fi

echo "Initiating restore for backup ${BACKUP_ID} (Mode: ${MODE})..."

if [ "${MODE}" == "--allow-destructive" ]; then
    echo "WARNING: Executing in DESTRUCTIVE mode. Active database and storage will be overwritten."
    RESPONSE=$(curl -sSf -X POST "${API_URL}/api/operations/backups/${BACKUP_ID}/restore?allowDestructive=true")
elif [ "${MODE}" == "--isolated" ]; then
    echo "Executing in ISOLATED target mode."
    RESPONSE=$(curl -sSf -X POST "${API_URL}/api/operations/backups/${BACKUP_ID}/restore?allowDestructive=false") || {
        echo "Error: Restore rejected by server. To restore over an active environment, specify --allow-destructive."
        exit 1
    }
else
    echo "Invalid mode specified: ${MODE}. Must be --isolated or --allow-destructive."
    exit 1
fi

echo "Restore operation accepted: ${RESPONSE}"
