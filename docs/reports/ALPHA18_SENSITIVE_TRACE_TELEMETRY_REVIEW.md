# Alpha.18 Sensitive Trace and Telemetry Redaction Review

**Review date:** 2026-10-13  
**Baseline:** `main` at `2791caa`  
**Scope:** trace/reveal handling, application logging, OIDC callbacks, operational telemetry, Infobip integration, and exception response paths.

## Executive conclusion

The review confirmed and remediated three information-leak paths and one secret-management defect:

1. A repository-wide Infobip webhook secret was embedded in application settings and a fallback constant. Webhook verification now fails closed unless an explicit secret is supplied through configuration or a secret provider.
2. Infobip provider response bodies and exception messages were logged and returned from the outbound adapter. These now use generic client-safe messages and status/type-only diagnostics.
3. The Serilog sensitive-data filter inspected only top-level property names and rendered message JWTs. It now also inspects exception text and sensitive key/value patterns before sink forwarding.
4. Deployment promotion/rollback endpoints returned raw `InvalidOperationException.Message` values. They now log only the exception type and return generic problem details.

OIDC callback logging was also reduced so issuer, tenant identifiers, and client-secret handling details are not emitted. `SaveTokens = false`, PKCE, nonce/correlation cookies, and explicit token validation remain enabled.

## Confirmed findings and disposition

| Finding | Severity | Disposition |
|---|---:|---|
| Shared Infobip webhook fallback secret in source/config/docs | High | Fixed; removed constant and committed default, fail-closed verification, documentation updated |
| Provider error body returned/logged by Infobip outbound adapter | High | Fixed; provider body is no longer read into logs/results |
| Exception message logged through exception enrichment outside filter coverage | High | Fixed; filter inspects exception text and sensitive key/value patterns |
| Deployment operation exception messages returned to clients | Medium | Fixed; generic problem details and type-only server diagnostic |
| OIDC issuer/tenant metadata in informational logs | Medium | Fixed; replaced with non-identifying event message |
| Sensitive trace artifact reveal | High | Existing control retained: sensitive artifacts are redacted by default and only mapped when `includeSensitive` is explicitly authorized by the controller path; authorization/tenant isolation remains an Alpha.18 workstream |
| Automatic outbound HTTP instrumentation | Medium | Existing Gemini and Azure Key Vault host suppression retained; custom suppression option remains available for other sensitive clients |

## Existing controls verified

- `GlobalExceptionMiddleware` returns generic details for unexpected exceptions and exposes only correlation IDs to clients.
- `SensitiveOutputSanitizerMiddleware` processes bounded `application/problem+json` bodies and redacts JWTs, email values, and sensitive-key values as a transport backstop.
- OIDC token persistence is disabled (`SaveTokens = false`); PKCE, nonce, issuer/audience/lifetime/signature validation are enabled.
- OTLP dependency evidence exposes state, endpoint presence, exporter state, service name, and failure codes—not secret material.
- Operational metrics contain counts, durations, statuses, and token/cost aggregates rather than prompt or response content.
- EF Core sensitive-data logging is not enabled by the application registration.

## Remaining Alpha.18 work

This review does **not** close the separate authorization finding: global repositories/services and trace APIs still require the planned workspace/tenant authorization enforcement and cross-tenant regression suite. Redaction cannot compensate for an authorization failure. The next implementation slice should bind every trace, analytics, simulation, knowledge, workflow, and operational query to the resolved workspace/organisation context, then add positive/negative cross-tenant tests.

## Validation

- `git diff --check`: passed.
- Repository scan for the removed Infobip fallback secret and fallback symbol: passed with no matches.
- `npm --prefix web ci --no-audit --no-fund --ignore-scripts`: passed.
- `npm run build`: passed; TypeScript, Vite production build, and bundle budgets passed.
- Native .NET build/tests could not run in the reset sandbox because the `dotnet` CLI is not installed and Docker is unavailable. CI must provide the authoritative .NET compilation and test gate before release promotion.
