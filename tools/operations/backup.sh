#!/usr/bin/env bash
set -euo pipefail

# ConvoLab Operational Backup Script
# Creates a snapshot of PostgreSQL database, documents, and data protection keys.

BACKUP_DIR="${1:-./data/backups}"
BACKUP_ID="convolab-backup-$(date -u +%Y%m%dT%H%M%SZ)"
TARGET_DIR="${BACKUP_DIR}/${BACKUP_ID}"

mkdir -p "${TARGET_DIR}"

echo "Creating ConvoLab backup at ${TARGET_DIR}..."

# Database Dump
if command -v pg_dump &> /dev/null; then
    pg_dump -Fc "${DATABASE_URL:-postgresql://postgres:postgres@localhost:5432/convolab}" > "${TARGET_DIR}/database.dump"
else
    echo "Warning: pg_dump not found in PATH. Skipping direct pg_dump."
fi

# Documents Archive
if [ -d "./data/knowledge-documents" ]; then
    tar -cf "${TARGET_DIR}/documents.tar" -C "./data/knowledge-documents" .
fi

# Data Protection Keys Archive
if [ -d "./data/keys" ]; then
    tar -cf "${TARGET_DIR}/dataprotection.tar" -C "./data/keys" .
fi

# Checksums
cd "${TARGET_DIR}"
sha256sum * > checksums.sha256 2>/dev/null || shasum -a 256 * > checksums.sha256

echo "Backup complete: ${BACKUP_ID}"
