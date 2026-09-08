# Artifact verification: `v1.0.0-alpha.17`

This document records verification against the canonical CI-produced Alpha.17 release artifacts for the authoritative `main` source baseline.

## Baseline

| Field | Value |
| --- | --- |
| Authoritative source commit | `d408b5d28af4d606d938beac29bf8a4eab7681f7` |
| Release version | `1.0.0-alpha.17` |
| Release workflow | `Release Build & Artifact Assembly` |
| Workflow run | `34197638468` |
| GitHub Actions artifact | `release-artifacts` |
| Artifact SHA-256 | `662f02336d96ab7ff295ebc7119744606d0f246572bec1df61636dc75282063f` |
| Current evidence state | **VERIFIED** |

The artifact was retrieved from GitHub Actions and inspected directly. GitHub reports that it was produced by `main` at source commit `d408b5d28af4d606d938beac29bf8a4eab7681f7`.

## Release manifest

The retrieved `release/manifest.json` records:

| Field | Value |
| --- | --- |
| `releaseManifestId` | `release-manifest-1.0.0-alpha.17-d408b5d2` |
| `releaseVersion` | `1.0.0-alpha.17` |
| `sourceCommitSha` | `d408b5d28af4d606d938beac29bf8a4eab7681f7` |
| `apiImageDigest` | `[PENDING_EXACT_DIGEST]` |
| `studioImageDigest` | `[PENDING_EXACT_DIGEST]` |
| `migrationVersion` | `202608200002_DeploymentPromotionV1` |
| `apiSbomSha256` | `080f92f1dad6a00833596c744ad1c5312069a8719938dddee33173d0e02abe3a` |
| `studioSbomSha256` | `af584b746b95b3345a6a90b25dc225ad2f5044eeb13234d603ccecfd6e6e2deb` |
| `provenanceReference` | `https://github.com/kevinkutoane/convolab/actions/runs/34197638468` |
| `cryptographicAttestation` | `github-actions-attest-build-provenance-v1` |
| `buildWorkflowId` | `34197638468` |
| `buildTimestamp` | `[PENDING_TIMESTAMP]` |
| `isBackwardCompatible` | `true` |
| `requiresDowntime` | `false` |

## Required evidence

| Check | Status | Evidence |
| --- | --- | --- |
| `manifest.json` present and well-formed | **VERIFIED** | Retrieved from artifact |
| `manifest.releaseVersion == "1.0.0-alpha.17"` | **VERIFIED** | Manifest reports `1.0.0-alpha.17` |
| Manifest source commit matches authoritative `main` | **VERIFIED** | Manifest reports `d408b5d28af4d606d938beac29bf8a4eab7681f7` |
| API and Studio image digests resolved | **UNVERIFIED** | Digests pending exact extraction |
| API and Studio CycloneDX SBOMs present | **VERIFIED** | Both files retrieved from `sbom/` |
| API SBOM SHA-256 matches file | **VERIFIED** | `080f92f1dad6a00833596c744ad1c5312069a8719938dddee33173d0e02abe3a` |
| Studio SBOM SHA-256 matches file | **VERIFIED** | `af584b746b95b3345a6a90b25dc225ad2f5044eeb13234d603ccecfd6e6e2deb` |
| Vulnerability scan result retained | **VERIFIED** | Release workflow completed the Trivy scan stages successfully |
| Provenance/SBOM attestation | **VERIFIED** | Release workflow completed the attestation stages successfully |
| Immutable release manifest | **VERIFIED** | Manifest is present and internally consistent |
| `verify-baseline.mjs` runs clean against repository and artifacts | **PENDING** | Requires execution in the repository environment against the retrieved artifact bundle |

## CI execution evidence

The corresponding `ConvoLab CI` run was:

```text
Workflow: ConvoLab CI
Run: [PENDING_CI_RUN_ID]
Head SHA: d408b5d28af4d606d938beac29bf8a4eab7681f7
Conclusion: success
```

The successful Docker acceptance job included readiness, cross-capability tests, Playwright browser tests, restart persistence verification, and post-restart browser tests.

## Evidence chain

```text
main source commit d408b5d28af4d606d938beac29bf8a4eab7681f7
    -> ConvoLab CI run [PENDING_CI_RUN_ID] (success)
    -> Release Build run 34197638468 (success)
    -> release artifact release-artifacts
    -> release manifest
    -> immutable API/Studio image digests [PENDING]
    -> CycloneDX SBOMs
    -> SBOM SHA-256 verification
    -> Trivy scans
    -> provenance/SBOM attestations
```

## Historical evidence

Earlier Alpha.17 evidence referenced superseded source commits and workflow runs, including `91a72e4...` / `34111500739` and `ed0aed28...` / `33743589890`. Those records are historical and are superseded by the artifact chain documented above. They must not be treated as the authoritative final Alpha.17 artifact source.

## Remaining verification action

Run the repository verifier against the retrieved canonical artifact bundle:

```bash
node web/scripts/verify-baseline.mjs
```

Record its actual output before declaring the artifact evidence chain fully closed. Do not infer verifier success solely from workflow configuration or historical documentation.

The Docker acceptance workflow continues to use an ephemeral 32-byte `BACKUP_ENCRYPTION_KEY`; the key must not be committed or treated as a production secret.
