#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-1.0.0-alpha.17}"
COMMIT_SHA=$(git rev-parse HEAD)
TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
MANIFEST_ID="release-manifest-${VERSION}-${COMMIT_SHA:0:8}"
OUTPUT_DIR="artifacts/release"

mkdir -p "$OUTPUT_DIR"

echo "=== ConvoLab Release Build ==="
echo "Version: $VERSION"
echo "Commit: $COMMIT_SHA"
echo "Manifest: $MANIFEST_ID"

# 1. Build API Container Image
echo "Building API Image..."
docker build -t "convolab-api:${VERSION}" -f src/Api/ConvoLab.Api/Dockerfile .
API_DIGEST=$(docker inspect --format='{{index .RepoDigests 0}}' "convolab-api:${VERSION}" 2>/dev/null || echo "sha256:$(docker images -q --no-trunc convolab-api:${VERSION} | sed 's/sha256://')")

# 2. Build Studio Container Image
echo "Building Studio Image..."
docker build -t "convolab-studio:${VERSION}" -f web/Dockerfile ./web
STUDIO_DIGEST=$(docker inspect --format='{{index .RepoDigests 0}}' "convolab-studio:${VERSION}" 2>/dev/null || echo "sha256:$(docker images -q --no-trunc convolab-studio:${VERSION} | sed 's/sha256://')")

# 3. Generate Release Manifest JSON
MANIFEST_PATH="${OUTPUT_DIR}/manifest.json"
cat <<EOF > "$MANIFEST_PATH"
{
  "releaseManifestId": "${MANIFEST_ID}",
  "releaseVersion": "${VERSION}",
  "sourceCommitSha": "${COMMIT_SHA}",
  "apiImageDigest": "${API_DIGEST}",
  "studioImageDigest": "${STUDIO_DIGEST}",
  "migrationVersion": "202608200002_DeploymentPromotionV1",
  "buildWorkflowId": "${GITHUB_RUN_ID:-local-build}",
  "buildTimestamp": "${TIMESTAMP}",
  "isBackwardCompatible": true,
  "requiresDowntime": false
}
EOF

echo "Release manifest successfully assembled at ${MANIFEST_PATH}"
cat "$MANIFEST_PATH"
