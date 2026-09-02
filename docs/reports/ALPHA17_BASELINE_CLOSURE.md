# Alpha.17 Baseline Closure Report

## Baseline

**Authoritative baseline:** `b3f6bfe4c6be7fd47ec407d95a0ce7ba1bb22242`

The repository was fetched from `origin/main` and verified at the authoritative current baseline. The earlier commit `5bf6d929f25ece38f5ba3e35d914816904a3d863` is historical context only and was not used as the active source of truth.

## Git state

| Item | Result |
| --- | --- |
| Branch | `main` |
| SHA | `b3f6bfe4c6be7fd47ec407d95a0ce7ba1bb22242` |
| Worktree before closure changes | Clean |
| Worktree after closure changes | Contains only the documented roadmap, artifact-verification, and closure/planning report changes |
| Relationship to original alpha.17 finalization | Seven post-finalization commits are present, ending in merge commit `b3f6bfe` |

### Seven post-alpha.17 commits

| Commit | Classification | Change |
| --- | --- | --- |
| `1de66f9` | Security / CI | Added the `RevealValue()` guard workflow and helper script |
| `8a5e26a` | CI/CD / Documentation | Added CI artifact download helper and verification template |
| `6d7ba93` | CI/CD / Security | Generated an ephemeral 32-byte backup encryption key for Docker acceptance |
| `ae4cc1d` | Integration | Merged the security/CI checks branch |
| `ebd0ec7` | Security / Testing / Documentation | Scoped the reveal guard to unsafe exposure, added JavaScript guard tests, and recorded release-alignment evidence |
| `85013c7` | Security / Configuration / Testing | Updated environment settings, Docker configuration, reveal scanning, bootstrap handling, and required-secret tests |
| `b3f6bfe` | Integration | Merged the final security/CI checks branch into `main` |

No post-alpha.17 commit introduced a new product capability, AI provider, or architecture rewrite.

## CI

### Implemented

The acceptance job in `.github/workflows/ci.yml` generates an ephemeral key with `openssl rand -base64 32`, exports it through `GITHUB_ENV`, and checks that decoding yields exactly 32 bytes. The key is not committed and is not presented as a production secret. Docker Compose receives it through environment interpolation. `BackupKeyProvider` remains fail-closed when the key is absent.

The repository also includes the `RevealValue()` guard workflow and its JavaScript test fixtures. The guard is intentionally scoped to unsafe exposure patterns rather than rejecting every legitimate secret-resolution call.

### Verified locally

The following checks were executed in the available environment:

| Check | Result |
| --- | --- |
| `sh scripts/ci/check-no-reveal.sh` | Passed |
| `node scripts/ci/test-check-no-reveal.mjs` | Passed |
| Frontend `npm ci --ignore-scripts` | Passed |
| Frontend `npm run build` | Passed; bundle budgets passed |
| Frontend `npm audit --omit=dev --audit-level=high` | Passed; zero reported vulnerabilities |
| Repository hygiene scan | No tracked `data/backups` files; ignore rule active |

### Not verified

No successful GitHub Actions run associated with `b3f6bfe4c6be7fd47ec407d95a0ce7ba1bb22242` was independently available during this task. Therefore Docker acceptance success, release artifact assembly, image digests, SBOMs, Trivy results, provenance, and manifest verification remain **NOT VERIFIED**. The .NET SDK and Docker executable were unavailable locally, so .NET tests, compilation, and container acceptance could not be run here.

## Release evidence

The release workflow configuration contains the intended image build, digest capture, CycloneDX SBOM generation, Trivy scanning, provenance attestation, release manifest, and artifact upload stages. Configuration alone does not prove execution. `docs/reports/ARTIFACT_VERIFICATION.md` now explicitly records the unavailable evidence instead of using unchecked placeholders.

The required next evidence is one successful authoritative workflow run for the current SHA, followed by retrieval and verification of the manifest, API and Studio digests, SBOM hashes, scan result, provenance reference, and verifier output.

## Security baseline status

| Finding | Original status | Current status | Evidence | Remaining work |
| --- | --- | --- | --- | --- |
| `RevealValue()` exposure | Open / guard recommended | **Partially fixed** | Guard workflow, scoped checker, and fixtures exist | Keep allow-list narrow; prove CI run on current SHA |
| Plaintext secret caching | Medium | **Still open; alpha.18 planning** | Secret store retains configurable in-memory cache | Add conservative TTL/no-cache controls and tests; not implemented in baseline closure |
| OIDC token-exchange logging | Low–Medium | **Needs verification** | Secret is resolved at exchange time and token persistence is disabled | Add failure-path log assertions in alpha.18; no evidence of a successful CI run here |
| Secret-reference canonicalization | Low | **Needs verification** | Existing canonicalization and tests are present | Add edge-case normalization tests in alpha.18 |
| Telemetry leakage | Medium | **Partially fixed** | Secret-carrying HTTP requests suppress automatic instrumentation | Add deny-list and failure-path assertions in alpha.18 |
| Break-glass controls | Process risk | **Implemented; operational verification pending** | Configuration and startup checks exist | Complete rotation and audit runbook in alpha.18 |
| Production-readiness validation | Positive control | **Implemented; CI evidence pending** | Validator and tests exist | Prove current-baseline execution in CI |
| Backup secret handling | CI blocker | **Implemented in code; not verified in a successful run** | Ephemeral 32-byte key generation in CI; fail-closed provider | Run Docker acceptance on current SHA |
| Placeholder/example credentials | Operational risk | **Partially fixed** | CI guard and required-secret tests exist; local Compose still has development defaults | Keep defaults isolated to local development and add deployment-policy enforcement in alpha.18 |
| Resource-level workspace isolation | High from prior integration audit | **Still open; alpha.18 planning** | Global routes and object-only repository query paths require dedicated isolation tests | Implement only after baseline freeze under alpha.18 scope |
| Plugin SSRF redirect/rebinding protection | Medium–High conditional risk | **Still open; alpha.18 planning** | Pre-DNS checks exist; redirect/connect-time validation requires proof | Add strict redirect and destination validation tests |

## Documentation

The roadmap was minimally aligned to identify alpha.17 baseline closure, distinguish environment-dependent and pending evidence, and establish alpha.18 as planning-only. The artifact-verification document now names the current SHA and records all unavailable release evidence as **NOT VERIFIED**.

## Tests and blockers

The available frontend and CI guard checks passed. .NET and Docker validation were blocked by missing local tooling. The only authoritative release blocker that cannot be closed from repository inspection is the absence of a successful current-SHA GitHub Actions run and its retained artifact bundle.

## Freeze decision

### RED

The repository is not ready to be declared `ALPHA.17 HARDENED BASELINE — VERIFIED` because material security-isolation findings remain open and current-SHA Docker/release evidence has not been independently verified. The baseline is structurally improved and the CI backup-key fix is implemented, but it must remain **RED** until the current release evidence is produced and the remaining security work is addressed under the explicitly permitted alpha.18 hardening scope.
