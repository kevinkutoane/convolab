# alpha.15 Operational Foundation — Final Sign-Off

## Release position

- Active release metadata: `1.0.0-alpha.14`
- Workstream: `alpha.15 Operational Foundation — Final Sign-Off`
- Tranche status: signed off as an operational-foundation tranche, not as the complete alpha.15 release
- Deferred and not started here: Entra/OIDC, backup/restore, deployment promotion, supply-chain controls, and the final alpha.15 readiness report

## Repository evidence

| Evidence | Result |
|---|---|
| Original directory | Passed: `C:\Users\W1022804\convolab` no longer exists. |
| Final directory | `C:\Users\W1022804\convolab-main` |
| Rename result | Passed: the existing repository now runs from the final directory. |
| Git branch | `main` |
| Git HEAD | `60fdda39874e75ddba663ada83bc610d57dd63c0` |
| Git working tree | Deliberately dirty and preserved: 40 tracked modified paths and 18 untracked paths, including this report. No reset, clean, checkout, stash, or history rewrite was used. |
| Required root contents | `.git/`, `ConvoLab.sln`, `src/`, `web/`, `docs/`, and `tools/` are intact under `C:\Users\W1022804\convolab-main`. |
| Docker persistence across folder rename | Compose project name remains explicitly pinned to `convolab`; the preserved `convolab_postgres_data`, `convolab_knowledge_documents`, and `convolab_data_protection_keys` volumes are attached after the folder rename. Service and namespace identifiers were not renamed. |

Metadata inspection confirmed `Directory.Build.props`, `web/package.json`, and `web/package-lock.json` remain `1.0.0-alpha.14`; no active `1.0.0-alpha.15` release metadata was introduced.

## Development readiness decision

| Evidence | Result |
|---|---|
| Previous provider | `Gemini` |
| Previous required-secret result | `Unhealthy`: the active/default Development environment selected Gemini without an effective secret reference. |
| New provider | `Deterministic` |
| New model | `convolab-deterministic-primary` |
| New effective configuration revision | `sha256:1f555615386d14fced182032516542f78217d2489dd51f77cc8b2c94605b3dda` |
| Environment setting revisions | `ai.provider` environment revision 1; `ai.model` environment revision 2; environment revision was 14 before the governed changes. |
| Decision reason | Deterministic execution is the safest internal Development default: it removes the unintentional external dependency, needs no Gemini secret, retains normal readiness validation, and leaves platform-level Gemini capability available for later intentional testing. |

No fake API key was added. The final secret-provider evidence identifies Development as `Deterministic`, `required=false`, with no secret-provider scheme. Gemini was not resolved or invoked.

## Health and operational evidence

The following evidence was captured from the final corrected Docker images after API and PostgreSQL restart recovery and worker lease takeover.

| Endpoint | HTTP | Sanitized response summary |
|---|---:|---|
| `GET /health/live` | 200 | `Healthy`; version `1.0.0-alpha.14`; minimal anonymous response with correlation ID. |
| `GET /health/ready` | 200 | `Healthy`; version `1.0.0-alpha.14`; minimal anonymous response with correlation ID. |
| `GET /api/platform/status` | 200 | Version `1.0.0-alpha.14`; workstream `alpha.15 Operational Foundation — Final Sign-Off`; Development; safe mode `false`. |
| `GET /api/operations/status` | 200 | Overall/readiness `Healthy`; release status `in-progress`; safe mode disabled; worker `LiveValidated`; Analytics `Healthy`, pending 0, failed 0; telemetry `NotConfigured`. |
| `GET /api/operations/readiness` | 200 | Overall `Healthy`. Production configuration `Configured`; providers `StubValidated`; data protection, database, document storage, workspace identity, required secrets, worker, and Analytics pipeline `LiveValidated`; every component `Healthy`. |
| `GET /api/operations/secret-providers` | 200 | Azure Key Vault `NotConfigured`; Docker and environment providers `Configured`; Development deterministic and not secret-requiring; no values or references returned. |
| `GET /api/operations/workers` | 200 | `analytics-maintenance` `Healthy`; fresh heartbeat and successful iteration; lease owner `6c306de608c3:1`, fencing token 504, server-derived expiry `2026-08-03T13:09:27.872155+00:00`. |
| `GET /api/operations/analytics-pipeline` | 200 | `Healthy`; pending 0; failed 0; dirty/failed checkpoints 0; maximum aggregation lag 0 seconds. |
| `GET /api/operations/backups` | 200 | Explicit `NotConfigured`; no invented RPO, RTO, age, or verification claim. |

