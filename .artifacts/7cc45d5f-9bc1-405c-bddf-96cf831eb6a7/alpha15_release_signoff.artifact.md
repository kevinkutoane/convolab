# ConvoLab Platform and Studio — alpha.15 Release Sign-Off & Metadata Closure Report

## 1. Executive Summary & Release Status
The **alpha.15 — Microsoft Entra ID, External Identities & Hybrid Authentication** workstream has been closed and fully signed off. All authoritative metadata across backend, frontend, API, test contracts, and documentation have been updated from `1.0.0-alpha.14` to `1.0.0-alpha.15`.

- **Active Release Version:** `1.0.0-alpha.15`
- **Canonical Repository Root:** `convolab-main` (retained and unchanged)
- **Workstream Status:** `SIGNED OFF`
- **Provider Acceptance:** `StubValidated`
- **Live Microsoft Entra Tenant Validation:** `Not executed` (retained truthfully as an environment prerequisite)
- **Environment Readiness Distinction:** Local development readiness may report `Degraded` when optional AI provider credentials (such as `GEMINI_API_KEY`) are unconfigured. This is distinct from the completed authentication and identity release capabilities.

---

## 2. Updated Version Sources

| Scope | Source File | Updated Version |
| :--- | :--- | :--- |
| **Backend / .NET** | `Directory.Build.props` | `1.0.0-alpha.15` |
| **Root Package** | `package.json`, `package-lock.json` | `1.0.0-alpha.15` |
| **Studio Web Package** | `web/package.json`, `web/package-lock.json` | `1.0.0-alpha.15` |
| **API Fallback / Header** | `src/Api/ConvoLab.Api/Controllers/PlatformController.cs` | `1.0.0-alpha.15` |
| **Studio Platform Data** | `web/src/data/platform.ts` | `1.0.0-alpha.15` |
| **Studio UI Footer** | `web/src/components/Sidebar.tsx` | `1.0.0-alpha.15` |
| **Studio Login Brand** | `web/src/pages/LoginPage.tsx` | `1.0.0-alpha.15` |
| **Baseline Verification Script** | `web/scripts/verify-baseline.mjs` | `1.0.0-alpha.15` |
| **Integration Test Contracts** | `src/tests/ConvoLab.Api.IntegrationTests/ApiContractTests.cs` | `1.0.0-alpha.15` |
| **Transport Health Contracts** | `src/tests/ConvoLab.Api.IntegrationTests/ProductionTransportTests.cs` | `1.0.0-alpha.15` |
| **Documentation & Manifests** | `README.md`, `ROADMAP.md`, `CHANGELOG.md`, `DEPLOYMENT.md`, `ENTRA_HYBRID_AUTHENTICATION_REPORT.md`, `docs/PlatformManifest.md`, `docs/Roadmap.md`, `docs/Architecture/README.md`, `docs/Architecture/ProductReadinessAssessment.md`, `docs/MASTER_CHECKLIST_STATUS.md`, `docs/OperationalFoundation-alpha15.md`, `docs/operations/HealthChecks.md`, `docs/security/ProductionSecurityChecklist.md` | `1.0.0-alpha.15` |
| **Release Notes** | `docs/releases/PlatformCore-v1.0.0-alpha.15.md` | `[NEW]` |

---

## 3. Preserved Historical alpha.14 References
All historical alpha.14 references have been preserved where they accurately record prior releases or migrations:
- `docs/releases/PlatformCore-v1.0.0-alpha.14.md` (canonical release notes for alpha.14)
- `CHANGELOG.md` (`## 1.0.0-alpha.14 — 2026-07-28` section)
- `OPERATIONAL_FOUNDATION_FINAL_SIGNOFF.md` & `OPERATIONAL_FOUNDATION_CORRECTION_REPORT.md` (historical audit logs for prior milestones)
- `FUNCTIONAL_PLATFORM_ANALYTICS_V1_REPORT.md` & `PLATFORM_ANALYTICS_V1_REPORT.md` (historical milestone reports)
- `src/Infrastructure/ConvoLab.Infrastructure/Data/Migrations/202607240001_PlatformAnalyticsV1.cs` (migration seed constants)
- `src/Infrastructure/ConvoLab.Infrastructure/Analytics/AnalyticsRecords.cs` & `OperationalWorkerLease.cs` (deprecated backward-compatibility code comments)

---

## 4. Verification Results

- **.NET Test Suite:** `dotnet test`
  - `ConvoLab.Domain.Tests`: 188 passed (0 failed)
  - `ConvoLab.Application.Tests`: 42 passed (0 failed)
  - `ConvoLab.ArchitectureTests`: 16 passed (0 failed)
  - `ConvoLab.Infrastructure.IntegrationTests`: 86 passed (0 failed)
  - `ConvoLab.Api.IntegrationTests`: 91 passed (0 failed)
  - **Total:** 423 passed, 0 failed.
- **Frontend Verifications:**
  - `node scripts/verify-baseline.mjs`: PASSED for `1.0.0-alpha.15`.
  - `npm run lint`: PASSED (0 errors, 0 warnings).
  - `npm run test -- --run`: PASSED (contract tests & 36 TSX interaction audits).
  - `npm run build`: PASSED (TypeScript check, 1,994 modules transformed, initial JS/CSS and 20 lazy route budgets passed).

---

## 5. Final Release Sign-Off
- **Workstream:** `alpha.15 — Microsoft Entra ID, External Identities & Hybrid Authentication`
- **Release Version:** `1.0.0-alpha.15`
- **Sign-off Status:** **`alpha.15 — COMPLETE`**
