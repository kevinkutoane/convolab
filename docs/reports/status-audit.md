# Platform Feature Status Audit

**Audit Date:** 2026-10-20  
**Target:** `web/src/data/platform.ts` (39 entries across Capabilities, Navigation Items, and Studio Pages)  
**Reference Standard:** [`RELEASES.md`](../../RELEASES.md)  
**Rule:** Only mark `stable` where there is both a working UI and verified test coverage. Mark `active` where features are working and accessible but evolving or missing secondary capabilities. Mark `foundation` where features are scaffolded or partially implemented without user-facing completeness. Mark `planned` where development has not started.

---

## 1. Status Definitions Summary

- **`stable` (Green)**: The feature is fully implemented, thoroughly tested, and production-ready. It has no known major bugs, and its API contracts are not expected to have breaking changes without version bumps. Requires **both** working UI and test coverage.
- **`active` (Blue)**: The feature is working and accessible, but it may still be evolving. It might be missing secondary capabilities or its API might change. Used for new features that are "live" but not yet complete.
- **`foundation` (Purple)**: The feature is scaffolded or partially implemented, but it is not yet user-facing complete. It may be missing a UI, or the UI may be incomplete.
- **`planned` (Grey)**: The feature is on the roadmap but development has not yet begun.

---

## 2. Capabilities Audit (`designTimePlatformStatus.capabilities`)

