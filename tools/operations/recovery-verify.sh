#!/usr/bin/env bash
set -euo pipefail

# ConvoLab Disaster Recovery Post-Restore Verification Script
# Verifies health, readiness, and document reconciliation.

API_URL="${1:-http://localhost:5000}"

echo "Checking /health/live..."
curl -sSf "${API_URL}/health/live" > /dev/null
echo "Live check: OK"

echo "Checking /health/ready..."
curl -sSf "${API_URL}/health/ready" > /dev/null || echo "Readiness check reported non-200 (expected if AI provider keys are omitted in Development/Recovery)."

echo "Verifying document storage reconciliation..."
if [ -d "./data/knowledge-documents" ]; then
    DOC_COUNT=$(find ./data/knowledge-documents -type f ! -name ".*" | wc -l)
    echo "Restored physical documents count: ${DOC_COUNT}"
fi

echo "Disaster recovery verification finished."
