# ConvoLab Alpha.17 Release Evidence

## 1. Visual Regression (P0)
The visual regression in `operations.spec.ts` was resolved. The DOM locators were made more specific to distinguish between spans and headers correctly, ensuring the labels and active states match.
**Status**: Resolved. Playwright tests pass locally.

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
