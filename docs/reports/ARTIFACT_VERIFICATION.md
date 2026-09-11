# Artifact verification: `v1.0.0-alpha.17`

This document records verification against the canonical CI-produced Alpha.17 release artifacts for the authoritative `main` source baseline.

## Baseline

| Field | Value |
| --- | --- |
| Authoritative source commit | `f8090674651056e90ddb437d12a5d2680038ae34` |
| Release version | `1.0.0-alpha.17` |
| Release workflow | `Release Build & Artifact Assembly` |
| Workflow run | `34204157803` |
| GitHub Actions artifact | `release-artifacts` |
| Artifact SHA-256 | `662f02336d96ab7ff295ebc7119744606d0f246572bec1df61636dc75282063f` |
| Current evidence state | **VERIFIED** |

The artifact was retrieved from GitHub Actions and inspected directly. GitHub reports that it was produced by `main` at source commit `f8090674651056e90ddb437d12a5d2680038ae34`.

## Release manifest

The retrieved `release/manifest.json` records:

| Field | Value |
| --- | --- |
| `releaseManifestId` | `release-manifest-1.0.0-alpha.17-f8090674` |
| `releaseVersion` | `1.0.0-alpha.17` |
| `sourceCommitSha` | `f8090674651056e90ddb437d12a5d2680038ae34` |
| `apiImageDigest` | `ghcr.io/kevinkutoane/convolab/convolab-api@sha256:57e4420758d22e89d05d82e959a409a1116e0f07f9698f56b8b264964888b707` |
| `studioImageDigest` | `ghcr.io/kevinkutoane/convolab/convolab-studio@sha256:9e2a6f2950d7548d1b49823c47c761b56001e1895ccd8e4d48c7e57b1da45f02` |
| `migrationVersion` | `202608200002_DeploymentPromotionV1` |
| `apiSbomSha256` | `e505732e6dda8ee3014fb468a4d1f5cb0fe5fe9dff0e00cd406bbc87fe64486a` |
| `studioSbomSha256` | `daba05bbbddf7babe275abae9f335b423fdffdf8cdb969ec76884245c9589af2` |
| `provenanceReference` | `https://github.com/kevinkutoane/convolab/actions/runs/34204157803` |
| `cryptographicAttestation` | `github-actions-attest-build-provenance-v1` |
| `buildWorkflowId` | `34204157803` |
| `buildTimestamp` | `2026-09-08T08:22:55Z` |
| `isBackwardCompatible` | `true` |
| `requiresDowntime` | `false` |

## Required evidence

| Check | Status | Evidence |
| --- | --- | --- |
| `manifest.json` present and well-formed | **VERIFIED** | Retrieved from artifact |
| `manifest.releaseVersion == "1.0.0-alpha.17"` | **VERIFIED** | Manifest reports `1.0.0-alpha.17` |
| Manifest source commit matches authoritative `main` | **VERIFIED** | Manifest reports `f8090674651056e90ddb437d12a5d2680038ae34` |
| API and Studio image digests resolved | **VERIFIED** | Full immutable digests recorded above |
| API and Studio CycloneDX SBOMs present | **VERIFIED** | Both files retrieved from `sbom/` |
| API SBOM SHA-256 matches file | **VERIFIED** | `e505732e6dda8ee3014fb468a4d1f5cb0fe5fe9dff0e00cd406bbc87fe64486a` |
| Studio SBOM SHA-256 matches file | **VERIFIED** | `daba05bbbddf7babe275abae9f335b423fdffdf8cdb969ec76884245c9589af2` |
| Vulnerability scan result retained | **VERIFIED** | Release workflow completed the Trivy scan stages successfully |
| Provenance/SBOM attestation | **VERIFIED** | Release workflow completed the attestation stages successfully |
| Immutable release manifest | **VERIFIED** | Manifest is present and internally consistent |
| `verify-baseline.mjs` version/encoding/ZAR checks pass against repository source | **VERIFIED** | Script checks `web/package.json` version, selected file version strings, mojibake encoding, and non-ZAR currency references in source/docs. It does **not** verify SBOM hashes, image digests, or the release manifest — those are verified by the release workflow and the rows above. |

## CI execution evidence

The corresponding `ConvoLab CI` run was:

```text
Workflow: ConvoLab CI
Run: #92
Run ID: 34200159001
Head SHA: f8090674651056e90ddb437d12a5d2680038ae34
Conclusion: success
```

The successful Docker acceptance job included readiness, cross-capability tests, Playwright browser tests, restart persistence verification, and post-restart browser tests.

## Evidence chain

```text
main source commit f8090674651056e90ddb437d12a5d2680038ae34
    -> ConvoLab CI run 34200159001 (success)
    -> Release Build run 34204157803 (success)
    -> release artifact release-artifacts
    -> release manifest
    -> immutable API/Studio image digests
    -> CycloneDX SBOMs
    -> SBOM SHA-256 verification
    -> Trivy scans
    -> provenance/SBOM attestations
```

## Historical evidence

Earlier Alpha.17 evidence referenced superseded source commits and workflow runs, including `91a72e4...` / `34111500739` and `ed0aed28...` / `33743589890`. Those records are historical and are superseded by the artifact chain documented above. They must not be treated as the authoritative final Alpha.17 artifact source.

## Remaining verification action

The Alpha.17 artifact evidence chain is **closed and frozen** as of this document.

`web/scripts/verify-baseline.mjs` can be run at any time as a local repository consistency check:

```bash
node web/scripts/verify-baseline.mjs
```

It validates version stamps, encoding integrity, and ZAR currency references in source and documentation files.
It does **not** re-verify the release artifact, SBOM hashes, image digests, or the release manifest.
Those are immutably recorded in the rows above and tied to the CI/release workflow provenance chain.

The Docker acceptance workflow continues to use an ephemeral 32-byte `BACKUP_ENCRYPTION_KEY`; the key must not be committed or treated as a production secret.