| # | Capability ID | Name | Audited Status | Backend Endpoints & Domain | UI Implementation | Test Coverage | Audit Rationale & Evidence |
|---|---|---|---|---|---|---|---|
| 1 | `conversation` | Conversation Engine | **`stable`** | `POST /api/simulations/run`, `GET /api/simulations/{id}`, `Conversation`, `Message`, `Participant` | [`ConversationSimulatorPage.tsx`](../../web/src/pages/ConversationSimulatorPage.tsx) | `ConversationAggregateTests.cs`, `SimulationStoreTests.cs`, `EfConversationSimulationStoreTests.cs` | End-to-end multi-turn simulation runner, interactive timeline, memory snapshots, and full test suite. |
| 2 | `workflow` | Workflow Engine | **`stable`** | `GET/POST /api/workflows`, `POST /api/workflows/{id}/versions`, `WorkflowDefinition`, `WorkflowVersion` | [`WorkflowDesignerPage.tsx`](../../web/src/pages/WorkflowDesignerPage.tsx) | `WorkflowDefinitionTests.cs`, `WorkflowStudioServiceTests.cs`, `SqliteRepositoryQueryTests.cs` | Interactive visual graph editor (nodes, transitions, validation, publishing) and unit tests for graph invariants, cycles, and deprecation. |
| 3 | `prompt` | Prompt Engine | **`stable`** | `GET/POST /api/prompts`, `POST /api/prompts/{id}/versions`, `PromptTemplate`, `PromptVersion` | [`PromptStudioPage.tsx`](../../web/src/pages/PromptStudioPage.tsx) | `PromptAggregateTests.cs`, `PromptStudioServiceTests.cs`, `CleanArchitectureTests.cs`, `PromptEngineArchitectureTests.cs` | Version-controlled prompt IDE with typed sections, template variables, approval workflow, token estimation preview, and diffing. |
| 4 | `knowledge` | Knowledge Engine | **`active`** | `GET/POST /api/knowledge/collections`, `/documents`, `HybridKnowledgeRetriever.cs` | [`KnowledgeStudioPage.tsx`](../../web/src/pages/KnowledgeStudioPage.tsx) | `HybridKnowledgeRetrieverTests.cs`, `KnowledgeInfrastructureTests.cs`, `KnowledgeStudioServiceTests.cs` | Working UI for collection management, upload, chunk inspection, and retrieval test. Uses 1024-dim FNV-1a hash clustering rather than learned neural embeddings, and enterprise connectors (SharePoint, Confluence) remain roadmap items. |
| 5 | `intelligence` | Intelligence Engine | **`active`** | `GET /api/intelligence/overview`, `/executions`, `POST /api/intelligence/plan-preview`, `/providers/{p}/test` | [`IntelligenceCenterPage.tsx`](../../web/src/pages/IntelligenceCenterPage.tsx) | `ExecutionPlannerTests.cs`, `ExecutionRequestTests.cs`, `IntelligenceStudioServiceTests.cs`, `ApiContractTests.cs` | Working UI for model catalogue, plan previewer, provider testing, and budget tracking. However, provider ecosystem currently implements only Gemini and Deterministic local provider; multi-provider integration (Azure OpenAI, Copilot) is evolving. |
| 6 | `policy` | Policy Engine | **`stable`** | `GET/POST /api/policies`, `POST /api/policies/evaluate`, `PolicyDefinition` | [`PolicyCenterPage.tsx`](../../web/src/pages/PolicyCenterPage.tsx) | `PolicyDefinitionTests.cs`, `PolicyStudioServiceTests.cs`, `PromptGuardrailEngineTests.cs` | Scoped governance rules, PII redaction rules, provider/model/budget limits, test evaluation simulator, and verified domain tests. |
| 7 | `evaluation` | Evaluation Capability | **`stable`** | `GET/POST /api/evaluation/scorecards`, `POST /api/evaluation/batches/run`, `POST /api/evaluation/runs/{id}/review` | [`EvaluationStudioPage.tsx`](../../web/src/pages/EvaluationStudioPage.tsx) | `EvaluationStudioServiceTests.cs`, `ExpandedEvaluationStudioServiceTests.cs` | Scorecard management, test case authoring, batch runs, Golden Dataset CI/CD, review, and run comparison diffing. |
| 8 | `tracing` | Tracing Capability | **`stable`** | `GET /api/traces`, `GET /api/traces/{id}`, `Trace`, `Span` | [`TraceExplorerPage.tsx`](../../web/src/pages/TraceExplorerPage.tsx) | `TraceStudioServiceTests.cs` | OpenTelemetry-aligned distributed trace explorer with spans waterfall, events timeline, artifacts inspection, sensitive toggle, and replay link. |
| 9 | `replay` | Replay Studio | **`stable`** | `GET/POST /api/replay/experiments`, `POST /api/replay/experiments/{id}/candidates` | [`ReplayStudioPage.tsx`](../../web/src/pages/ReplayStudioPage.tsx) | `ReplayExperimentTests.cs`, `ReplayStudioServiceTests.cs` | Experiment creation from simulation snapshots, parameter variations (prompt, model, temp, knowledge), multi-candidate execution, and comparison. |
| 10 | `plugins` | Plugin Engine | **`active`** | `GET/POST /api/plugins`, `POST /api/plugins/{id}/health`, `Plugin` | [`PluginCenterPage.tsx`](../../web/src/pages/PluginCenterPage.tsx) | `PluginTests.cs`, `PluginStudioServiceTests.cs`, `PluginHealthProbeTests.cs` | Extension registry, categories, lifecycle state transitions, and health check probes are fully working. However, secure sandboxed execution runtime (WASM/container isolation) is documented as an in-progress foundation. |
| 11 | `workspace-identity` | Workspace, Identity & Access | **`active`** | `GET/POST /api/workspaces`, `/members`, `/service-accounts`, `/auth/login` | [`WorkspacePage.tsx`](../../web/src/pages/WorkspacePage.tsx) | `WorkspaceIdentityTests.cs`, `AuthenticationRegressionTests.cs`, `BreakGlassAuthenticationTests.cs`, `MockEntraOidcTests.cs` | Local authentication, session cookies, RBAC, service accounts, and audit logging are complete. Live Microsoft Entra ID enterprise tenant federation is environment-gated, and SCIM provisioning is deferred. |
| 12 | `analytics` | Platform Analytics | **`stable`** | `GET /api/analytics/dashboard`, `/events`, `/exports`, `POST /api/analytics/exports` | [`AnalyticsPage.tsx`](../../web/src/pages/AnalyticsPage.tsx) | `AnalyticsTests.cs`, `ExecutiveFinOpsTests.cs`, `AnalyticsOperationalEvidenceTests.cs` | 9 role-gated tabs (Overview, Usage, Cost in ZAR, Quality, Governance, Performance, Adoption, Events, Exports), event drill-down, CSV export, PostgreSQL-verified. |
| 13 | `studio` | ConvoLab Studio | **`stable`** | Single Page Application shell, `/api/platform/status` | React 19 Shell, Theme, Navigation, HelpDrawer | Browser interaction audit (39 TSX files), contract test suite | Dark-first responsive layout, accessible navigation, drawer documentation, error boundaries, clean production bundle. |

---

## 3. Navigation Items Audit (`navigationItems`)

