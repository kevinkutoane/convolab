# Alpha.17 Baseline Closure Report

> **Freeze status: ALPHA.17 FROZEN** — The AMBER state recorded in the historical evidence section below was an intermediate status. The Alpha.17 release is fully frozen. Authoritative evidence is in the "Final Alpha.17 Closure Evidence" section and in [`docs/reports/ARTIFACT_VERIFICATION.md`](ARTIFACT_VERIFICATION.md).

## Final Alpha.17 Closure Evidence

The Alpha.17 release has been successfully built and verified against the authoritative `main` baseline:

**Authoritative source commit:** `f8090674651056e90ddb437d12a5d2680038ae34`

### CI and Release Verification
- **ConvoLab CI Run:** `34200159001` (Success)
- **Release Build & Artifact Assembly Run:** `34204157803` (Success)

### Artifact Integrity
- **Release Version:** `1.0.0-alpha.17`
- **Artifact SHA-256:** `662f02336d96ab7ff295ebc7119744606d0f246572bec1df61636dc75282063f`
- **Manifest ID:** `release-manifest-1.0.0-alpha.17-f8090674`
- **API SBOM Hash:** `e505732e6dda8ee3014fb468a4d1f5cb0fe5fe9dff0e00cd406bbc87fe64486a`
- **Studio SBOM Hash:** `daba05bbbddf7babe275abae9f335b423fdffdf8cdb969ec76884245c9589af2`
- **API Image Digest:** `ghcr.io/kevinkutoane/convolab/convolab-api@sha256:57e4420758d22e89d05d82e959a409a1116e0f07f9698f56b8b264964888b707`
- **Studio Image Digest:** `ghcr.io/kevinkutoane/convolab/convolab-studio@sha256:9e2a6f2950d7548d1b49823c47c761b56001e1895ccd8e4d48c7e57b1da45f02`

The release job passed every critical stage including API and Studio image builds, digest capture, CycloneDX SBOM generation, Trivy scans, provenance/SBOM attestations, immutable manifest assembly, and artifact publication. The SBOM hashes in the manifest exactly match the SHA-256 hashes of the SBOM files inside the artifact.

**Freeze Decision:** **ALPHA.17 FROZEN**

---

## Historical Evidence (Superseded)

### Historical authoritative main baseline

The historical authoritative Alpha.17 source baseline was `main` at the latest documentation-reconciled commit. The latest commit is intentionally documentation-only; the implementation baseline remains the validated Alpha.17 implementation.

Validated implementation baseline:

```text
91a72e4324ed3ce2f861be5a6889ac813627d256
```

Latest main documentation reconciliation commit:

```text
23af85d943bc099ed277165eb540929ae6c86619
```

No Alpha.17 product capability or architecture rewrite is introduced by the evidence reconciliation commits.

### Historical CI evidence

`ConvoLab CI` run `34196441417` (#90) executed against the documentation-reconciled `main` commit `cb22881191b888ac2d917287ced38cec26cbbaa8` and completed successfully.

The run passed:

- Repository hygiene
- ConvoLab Studio lint/build/unit/audit checks
- Platform Core restore/build/test checks
- Docker build and readiness
- Cross-capability tests
- Playwright browser tests
- Restart persistence verification
- Post-restart browser tests
- `npm run test:baseline` / `web/scripts/verify-baseline.mjs`

The baseline verifier therefore has actual successful CI evidence; its success is not inferred from workflow configuration.

### Historical Alpha.17 release artifact

The previously produced canonical Alpha.17 release artifact remains valid for the implementation commit it actually built:

```text
Source commit: 91a72e4324ed3ce2f861be5a6889ac813627d256
Release workflow: 34111500739
GitHub Actions artifact: 10014601379
Release version: 1.0.0-alpha.17
```

Its manifest and SBOM hashes were independently checked for internal consistency. It must not be described as an artifact built from the later documentation commits.

### Historical Evidence reconciliation

The evidence now distinguishes:

1. The Alpha.17 implementation baseline at `91a72e432...` had successful CI and a successful Alpha.17 release build.
2. The later documentation-reconciled `main` state has independently passed the full CI matrix, including the baseline verifier.
3. The existing release artifact remains cryptographically tied to `91a72e432...`.
4. The documentation-only commits do not change the validated product implementation, but a fresh release build is still required before claiming the final `main` state and release artifact are one exact source-to-artifact chain.

### Historical Freeze decision

#### AMBER — implementation and current main CI green; final release binding remains

Alpha.17 implementation validation is green and the current main line has passed CI. Formal Alpha.17 freeze requires a fresh release build from the final main state, with the resulting manifest, immutable image digests, SBOM hashes, provenance/attestations, and artifact record bound to that release source.

Do not begin Alpha.18 implementation until that release binding is complete.
