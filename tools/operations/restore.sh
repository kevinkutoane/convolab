#!/usr/bin/env bash
set -euo pipefail

# ConvoLab Operational Restore Script
# Restores state into a clean or designated database target.

BACKUP_PATH="${1:-}"
ALLOW_DESTRUCTIVE="${2:-false}"

if [ -z "${BACKUP_PATH}" ] || [ ! -d "${BACKUP_PATH}" ]; then
    echo "Error: Backup path does not exist or was not specified."
    echo "Usage: ./restore.sh <path-to-backup-dir> [--allow-destructive]"
    exit 1
fi

if [ "${ALLOW_DESTRUCTIVE}" != "--allow-destructive" ]; then
    echo "Safety check failed: You must specify --allow-destructive to perform a database and storage restore."
    exit 1
fi

echo "Verifying checksums..."
cd "${BACKUP_PATH}"
if command -v sha256sum &> /dev/null; then
    sha256sum -c checksums.sha256
else
    shasum -a 256 -c checksums.sha256
fi

echo "Restoring database..."
if [ -f "database.dump" ] && command -v pg_restore &> /dev/null; then
    pg_restore -c -d "${DATABASE_URL:-postgresql://postgres:postgres@localhost:5432/convolab}" "database.dump" || true
fi

echo "Restoring documents..."
if [ -f "documents.tar" ]; then
    mkdir -p "../../data/knowledge-documents"
    tar -xf "documents.tar" -C "../../data/knowledge-documents"
fi

echo "Restoring data protection keys..."
if [ -f "dataprotection.tar" ]; then
    mkdir -p "../../data/keys"
    tar -xf "dataprotection.tar" -C "../../data/keys"
fi

echo "Restore operation complete."
