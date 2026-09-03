# Artifact verification: `v1.0.0-alpha.17`

This document records verification against CI-produced release artifacts for the authoritative alpha.17 baseline.

## Baseline

| Field | Value |
| --- | --- |
| Authoritative source commit | `ed0aed28bb6a6fb06584c6ef5ef769ff59e7f864` |
| Release version | `1.0.0-alpha.17` |
| Required artifact source | GitHub Actions `Release Build & Artifact Assembly` workflow |
| Local artifact directory | `docs/reports/artifacts/` |
| Current evidence state | **VERIFIED**; workflow run 33743589890 successful and artifacts present |

## Required evidence

| Check | Status | Evidence or blocker |
| --- | --- | --- |
| `manifest.json` present and well-formed | **VERIFIED** | Present in release artifacts |
| `manifest.releaseVersion == "1.0.0-alpha.17"` | **VERIFIED** | Matches `1.0.0-alpha.17` |
| API and Studio image digests resolved | **VERIFIED** | API: `sha256:25eca43...` Studio: `sha256:175c76e...` |
| API and Studio CycloneDX SBOMs present and valid | **VERIFIED** | Present in `artifacts/sbom/` |
| SBOM SHA entries match files | **VERIFIED** | API: `19beae5aa34...` Studio: `229a4d86e5e...` |
| Vulnerability scan result retained | **VERIFIED** | Trivy passed (CVE-2026-31789 fixed) |
| Provenance attestation reference retained | **VERIFIED** | actions/runs/33743589890 |
| `verify-baseline.mjs` runs clean against repository and artifacts | **VERIFIED** | Version, encoding, and ZAR checks passed |

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
