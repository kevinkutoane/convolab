#!/usr/bin/env bash
set -euo pipefail
# download-ci-artifacts.sh
# Usage: GITHUB_OWNER=kevinkutoane GITHUB_REPO=convolab GITHUB_TOKEN=... ./scripts/ci/download-ci-artifacts.sh <run-id> [output-dir]

if [ "$#" -lt 1 ]; then
  echo "Usage: $0 <run-id> [output-dir]" >&2
  exit 2
fi
RUN_ID="$1"
OUT_DIR="${2:-docs/reports/artifacts}"
mkdir -p "$OUT_DIR"

if command -v gh >/dev/null 2>&1; then
  echo "Using gh to download artifacts for run $RUN_ID"
  gh run download --repo "$GITHUB_OWNER/$GITHUB_REPO" "$RUN_ID" --dir "$OUT_DIR"
  exit 0
fi

if [ -z "${GITHUB_TOKEN:-}" ]; then
  echo "GITHUB_TOKEN must be set in environment when gh is unavailable" >&2
  exit 2
fi

API_URL="https://api.github.com/repos/$GITHUB_OWNER/$GITHUB_REPO/actions/runs/$RUN_ID/artifacts"

echo "Fetching artifact list from $API_URL"
artifacts_json=$(curl -s -H "Authorization: token $GITHUB_TOKEN" "$API_URL")
ids=$(echo "$artifacts_json" | jq -r '.artifacts[] | @base64')
if [ -z "$ids" ]; then
  echo "No artifacts found for run $RUN_ID" >&2
  exit 1
fi
for a in $ids; do
  _jq() { echo "$a" | base64 --decode | jq -r "$1"; }
  id=$(_jq '.id')
  name=$(_jq '.name')
  echo "Downloading artifact $name ($id)"
  dl_url="https://api.github.com/repos/$GITHUB_OWNER/$GITHUB_REPO/actions/artifacts/$id/zip"
  curl -s -L -H "Authorization: token $GITHUB_TOKEN" -o "$OUT_DIR/${name}.zip" "$dl_url"
done

echo "Artifacts downloaded to $OUT_DIR"