| # | Item Label | Path | Audited Status | UI Page | Backing Endpoints | Tests | Audit Finding |
|---|---|---|---|---|---|---|---|
| 14 | Conversation Simulator | `/conversations` | **`stable`** | `ConversationSimulatorPage.tsx` | `/api/simulations` | `SimulationEndpointTests.cs` | Interactive simulation runner with multi-turn persona execution. |
| 15 | Workflow Designer | `/workflows` | **`stable`** | `WorkflowDesignerPage.tsx` | `/api/workflows` | `WorkflowStudioServiceTests.cs` | Full visual drag-and-drop workflow canvas and graph validation. |
| 16 | Prompt Studio | `/prompts` | **`stable`** | `PromptStudioPage.tsx` | `/api/prompts` | `PromptStudioServiceTests.cs` | Complete prompt authoring, approval lifecycle, and version comparison. |
| 17 | Knowledge Studio | `/knowledge` | **`active`** | `KnowledgeStudioPage.tsx` | `/api/knowledge` | `KnowledgeStudioServiceTests.cs` | Active retrieval testing and chunk inspection; hash-based vectors rather than learned embeddings. |
| 18 | Intelligence Center | `/intelligence` | **`active`** | `IntelligenceCenterPage.tsx` | `/api/intelligence` | `IntelligenceStudioServiceTests.cs` | Active execution plan previewer and provider tester; single external provider (Gemini). |
| 19 | Policy Center | `/policies` | **`stable`** | `PolicyCenterPage.tsx` | `/api/policies` | `PolicyStudioServiceTests.cs` | Governance rule authoring and policy simulation testing. |
| 20 | Evaluation Studio | `/evaluation` | **`stable`** | `EvaluationStudioPage.tsx` | `/api/evaluation` | `EvaluationStudioServiceTests.cs` | Automated scorecard quality gates, Golden Dataset CI/CD, and run comparisons. |
| 21 | Trace Explorer | `/traces` | **`stable`** | `TraceExplorerPage.tsx` | `/api/traces` | `TraceStudioServiceTests.cs` | Distributed trace search, span waterfall, and unmasking toggle. |
| 22 | Replay Studio | `/replay` | **`stable`** | `ReplayStudioPage.tsx` | `/api/replay` | `ReplayStudioServiceTests.cs` | Snapshot-based regression experiments and side-by-side evaluation. |
| 23 | Plugin Center | `/plugins` | **`active`** | `PluginCenterPage.tsx` | `/api/plugins` | `PluginStudioServiceTests.cs` | Working registry and health probes; sandboxed execution runtime deferred. |
| 24 | Workspace & Access | `/workspace` | **`active`** | `WorkspacePage.tsx` | `/api/workspaces`, `/auth` | `WorkspaceIdentityTests.cs` | Working local auth, RBAC, service accounts, and audit; live Entra ID environment-gated. |
| 25 | Analytics | `/analytics` | **`stable`** | `AnalyticsPage.tsx` | `/api/analytics` | `ExecutiveFinOpsTests.cs` | Full 9-tab analytics dashboard, ZAR FinOps calculations, and event log. |
| 26 | Settings | `/settings` | **`stable`** | `SettingsPage.tsx` | `/api/settings` | `OperationalSecretStoreTests.cs` | Environment management, secret references, configuration import/export. |
| 27 | Operations | `/operations` | **`stable`** | `OperationsPage.tsx` | `/api/operations` | `DeploymentAlmTests.cs`, `BackupExecutorIntegrationTests.cs` | Multi-environment promotion, encrypted backups, recovery, and safe mode. |

---

## 4. Studio Page Definitions Audit (`studioPages`)

| # | Page Key | Title | Audited Status | Matched Route | Associated Capability | Audit Finding |
|---|---|---|---|---|---|---|
| 28 | `conversations` | Conversation Explorer | **`stable`** | `/conversations` | Conversation Engine | Full interactive execution UI and domain test coverage. |
| 29 | `workflows` | Workflow Designer | **`stable`** | `/workflows` | Workflow Engine | Visual canvas designer with validated state transitions. |
| 30 | `prompts` | Prompt Studio | **`stable`** | `/prompts` | Prompt Engine | Governed versioned prompt asset authoring and approval. |
| 31 | `knowledge` | Knowledge Studio | **`active`** | `/knowledge` | Knowledge Engine | Working chunking and RRF retrieval; hash-based vectors. |
| 32 | `intelligence` | Intelligence Center | **`active`** | `/intelligence` | Intelligence Engine | Working plan previewer and execution history; Gemini-only. |
| 33 | `policies` | Policy Center | **`stable`** | `/policies` | Policy Capability | Centralized governance rules and interactive policy evaluation. |
| 34 | `evaluations` | Evaluation Studio | **`stable`** | `/evaluation` | Evaluation Capability | Scorecards, quality thresholds, batch runs, and comparison. |
| 35 | `traces` | Trace Explorer | **`stable`** | `/traces` | Tracing Capability | Trace inspection, spans waterfall, and artifacts view. |
| 36 | `replay` | Replay Studio | **`stable`** | `/replay` | Replay Studio | Controlled re-execution and candidate comparison. |
| 37 | `plugins` | Plugin Center | **`active`** | `/plugins` | Plugin Engine | Registry and health probes; sandboxed execution deferred. |
| 38 | `analytics` | Platform Analytics | **`stable`** | `/analytics` | Platform Analytics | ZAR TCO/ROI, multi-dimensional attribution, and evidence. |
| 39 | `settings` | Studio Settings | **`stable`** | `/settings` | Settings & Operations | Environment configuration, secret references, and change audit. |

---

## 5. Audit Summary Statistics

- **Total Items Audited:** 39
- **Stable Items:** 28 (71.8%)
- **Active Items:** 11 (28.2%) — Knowledge Engine (3), Intelligence Engine (3), Plugin Engine (3), Workspace & Access (2)
- **Foundation Items:** 0 (All audited components have working user-facing UIs)
- **Planned Items:** 0 (All 39 entries have active code and UI)

Every item marked `stable` has been verified to have both an operational user interface and automated test coverage across domain, application, and integration test suites.
