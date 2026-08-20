#!/usr/bin/env bash
set -euo pipefail

MANIFEST_FILE="${1:-artifacts/release/manifest.json}"
TARGET_ENV="${2:-UAT}"

if [ ! -f "$MANIFEST_FILE" ]; then
  echo "Error: Release manifest $MANIFEST_FILE does not exist."
  exit 1
fi

echo "=== ConvoLab Environment Promotion ==="
echo "Target Environment: $TARGET_ENV"
echo "Reading Manifest: $MANIFEST_FILE"

API_DIGEST=$(grep -o '"apiImageDigest": *"[^"]*"' "$MANIFEST_FILE" | cut -d'"' -f4)
STUDIO_DIGEST=$(grep -o '"studioImageDigest": *"[^"]*"' "$MANIFEST_FILE" | cut -d'"' -f4)
VERSION=$(grep -o '"releaseVersion": *"[^"]*"' "$MANIFEST_FILE" | cut -d'"' -f4)
MANIFEST_ID=$(grep -o '"releaseManifestId": *"[^"]*"' "$MANIFEST_FILE" | cut -d'"' -f4)

echo "Promoting API: $API_DIGEST"
echo "Promoting Studio: $STUDIO_DIGEST"

if [ "$TARGET_ENV" = "Production" ]; then
  echo "Executing Pre-Migration Backup Verification on Production..."
  # Invoke backup trigger via API
  curl -fsSL -X POST http://localhost:5000/api/operations/backups || {
    echo "Error: Pre-migration backup failed. Production deployment aborted."
    exit 1
  }
fi

export CONVOLAB_API_IMAGE_DIGEST="$API_DIGEST"
export CONVOLAB_STUDIO_IMAGE_DIGEST="$STUDIO_DIGEST"

if [ "$TARGET_ENV" = "UAT" ]; then
  docker compose -f deploy/uat/docker-compose.yml up -d
elif [ "$TARGET_ENV" = "Production" ]; then
  docker compose -f deploy/production/docker-compose.yml up -d
fi

echo "Promotion complete. Probing readiness..."
curl -fsSL http://localhost:5000/health/ready || curl -fsSL http://localhost:5001/health/ready
echo "Readiness confirmed."