The replacement worker did not take over early after image recreation. Readiness temporarily returned 503 until the previous PostgreSQL-server-time lease expired, then returned 200 with a new owner and fencing token. This is the expected single-owner behavior, not an application-clock bypass.

## Execution and attribution evidence

A final deterministic simulation was executed through the corrected image and reconciled to its trace and Analytics evidence.

| Field | Evidence |
|---|---|
| SourceExecutionId | `23847d77-eb51-498f-9328-a8ad488517ea` |
| CorrelationId | `6eeb0869-ddf3-4b56-9dcc-4f9fe57e069d` (Analytics compact form `6eeb0869ddf34b569dcc4f9fe57e069d`) |
| OrganisationId | `10000000-0000-0000-0000-000000000001` |
| WorkspaceId | `20000000-0000-0000-0000-000000000001` |
| EnvironmentId | `cdf8ef31-fd55-449c-b584-387bf4741372` |
| ActorId | `30000000-0000-0000-0000-000000000001` |
| ConfigurationRevision | `sha256:1f555615386d14fced182032516542f78217d2489dd51f77cc8b2c94605b3dda` |
| Provider | `Deterministic` |
| Model | `convolab-deterministic-primary` |
| Policy outcome | Terminal execution `Allowed`; bounded governance decisions also included a non-blocking `Warning`. |
| Evaluation outcome | `Passed` |
| Trace ID | `23847d77-eb51-498f-9328-a8ad488517ea`; status `Completed`; 10 spans; provider shown as local test provider. |
| Analytics event IDs | `366f106b-7180-4d5f-aa38-30c5f7e26ad4`, `61af4d31-44c5-4865-a9b6-2775f433b1d4`, `dfd01b81-cd28-4dda-9bbd-b6467f8870c4`, `03aa29fc-d410-4ddc-b838-c20551a41dd1`, `f472be07-aee0-486f-8de6-884f625acf6c`, `0a6d5438-9b61-4745-acec-2c21866fd344`, `4e2123b3-499e-4e18-87d8-5a8125b24814`, `c9041079-f9ca-4211-9095-758b58f319ad`, `fbf7da06-5085-4c8b-929d-9a1d78c1c31b`, `377b3db3-a750-405e-bde6-266322a4b106`, `04385b26-fb85-4939-942a-662d03e4dfd1` |

All 11 Analytics events use the same source execution, organisation, workspace, environment, actor, deterministic provider/model, and configuration revision. The database reconciliation showed matching hourly/daily totals and Original attribution links. The policy-denial acceptance produced `ProviderInvocationPrevented` with zero input tokens, zero output tokens, and zero cost before any provider execution record.

## Vulnerability evidence

| Command | Exit | Result |
|---|---:|---|
| `dotnet list ConvoLab.sln package --vulnerable --include-transitive` | 0 | Connected to `https://api.nuget.org/v3/index.json`; 0 vulnerable packages across all 9 projects, including transitives. |
| `npm audit --audit-level=low` | 0 | Connected npm advisory endpoint; 0 vulnerabilities. |

The first sandbox-restricted attempts could not access their feeds and were not treated as clean scans. Both commands were rerun with connected network access; only the connected results above support sign-off.

## Verification table

