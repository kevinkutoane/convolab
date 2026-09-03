# ConvoLab Alpha.17 Release Evidence

## 1. Visual Regression (P0)
The visual regression in `operations.spec.ts` was resolved. The tests were updated to navigate the new tabbed UI structure of the Operations Center, and DOM locators were made more specific (e.g. strict element containment for the `stub` dependency). Test synchronization was improved by explicitly asserting on the semantic "API online" string and `api-online` connectivity classes.
**Status**: Resolved against commit `0ef86c6e5caba7487b4a22aa138a4114c7316c7f`. Browser suite, cross-capability, and restart persistence tests are fully green locally.

## 2. Authentication/Session Regression (P0)
The `.NET` integration tests for Entra OIDC safe return URLs and session handling were fixed.
- `AllowAutoRedirect = false` was configured on the test `HttpClient` in `AuthenticationRegressionTests.cs` to prevent Kestrel from validating external redirects internally.
- `TimeProvider` was added to `ConvoLabAuthenticationHandler` to ensure test assertions on TTL run deterministically.
**Status**: Resolved. All 99 integration tests pass.

## 3. Security Hardening (P1)
- **Secret Caching**: `ISecretStore.Clear()` method was introduced and implemented across all components (like `CompositeSecretStore` and test mocks) to properly clear credentials between runs.
- **OIDC Observability**: Detailed logging points were added to `ConvoLabOpenIdConnectEvents` spanning all critical OIDC flow events.
- **Secret Canonicalization**: Modified `CompositeSecretStore` cache resolution to lowercase both scheme and key strings (e.g. `$"{scheme.ToLowerInvariant()}:{key.ToLowerInvariant()}"`) to ensure lookups map `env:MyKey` to `env:mykey` safely.
- **Break-glass Controls**: Replaced `IOptions<AuthenticationOptions>` with `IOptionsSnapshot<AuthenticationOptions>` in `AuthController`, `OperationsController`, `ExternalIdentitiesController`, and `EntraAuthenticationHealthCheck` to enable strict `appsettings.json` override without restart so admins can force `Local` if OIDC is broken.

## 4. Release Validation
All requirements for the Alpha.17 hardening phase are now met. The baseline is verified and fully passes both browser integration and API tests.

## 5. Artifact Verification
The final release artifacts have been successfully verified against authoritative commit `ed0aed28bb6a6fb06584c6ef5ef769ff59e7f864`.
- **Workflow Run**: `33743589890` (Release Build & Artifact Assembly)
- **Manifest**: `release-manifest-1.0.0-alpha.17-5f32fb6a`
- **SBOM Validation**: CycloneDX SBOMs for API and Studio generated correctly.
- **Vulnerability Scans**: Passed (CVE-2026-31789 fixed in Alpine base).
- **Provenance Attestation**: Created successfully.
