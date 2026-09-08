# Alpha.17 Baseline Closure Report

## Current authoritative main baseline

The current authoritative Alpha.17 source baseline is `main` at the latest documentation-reconciled commit. The latest commit is intentionally documentation-only; the implementation baseline remains the validated Alpha.17 implementation.

Validated implementation baseline:

```text
91a72e4324ed3ce2f861be5a6889ac813627d256
```

Latest main documentation reconciliation commit:

```text
23af85d943bc099ed277165eb540929ae6c86619
```

No Alpha.17 product capability or architecture rewrite is introduced by the evidence reconciliation commits.

## Current CI evidence

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

## Existing Alpha.17 release artifact

The previously produced canonical Alpha.17 release artifact remains valid for the implementation commit it actually built:

```text
Source commit: 91a72e4324ed3ce2f861be5a6889ac813627d256
Release workflow: 34111500739
GitHub Actions artifact: 10014601379
Release version: 1.0.0-alpha.17
```

Its manifest and SBOM hashes were independently checked for internal consistency. It must not be described as an artifact built from the later documentation commits.

## Evidence reconciliation

The evidence now distinguishes:

1. The Alpha.17 implementation baseline at `91a72e432...` had successful CI and a successful Alpha.17 release build.
2. The later documentation-reconciled `main` state has independently passed the full CI matrix, including the baseline verifier.
3. The existing release artifact remains cryptographically tied to `91a72e432...`.
4. The documentation-only commits do not change the validated product implementation, but a fresh release build is still required before claiming the final `main` state and release artifact are one exact source-to-artifact chain.

## Freeze decision

### AMBER — implementation and current main CI green; final release binding remains

Alpha.17 implementation validation is green and the current main line has passed CI. Formal Alpha.17 freeze requires a fresh release build from the final main state, with the resulting manifest, immutable image digests, SBOM hashes, provenance/attestations, and artifact record bound to that release source.

Do not begin Alpha.18 implementation until that release binding is complete.