| Command/check | Exit/result | Tests or evidence | Warnings/notes |
|---|---:|---|---|
| `dotnet restore ConvoLab.sln --force-evaluate` | 0 / Passed | 9 projects restored | Connected rerun; no NU1900 warning. The initial restricted restore emitted NU1900 and was superseded. |
| `dotnet build ConvoLab.sln --configuration Release --no-restore` | 0 / Passed | 9 projects | 0 warnings, 0 errors. Rerun after the log-leak correction with the same clean result. |
| `dotnet test ConvoLab.sln --configuration Release --no-build` | 0 / Passed | 366/366: Domain 186, Application 42, Architecture 16, API integration 39, Infrastructure integration 83 | 0 failed, 0 skipped. Final-source rerun passed. |
| PostgreSQL migration filter | 0 / Passed | 8/8 | Covers fresh PostgreSQL, alpha.14 upgrade, Operational Foundation upgrade, reconnect/restart persistence, lease ownership/fencing, export claiming, and existing-schema preservation. |
| `npm ci` | 0 / Passed | 199 packages installed; 200 audited | Initial restricted attempt failed on registry/cache access; connected rerun passed with 0 vulnerabilities. |
| `npm run lint` | 0 / Passed | Full Studio ESLint | No lint findings. |
| `npm run test` | 0 / Passed | Frontend contract tests; interaction audit of 36 TSX files | No failures. |
| `npm run build` | 0 / Passed | 1,994 modules; initial graph and all 20 lazy-route budgets | Active package version printed as `1.0.0-alpha.14`. |
| `npm audit --audit-level=low` | 0 / Passed | 0 vulnerabilities | Connected scan. |
| `docker compose build api` | 0 / Passed | Release API image | Rebuilt after secret-reference parameterization. |
| `docker compose build web` | 0 / Passed | Production Studio image | Package metadata remained alpha.14; bundle budgets passed in image build. |
| `docker compose --profile telemetry up -d --wait` | 0 / Passed | PostgreSQL, API, Studio, and collector started | Required services healthy. |
| Health live/readiness | 200 / 200 | Minimal anonymous responses | Final status Healthy/Healthy. |
| Operations Playwright | 0 / Passed | 8/8 | Administrator routing, authorization, dependency states, safe mode, cross-session banner, and 3 responsive widths. |
| Complete Playwright | 0 / Passed | 21/21 | Final-image rerun after the leakage correction; 0 failed. |
| Cross-capability deterministic acceptance | 0 / Passed | Simulation, evaluation, trace, replay, plugin health, policy denial | Generated restart evidence; acceptance deny policy was retired afterward. |
| `docker compose restart api` plus restart verifier | 0 / Passed | 4/4 persisted resource categories retrieved | Simulation, replay, policy, and plugin identifiers survived. |
| `docker compose restart db` plus restart verifier | 0 / Passed | 4/4 persisted resource categories retrieved | PostgreSQL and API recovered healthy; identifiers survived. |
| Analytics reconciliation SQL | 0 / Passed | Event inventory, attributed timelines, denial invariants, hourly/daily aggregates, attribution links | Sensitive Analytics rows 0; sensitive exports 0. |
| Sensitive runtime log scan | 0 / Passed after correction | 6 final live-request sentinel categories absent from API/collector logs | Initial scan found a hard-coded default reference in EF Development query-plan logs. The query was parameterized, API rebuilt, 366 .NET tests and 21 Playwright tests rerun, and both boot-time and live-request scans passed. No secret value was exposed. |
| Metadata and root inspection | 0 / Passed | Required roots present; .NET/npm alpha.14 metadata confirmed | Workstream marker is final-sign-off while release remains alpha.14. |
| In-place workspace rename and post-rename restart | 0 / Passed | Original path absent; required roots intact at `C:\Users\W1022804\convolab-main`; preserved `convolab` Compose project running with all three existing volumes; live/readiness 200/200 | Existing images were reused with no rebuild. |

## Final declaration

Operational Foundation tranche: Signed off

The directory rename, preserved Compose restart, volume attachment, health checks, and active metadata verification are complete and passing.

This declaration applies only to the Operational Foundation tranche. It does not declare full alpha.15 complete; Entra/OIDC, backup/restore, deployment promotion, supply-chain controls, and the final readiness report remain required future workstreams.
