#!/usr/bin/env bash
set -euo pipefail

root="${1:-.}"
pattern='([[:alnum:]_$]+\.)?RevealValue[[:space:]]*\('
allowed_paths=(
  "src/Application/ConvoLab.Application/Settings/SettingsContracts.cs"
  "src/Api/ConvoLab.Api/Security/EntraAuthentication.cs"
  "src/Infrastructure/ConvoLab.Infrastructure/Settings/CompositeSecretStore.cs"
  "src/Infrastructure/ConvoLab.Infrastructure/Settings/ProviderValidationService.cs"
  "src/Infrastructure/ConvoLab.Infrastructure/Intelligence/GeminiIntelligenceExecutor.cs"
  "src/Infrastructure/ConvoLab.Infrastructure/Operations/Backups/BackupKeyProvider.cs"
  "src/tests/ConvoLab.Infrastructure.IntegrationTests/Settings/OperationalSecretStoreTests.cs"
)

scan_path() {
  local target="$1"
  local matches

  if [[ ! -e "$target" ]]; then
    return 1
  fi

  matches=$(grep -RInE \
    --include='*.cs' \
    --include='*.js' \
    --include='*.mjs' \
    --include='*.ts' \
    --include='*.tsx' \
    --include='*.sh' \
    --include='*.ps1' \
    --exclude-dir='.git' \
    --exclude-dir='bin' \
    --exclude-dir='obj' \
    --exclude-dir='dist' \
    --exclude-dir='node_modules' \
    --exclude='check-no-reveal.sh' \
    --exclude='check-no-reveal.mjs' \
    --exclude='test-check-no-reveal.mjs' \
    "$pattern" "$target" || true)

  if [[ -n "$matches" ]]; then
    for excluded in "${allowed_paths[@]}"; do
      matches=$(printf '%s\n' "$matches" | grep -vF "${excluded}:" || true)
    done
  fi

  if [[ -n "$matches" ]]; then
    printf '%s\n' "$matches" >&2
    return 0
  fi

  return 1
}

found=0
for target in \
  "$root/src/Application" \
  "$root/src/Domain" \
  "$root/src/Infrastructure" \
  "$root/src/Api" \
  "$root/web/src" \
  "$root/tools/release"
do
  if scan_path "$target"; then
    found=1
    break
  fi
done

if [[ "$found" -eq 1 ]]; then
  echo "Unsafe RevealValue usage detected in application source paths." >&2
  exit 1
fi

node "$(dirname "$0")/check-no-reveal.mjs" "$root"
