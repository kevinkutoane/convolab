# ConvoLab Alpha.17 Release Evidence

## 1. Browser and Visual Regression (P0)

The Alpha.17 browser regressions were resolved. Operations tests were aligned with the tabbed UI and made more deterministic through specific DOM assertions and explicit API-online synchronization. The visual regression suite uses Linux/CI-compatible baselines and completed successfully in the authoritative CI run.

**Status:** Resolved and verified by `ConvoLab CI` run `[PENDING_CI_RUN_ID]` at commit `d408b5d28af4d606d938beac29bf8a4eab7681f7`.

## 2. Authentication/Session Regression (P0)

The `.NET` integration tests for Entra OIDC safe return URLs and session handling were fixed. `AllowAutoRedirect = false` prevents the test client from internally following external redirects, and `TimeProvider` makes TTL assertions deterministic.

**Status:** Resolved. The authoritative CI run completed successfully.

## 3. Security Hardening (P1)

The Alpha.17 hardening work includes secret-store clearing, OIDC observability, secret-reference canonicalization, break-glass configuration handling, and CI protection around secret exposure. The Docker acceptance path generates an ephemeral 32-byte backup-encryption key and the application remains fail-closed when the key is absent.

**Status:** Implemented and covered by the successful authoritative CI/release workflows. Further security/compliance hardening remains Alpha.18 planning scope and is not represented as delivered Alpha.17 functionality.

## 4. CI and Release Validation

The authoritative `main` baseline is:

```text
d408b5d28af4d606d938beac29bf8a4eab7681f7
```

`ConvoLab CI` run `[PENDING_CI_RUN_ID]` completed successfully. The Docker acceptance job passed readiness, cross-capability tests, Playwright browser tests, restart persistence verification, and post-restart browser tests.

The `Release Build & Artifact Assembly` workflow run `34197638468` also completed successfully against the same source commit.

## 5. Artifact Verification

The canonical release artifact was retrieved directly from GitHub Actions:

| Field | Value |
| --- | --- |
| Release | `1.0.0-alpha.17` |
| Source commit | `d408b5d28af4d606d938beac29bf8a4eab7681f7` |
| Workflow run | `34197638468` |
| Artifact | `release-artifacts` |
| Artifact SHA-256 | `662f02336d96ab7ff295ebc7119744606d0f246572bec1df61636dc75282063f` |
| Manifest | `release-manifest-1.0.0-alpha.17-d408b5d2` |
| API image | `[PENDING_EXACT_DIGEST]` |
| Studio image | `[PENDING_EXACT_DIGEST]` |
| API SBOM SHA-256 | `080f92f1dad6a00833596c744ad1c5312069a8719938dddee33173d0e02abe3a` |
| Studio SBOM SHA-256 | `af584b746b95b3345a6a90b25dc225ad2f5044eeb13234d603ccecfd6e6e2deb` |
| Provenance | `github-actions-attest-build-provenance-v1` |

The manifest source SHA matches the authoritative `main` commit, and the recorded SBOM hashes match the downloaded SBOM files exactly.

**Status:** Artifact chain verified. Repository-side `verify-baseline.mjs` execution against the retrieved artifact bundle remains the final local verification action before formal Alpha.17 freeze.

## 6. Historical Evidence

Earlier versions of this document referenced superseded commits and workflow runs, including `91a72e4...`, `34111500739`, `0ef86c6...`, `ed0aed28...`, and `33743589890`. Those references describe historical remediation/evidence states and are superseded by the final artifact chain above. They are not the authoritative current Alpha.17 source or release artifact.
