#!/usr/bin/env bash
set -euo pipefail

node "$(dirname "$0")/check-no-reveal.mjs" "${1:-.}"
