# Alpha.17 Baseline Closure Report

## Current authoritative main baseline

The current authoritative Alpha.17 source baseline is `main` at:

```text
cb22881191b888ac2d917287ced38cec26cbbaa8
```

This commit contains the reconciled Alpha.17 evidence documentation. No Alpha.17 product capability or architecture rewrite is introduced by these documentation-only reconciliation commits.

## Current CI evidence

`ConvoLab CI` run `34196441417` (#90) executed against the current `main` SHA and completed successfully.

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

The CI evidence therefore closes the previously pending repository-baseline verification action for the current `main` source. The verifier's success is evidenced by the successful Studio job step rather than inferred from documentation.

## Existing Alpha.17 release artifact

The previously produced canonical Alpha.17 release artifact remains valid for the implementation commit it actually built:

```text
Source commit: 91a72e4324ed3ce2f861be5a6889ac813627d256
Release workflow: 34111500739
GitHub Actions artifact: 10014601379
Release version: 1.0.0-alpha.17
```

Its manifest and SBOM hashes were independently checked for internal consistency. It must not be described as an artifact built from the newer `cb228811...` documentation commit.

## Evidence reconciliation

The documentation now distinguishes three facts that must not be conflated:

1. The implementation baseline at `91a72e432...` had a successful CI run and successful Alpha.17 release build.
2. The current `main` at `cb228811...` has now independently passed the full CI matrix, including the baseline verifier.
3. The existing release artifact is still cryptographically tied to `91a72e432...`, not `cb228811...`.

## Freeze decision

### AMBER — implementation and CI green; final release source/artifact binding remains

Alpha.17 implementation validation is green and current `main` is CI-clean. Formal freeze still requires a fresh Alpha.17 release build whose manifest `sourceCommitSha` equals the final frozen `main` SHA, followed by verification of the resulting artifact/SBOM/provenance chain.

Do not begin Alpha.18 implementation until that exact source-to-artifact chain is complete.
