ConvoLab Audit Findings
======================

Date: 2026-08-18
Auditor: AI assistant using Copilot CLI runtime in VS Code
Scope: Full repository audit focused on architecture, secret handling, authentication, telemetry, production readiness, and tests.

Executive summary
-----------------
ConvoLab is a well-architected Clean-Architecture ASP.NET Core platform with a React Studio frontend. The codebase demonstrates deliberate operational and security thought: startup-time production validation, explicit secret handling patterns, suppressed telemetry instrumentation for secret-carrying requests, cookie hardening, and rate limiting. Automated tests (unit, integration, and architecture) run successfully in the audit environment.

No critical implementation vulnerabilities were discovered in the inspected surface. Remaining risks are primarily operational (in-memory secret caching exposure, logging hygiene, host-level diagnostics) and can be mitigated with short-term changes and CI checks.

Scope and methodology
---------------------
- Static inspection of core files and directories, including docs and architecture artifacts.
- Targeted review of secret handling, providers, authentication flows, telemetry, and production validation code.
- Ran the full test suite locally to validate behavior and existing test coverage.
- Searched for secret usage, RevealValue() call sites, and instrumentation suppression markers.

Files inspected (representative)
---------------------------------
- ARCHITECTURE.md (C:/Users/W1022804/convolab-main/ARCHITECTURE.md)
- Program.cs (C:/Users/W1022804/convolab-main/src/Api/ConvoLab.Api/Program.cs)
- SecurityRegistration.cs (C:/Users/W1022804/convolab-main/src/Api/ConvoLab.Api/Security/SecurityRegistration.cs)
- EntraAuthentication.cs (C:/Users/W1022804/convolab-main/src/Api/ConvoLab.Api/Security/EntraAuthentication.cs)
- ConvoLabAuthenticationHandler.cs (C:/Users/W1022804/convolab-main/src/Api/ConvoLab.Api/Security/ConvoLabAuthenticationHandler.cs)
- SessionCookieService.cs (C:/Users/W1022804/convolab-main/src/Api/ConvoLab.Api/Security/SessionCookieService.cs)
- CompositeSecretStore.cs (C:/Users/W1022804/convolab-main/src/Infrastructure/ConvoLab.Infrastructure/Settings/CompositeSecretStore.cs)
- SettingsContracts.cs (C:/Users/W1022804/convolab-main/src/Application/ConvoLab.Application/Settings/SettingsContracts.cs)
- ProviderValidationService.cs (C:/Users/W1022804/convolab-main/src/Infrastructure/ConvoLab.Infrastructure/Settings/ProviderValidationService.cs)
- GeminiIntelligenceExecutor.cs (C:/Users/W1022804/convolab-main/src/Infrastructure/ConvoLab.Infrastructure/Intelligence/GeminiIntelligenceExecutor.cs)
- OperationalSecretStoreTests.cs (C:/Users/W1022804/convolab-main/src/tests/ConvoLab.Infrastructure.IntegrationTests/Settings/OperationalSecretStoreTests.cs)
- ProductionReadinessValidator.cs (C:/Users/W1022804/convolab-main/src/Api/ConvoLab.Api/Operations/ProductionReadinessValidator.cs)

Test results
------------
The solution test suite was executed during the audit. Representative results (local run):
- ConvoLab.Domain.Tests: 188 passed
- ConvoLab.Application.Tests: 42 passed
- ConvoLab.Infrastructure.IntegrationTests: 86 passed
- ConvoLab.Api.IntegrationTests: 91 passed
- ConvoLab.ArchitectureTests: 16 passed
All tests passed in the audit environment.

Key findings (prioritized)
--------------------------
1) In-memory plaintext secret caching (Medium)
   - Evidence: CompositeSecretStore caches resolved secret strings into IMemoryCache (CompositeSecretStore.cs).
   - Risk: Secrets in process memory are exposed to host-level memory-dumps, process heap inspection, or compromised admin accounts.
   - Recommendation:
     - Reduce default cache TTL (e.g., to 30–60s) or make production defaults conservative.
     - Provide an option to opt-out of caching for particularly sensitive secrets.
     - Document threat model and operational controls (no automatic dumps, restricted operator access).
   - Estimated effort: 1–2 days (config + docs + possible CI tests).

2) Logging and telemetry leakage risk (Medium)
   - Evidence: RevealValue() is used in a limited set of places (Entra token exchange, provider validation, Gemini executor). Request messages set SensitiveTelemetryHttpRequestOptions.SuppressAutomaticInstrumentation before adding secret headers (ProviderValidationService.cs, GeminiIntelligenceExecutor.cs).
   - Risk: Future code changes or custom logging could accidentally log a secret value or email secret header into telemetry if not careful.
   - Recommendation:
     - Add a CI check (simple script) to fail on new call sites of RevealValue() outside an allow-list.
     - Denylist sensitive header names in any custom request-logging middleware and assert they are not present in telemetry payloads.
     - Add unit tests asserting that OIDC token-exchange errors or other exception paths do not include secret payloads in logs.
   - Estimated effort: 1–3 days (CI check + tests + policy doc).

