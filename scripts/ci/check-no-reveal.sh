#!/usr/bin/env bash
set -euo pipefail
# Fail if any source file contains RevealValue(
# Search common source paths; exclude node_modules and .git
matches=$(grep -R --line-number --binary-files=without-match "RevealValue\s*\(" src web tools scripts || true)
if [ -n "$matches" ]; then
  echo "Forbidden RevealValue usage found:" >&2
  echo "$matches" >&2
  exit 2
fi
# no findings
echo "No RevealValue() occurrences found in scanned paths."
