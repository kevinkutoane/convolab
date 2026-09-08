# Alpha.17 Baseline Closure Report

## Final authoritative baseline

The Alpha.17 implementation baseline was verified on `main` at:

```text
91a72e4324ed3ce2f861be5a6889ac813627d256
```

This supersedes the earlier closure baseline `b3f6bfe4c6be7fd47ec407d95a0ce7ba1bb22242` and the subsequent historical evidence commits. No Alpha.17 product capability or architecture rewrite is introduced by the final evidence reconciliation.

## CI and release evidence

The authoritative implementation baseline passed `ConvoLab CI` run `34109230326`, including Docker acceptance, Playwright browser tests, restart persistence, and post-restart browser verification.

The release workflow `34111500739` completed successfully against the same implementation commit and produced the canonical Alpha.17 artifact `10014601379`.

The release manifest records:

- Release: `1.0.0-alpha.17`
- Source commit: `91a72e4324ed3ce2f861be5a6889ac813627d256`
- API image digest: `sha256:a8f683daec33cd7fc97ab020c5d5618b2ce9b03986ec39b14624070c772d62ae`
- Studio image digest: `sha256:21a6445556875fa039e768e4bc010b5b00f0e0894a3e0654bb796c7aa5b9263e`
- API SBOM SHA-256: `953e9368f1a1798ae03e9aaf4e1d66ba34939277fd6a7bbc43272410e97f2ca1`
- Studio SBOM SHA-256: `37a0c46b8422da00f962227f84700b992cecb4508f79274053720607ddb0a99e`
- Provenance workflow: `34111500739`

The artifact bundle was retrieved and its manifest and SBOM hashes were independently checked for internal consistency.

## Historical closure state

The earlier report described `b3f6bfe...` as the active baseline and left Docker/release evidence **NOT VERIFIED**. That was correct for that historical point in time. It is now superseded by the successful current evidence described above.

The earlier security findings that were explicitly reserved for Alpha.18 remain Alpha.18 planning scope. They must not be represented as Alpha.17 delivered functionality, but they also do not invalidate the demonstrated Alpha.17 CI/release evidence.

## Evidence reconciliation status

The repository evidence documents have been updated to identify the `91a72e432...` release artifact chain and to distinguish historical evidence from current evidence.

The repository verifier `web/scripts/verify-baseline.mjs` still requires execution in the repository environment against the retrieved artifact bundle. No verifier success is claimed here without actual execution output.

## Important source/artifact relationship

The canonical release artifact documented above was built from `91a72e432...`. Subsequent evidence-document commits have changed `main` after that artifact was produced. Therefore the existing release artifact must not be described as an artifact built from the newer documentation commit(s).

Before formal Alpha.17 freeze, the final `main` SHA must receive a fresh successful CI run and, where the release process requires exact source-to-artifact binding, a fresh release build and verifier run against that final SHA.

## Freeze decision

### AMBER — evidence reconciliation substantially complete; final source/artifact binding pending

Alpha.17 implementation and CI/release validation are green for the demonstrated implementation baseline. The remaining closure requirement is to produce final evidence against the post-reconciliation `main` SHA so that the frozen source, CI run, release artifacts, and verifier result form one exact chain.

Do not begin Alpha.18 implementation until that final chain is complete.
