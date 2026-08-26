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
- I will now continue with the in-depth audits requested (authentication flows, production readiness, and external adapters) and publish evidence-backed recommendations below.

Authentication audit (detailed)
--------------------------------
Summary:
- Authentication is implemented with careful considerations: secure cookies, hashed session tokens, PKCE support in OIDC flows, explicit break-glass handling, and invitation linking for external identities.

Findings and evidence:
1. Safe return URL validation: EntraAuthentication.IsSafeReturnUrl uses layered decoding and strict checks to avoid open-redirects (EntraAuthentication.cs). Good.
2. Token exchange secrecy: The OIDC AuthorizationCodeReceived event resolves the client secret at exchange time using CompositeSecretStore and assigns it to context.TokenEndpointRequest.ClientSecret; SaveTokens is false (SecurityRegistration.cs). This limits persistence of the secret. Ensure error handling does not serialize TokenEndpointRequest.
3. Break-glass flow: Break glass is audited and requires a pre-provisioned local credential; Program.cs rejects startup if break glass enabled but no admin credential exists. Recommend documenting the operator rotation and logging expectations.
4. Invitation linking: Authorization flow checks invitation hash and state before linking; code handles consumed/revoked/active states and returns explicit failure codes (EntraAuthentication.cs).

Recommendations (auth):
- Add a focused unit/integration test that induces a token-exchange failure and asserts logs do not contain the resolved client secret.
- Review global exception handling to ensure OIDC event exceptions are not logged with request objects; add a test to assert that.
- Document break-glass operational procedures: who can enable, rotate, and audit the credential.

Production readiness and OPA-like checks
---------------------------------------
Summary:
- ProductionReadinessValidator contains a strong set of checks for configuration placeholders, secret references, telemetry configuration, and evidence expiry. The validator is invoked at startup to prevent unsafe configurations.

Findings:
- The validator uses deny-lists of placeholder strings and verifies ClientSecretReference via secret resolution tests. It also verifies Entra settings and startup flags.
- Evidence expiry: dependency evidence snapshots include TTLs; ensure TTL values are operationally appropriate to not cause false positives.

Recommendations (readiness):
- Add a documented pre-deploy validation runbook that uses the same checks as ProductionReadinessValidator so release engineers can run validations before promoting to production.
- Parameterize and document the TTLs used for evidence expiry to align with operational polling cadence.
- Add a small script that runs ProductionReadinessValidator under a representative configuration and outputs a machine-readable JSON report for CI gating.

Infrastructure adapters (key vault, backups, telemetry)
--------------------------------------------------------
Summary:
- Key Vault: providers use DefaultAzureCredential and ChainedTokenCredential; secret references are validated. Good design.
- Backups: Postgres backup tooling exists and can create authenticated snapshots; backups were found in the repo and have been removed from VCS. Ensure backups are preserved in a secure external store instead of the repo.
- Telemetry: instrumentation suppression for secret-carrying requests is implemented; counters and ActivitySource use structured tags.

Findings:
- The PostgresBackupTooling uses parameters that may include passwords; ensure these are obtained from secrets (not inline) and that backup artifacts are stored off-repository in secure blob storage with encryption-in-transit and at-rest.
- The repo contains .env.example and CI placeholder creds; ensure those never contain real credentials and consider rotating any acceptance passwords referenced in CI if they were ever used in a shared environment.

Recommendations (infra):
- Ensure backup artifacts are pushed to a secure storage location (S3/Azure Blob) with strict ACLs and do not remain in the repository. Confirm and document the backup retention and access controls.
- Add CI checks that detect accidentally committed large artifacts (e.g., patterns in data/backups) and fail the push.
- Harden Postgres backup tooling so passwords are only read from secure secret references and not passed on command lines where possible.

Action plan (what will be done now)
-----------------------------------
1. Open a focused PR guidance for you to merge: I have pushed release/alpha.17-finalize; please open the PR in GitHub to trigger CI release artifact assembly. If you want, I can prepare the PR body text and instructions (I cannot open a PR without an authenticated GitHub token in this environment).
2. While CI runs, I completed the audits above and updated findings.md with the detailed audit sections.
3. I will watch for artifacts in artifacts/release/ when CI completes (if you want me to poll, provide a GitHub token or push the artifacts to an accessible location). Alternatively, an authenticated CI run will upload artifacts to the Actions UI for download.

Location
--------
The findings are saved to: [docs/reports/findings.md](C:/Users/W1022804/convolab-main/docs/reports/findings.md)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
