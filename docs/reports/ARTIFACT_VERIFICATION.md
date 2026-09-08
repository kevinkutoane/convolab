# Artifact verification: `v1.0.0-alpha.17`

This document records verification against the canonical CI-produced Alpha.17 release artifacts for the authoritative `main` source baseline.

## Baseline

| Field | Value |
| --- | --- |
| Authoritative source commit | `91a72e4324ed3ce2f861be5a6889ac813627d256` |
| Release version | `1.0.0-alpha.17` |
| Release workflow | `Release Build & Artifact Assembly` |
| Workflow run | `34111500739` |
| GitHub Actions artifact | `release-artifacts` (`10014601379`) |
| Artifact SHA-256 | `3557664545c7f4c1714dfb52783a27ce834bfa7a2ce830e0596142fdc99abde9` |
| Current evidence state | **ARTIFACT VERIFIED; repository verifier execution pending** |

The artifact was retrieved from GitHub Actions and inspected directly. GitHub reports that it was produced by `main` at source commit `91a72e4324ed3ce2f861be5a6889ac813627d256`.

## Release manifest

The retrieved `release/manifest.json` records:

| Field | Value |
| --- | --- |
| `releaseManifestId` | `release-manifest-1.0.0-alpha.17-91a72e43` |
| `releaseVersion` | `1.0.0-alpha.17` |
| `sourceCommitSha` | `91a72e4324ed3ce2f861be5a6889ac813627d256` |
| `apiImageDigest` | `ghcr.io/kevinkutoane/convolab/convolab-api@sha256:a8f683daec33cd7fc97ab020c5d5618b2ce9b03986ec39b14624070c772d62ae` |
| `studioImageDigest` | `ghcr.io/kevinkutoane/convolab/convolab-studio@sha256:21a6445556875fa039e768e4bc010b5b00f0e0894a3e0654bb796c7aa5b9263e` |
| `migrationVersion` | `202608200002_DeploymentPromotionV1` |
| `apiSbomSha256` | `953e9368f1a1798ae03e9aaf4e1d66ba34939277fd6a7bbc43272410e97f2ca1` |
| `studioSbomSha256` | `37a0c46b8422da00f962227f84700b992cecb4508f79274053720607ddb0a99e` |
| `provenanceReference` | `https://github.com/kevinkutoane/convolab/actions/runs/34111500739` |
| `cryptographicAttestation` | `github-actions-attest-build-provenance-v1` |
| `buildWorkflowId` | `34111500739` |
| `buildTimestamp` | `2026-09-07T10:27:33Z` |
| `isBackwardCompatible` | `true` |
| `requiresDowntime` | `false` |

## Required evidence

| Check | Status | Evidence |
| --- | --- | --- |
| `manifest.json` present and well-formed | **VERIFIED** | Retrieved from artifact `10014601379` |
| `manifest.releaseVersion == "1.0.0-alpha.17"` | **VERIFIED** | Manifest reports `1.0.0-alpha.17` |
| Manifest source commit matches authoritative `main` | **VERIFIED** | Manifest reports `91a72e4324ed3ce2f861be5a6889ac813627d256` |
| API and Studio image digests resolved | **VERIFIED** | Full immutable digests recorded above |
| API and Studio CycloneDX SBOMs present | **VERIFIED** | Both files retrieved from `sbom/` |
| API SBOM SHA-256 matches file | **VERIFIED** | `953e9368f1a1798ae03e9aaf4e1d66ba34939277fd6a7bbc43272410e97f2ca1` |
| Studio SBOM SHA-256 matches file | **VERIFIED** | `37a0c46b8422da00f962227f84700b992cecb4508f79274053720607ddb0a99e` |
| Vulnerability scan result retained | **VERIFIED** | Release workflow completed the Trivy scan stages successfully |
| Provenance/SBOM attestation | **VERIFIED** | Release workflow completed the attestation stages successfully |
| Immutable release manifest | **VERIFIED** | Manifest is present and internally consistent |
| `verify-baseline.mjs` runs clean against repository and artifacts | **PENDING** | Requires execution in the repository environment against the retrieved artifact bundle |

## CI execution evidence

The corresponding `ConvoLab CI` run was:

```text
Workflow: ConvoLab CI
Run: #87
Run ID: 34109230326
Head SHA: 91a72e4324ed3ce2f861be5a6889ac813627d256
Conclusion: success
```

The successful Docker acceptance job included readiness, cross-capability tests, Playwright browser tests, restart persistence verification, and post-restart browser tests.

## Evidence chain

```text
main source commit 91a72e4324ed3ce2f861be5a6889ac813627d256
    -> ConvoLab CI run 34109230326 (success)
    -> Release Build run 34111500739 (success)
    -> release artifact 10014601379
    -> release manifest
    -> immutable API/Studio image digests
    -> CycloneDX SBOMs
    -> SBOM SHA-256 verification
    -> Trivy scans
    -> provenance/SBOM attestations
```

## Historical evidence

Earlier Alpha.17 evidence referenced superseded source commits and workflow runs, including `ed0aed28...` / `33743589890`. Those records are historical and are superseded by the artifact chain documented above. They must not be treated as the authoritative final Alpha.17 artifact source.

## Remaining verification action

Run the repository verifier against the retrieved canonical artifact bundle:

```bash
node web/scripts/verify-baseline.mjs
```

Record its actual output before declaring the artifact evidence chain fully closed. Do not infer verifier success solely from workflow configuration or historical documentation.

The Docker acceptance workflow continues to use an ephemeral 32-byte `BACKUP_ENCRYPTION_KEY`; the key must not be committed or treated as a production secret.
