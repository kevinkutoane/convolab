Artifact verification checklist for 1.0.0-alpha.17

This document records checks performed against CI-produced artifacts for release 1.0.0-alpha.17.

Checklist
- [ ] manifest.json present and well-formed JSON
- [ ] manifest.releaseVersion == "1.0.0-alpha.17"
- [ ] All image digests resolved and match expected registry entries
- [ ] SBOMs (CycloneDX) present for all components and valid JSON
- [ ] SBOM SHA entries match the checksum on file
- [ ] verify-baseline.mjs runs clean against repository and artifact inputs

Provenance
- Artifacts directory: docs/reports/artifacts/
- Artifact retrieval method: CI workflow run (workflow_dispatch)
- Retrieval notes: (populate after CI run)

Verification output
- (Paste manifest and SBOM verification outputs here after running checks)
