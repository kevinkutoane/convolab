# Artifact verification: `v1.0.0-alpha.17`

This document records verification against CI-produced release artifacts for the authoritative alpha.17 baseline.

## Baseline

| Field | Value |
| --- | --- |
| Authoritative source commit | `b3f6bfe4c6be7fd47ec407d95a0ce7ba1bb22242` |
| Release version | `1.0.0-alpha.17` |
| Required artifact source | GitHub Actions `Release Build & Artifact Assembly` workflow |
| Local artifact directory | `docs/reports/artifacts/` |
| Current evidence state | **NOT VERIFIED**; no successful workflow run and corresponding artifact bundle was available during this review |

## Required evidence

| Check | Status | Evidence or blocker |
| --- | --- | --- |
| `manifest.json` present and well-formed | **NOT VERIFIED** | CI artifact bundle unavailable |
| `manifest.releaseVersion == "1.0.0-alpha.17"` | **NOT VERIFIED** | CI artifact bundle unavailable |
| API and Studio image digests resolved | **NOT VERIFIED** | CI artifact bundle unavailable |
| API and Studio CycloneDX SBOMs present and valid | **NOT VERIFIED** | CI artifact bundle unavailable |
| SBOM SHA entries match files | **NOT VERIFIED** | CI artifact bundle unavailable |
| Vulnerability scan result retained | **NOT VERIFIED** | CI artifact bundle unavailable |
| Provenance attestation reference retained | **NOT VERIFIED** | CI artifact bundle unavailable |
| `verify-baseline.mjs` runs clean against repository and artifacts | **NOT VERIFIED** | Artifact inputs unavailable |

## Intended evidence chain

```text
source commit b3f6bfe4c6be7fd47ec407d95a0ce7ba1bb22242
    -> build
    -> API and Studio container images
    -> immutable image digests
    -> CycloneDX SBOMs
    -> vulnerability scan
    -> provenance attestation
    -> release manifest
```

## Required completion procedure

Run the release workflow for the authoritative commit. Retrieve the generated artifact bundle with `scripts/ci/download-ci-artifacts.sh`, place the files under the documented artifact directory, and execute the repository verification helper. Record the workflow run identifier, manifest contents, image digests, SBOM hashes, scan result, provenance reference, and verifier output here. Do not mark any item verified from workflow configuration alone.

The Docker acceptance workflow must receive an ephemeral 32-byte `BACKUP_ENCRYPTION_KEY` generated in CI. The key must not be committed or used as a production secret, and the application must continue to fail closed when it is absent.