3) OIDC token exchange logging surface (Low→Medium)
   - Evidence: AuthorizationCodeReceived assigns the resolved client secret to context.TokenEndpointRequest.ClientSecret (EntraAuthentication.cs). SaveTokens = false in OIDC options (SecurityRegistration.cs).
   - Risk: If error/exception logging captures TokenEndpointRequest or exception objects that include the request, the secret could be logged.
   - Recommendation:
     - Ensure error handlers and logging (GlobalExceptionMiddleware, any OIDC event error paths) do not log OIDC request objects or their fields.
     - Add a focused integration test to simulate token-exchange errors and assert logs do not contain the secret.
   - Estimated effort: 1–2 days.

4) Secret canonicalization and invalidation correctness (Low)
   - Evidence: CompositeSecretStore builds canonical references from parsed scheme and key; Invalidate increments generation and removes cache entry (CompositeSecretStore.cs). OperationalSecretStoreTests exercise caching and invalidation.
   - Risk: If SecretReference.ParseReference allows ambiguous normalization, cached keys could collide or be missed on invalidation.
   - Recommendation:
     - Add unit tests for reference canonicalization (case/whitespace/alternate separators) to ensure deterministic canonical keys across callers.
   - Estimated effort: half-day to 1 day.

5) Production startup validation (Positive)
   - Evidence: ProductionReadinessValidator performs numerous startup-time checks and Program.cs calls ValidateStaticOrThrow at startup.
   - Recommendation: Keep these checks and ensure deployment runbooks align with them; document how to run pre-deploy validations.
   - Estimated effort: documentation update only (half-day).

6) Telemetry and instrumentation configuration (Positive with minor improvements)
   - Evidence: OpenTelemetry is configured, and call sites suppress automatic instrumentation for secret-carrying HTTP requests.
   - Recommendation: Formalize telemetry policy (denylist for headers and sensitive attributes). Add CI tests or sampling checks for telemetry payloads.
   - Estimated effort: 1–2 days.

7) Break-glass and operational account controls (Process)
   - Evidence: Program.cs checks for break-glass authorised accounts when enabled.
   - Recommendation: Publish an operational runbook describing who manages the break-glass account, rotation procedures, and audit expectations.
   - Estimated effort: documentation only (half-day).

Low-severity / informational observations
----------------------------------------
- Azure Key Vault credential factory uses DefaultAzureCredential in development and ChainedTokenCredential (WorkloadIdentity then ManagedIdentity) in production — explicit and testable; allow-list enforcement is implemented.
- Docker secrets provider enforces path traversal, symlink reparse point rejection, and unsafe permission checks on non-Windows systems (CompositeSecretStore.cs, OperationalSecretStoreTests.cs).
- ConvoLabAuthentication uses secure cookie defaults and hashes session tokens before storing them in DB (ConvoLabAuthenticationHandler.cs).

Recommended immediate tasks (concrete)
-------------------------------------
1. Create findings.md (this document) — done.
2. Add a CI check that flags any new RevealValue() usage outside an allow-listed file set (e.g., EntraAuthentication, ProviderValidationService, GeminiIntelligenceExecutor) and denies direct logging of secret values or headers. (High priority)
3. Short-term change: lower default secret cache TTL in SecretStoreOptions or expose a "do-not-cache" flag for secret references that must never be cached. (Medium priority)
4. Add focused tests:
   - Assert that OIDC token-exchange failures do not lead to secret values being logged.
   - Assert that secret canonicalization is deterministic for common variants.
5. Document: a short security/ops page describing where secrets are allowed to be revealed, how to invalidate caches on rotation, and host-level diagnostics policies.

Suggested owners & estimated effort
-----------------------------------
- CI check + tests: DevOps + Platform team — 1–3 days
- Config TTL change + docs: Platform team — 1 day
- Telemetry policy + denylist: Observability team — 1–2 days
- Break-glass runbook: Operations — half-day

Appendix: actionable file references
------------------------------------
- Secret caching and providers: src/Infrastructure/ConvoLab.Infrastructure/Settings/CompositeSecretStore.cs
- Secret type + RevealValue(): src/Application/ConvoLab.Application/Settings/SettingsContracts.cs
- Secret usage in authentication: src/Api/ConvoLab.Api/Security/EntraAuthentication.cs
- Provider validation usage: src/Infrastructure/ConvoLab.Infrastructure/Settings/ProviderValidationService.cs
- Provider invocation (Gemini): src/Infrastructure/ConvoLab.Infrastructure/Intelligence/GeminiIntelligenceExecutor.cs
- Session cookie and authentication: src/Api/ConvoLab.Api/Security/SessionCookieService.cs and src/Api/ConvoLab.Api/Security/ConvoLabAuthenticationHandler.cs
- Production readiness checks: src/Api/ConvoLab.Api/Operations/ProductionReadinessValidator.cs

Next steps
----------
Choose one or more items to continue work:
- I can add the CI check and a minimal test to the repository (create a script and a test that fails CI on unsafe patterns).
- I can open a PR to change the default secret cache TTL and add comments documenting the tradeoffs.
- I can implement the suggested unit tests for OIDC token-exchange logging and secret canonicalization.

Location
--------
The findings are saved to: [findings.md](C:/Users/W1022804/convolab-main/findings.md)


Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
