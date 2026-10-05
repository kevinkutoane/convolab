# Platform Analytics & Executive FinOps

Platform Analytics provides workspace- and environment-scoped operational evidence for ConvoLab. It is metadata-only: prompts, messages, provider payloads, trace artifacts, secret references, and customer content are never copied into analytics events or exports.

---

## Trusted Attribution

Execution-producing requests resolve a runtime environment after authentication. Clients may send `X-ConvoLab-Environment-Id`; when omitted, the active default environment is used. The API returns `X-ConvoLab-Resolved-Environment-Id`. Invalid, foreign, inactive, and unavailable-default environments are rejected before execution.

The API generates the authoritative `X-Correlation-ID`. A client-supplied value is retained only as `X-Parent-Correlation-ID`.

Each new simulation run stores:
- an immutable, secret-free configuration snapshot with a content-derived SHA-256 revision;
- associated execution attribution for organisation, workspace, environment, actor, role, configuration, and correlation;
- a deterministic, safe analytics event in the transactional outbox.

---

## Executive FinOps & AI Cost Attribution Engine

ConvoLab features an executive-tier FinOps engine ([`ExecutiveFinOpsSummaryDto`](../src/Application/ConvoLab.Application/Analytics/AnalyticsContracts.cs)) designed for CIOs, CFOs, and AI platform leaders to track financial ROI, unit economics, and spend burn rate:

### 1. Human Parity & ROI Economics
- **Human Benchmark Comparison:** Evaluates AI automated resolutions against human agent contact center cost benchmarks (configurable, default: `R45.00 ZAR` per resolved conversation).
- **Equivalent Human Handling Cost:**
  $$\text{EquivalentHumanCost} = \text{SucceededExecutions} \times \text{BenchmarkCostPerResolution}$$
- **Net Cost Savings:**
  $$\text{EstimatedCostSavings} = \max(0, \text{EquivalentHumanCost} - \text{TotalAiCost})$$
- **Return on Investment (ROI):**
  $$\text{ROI} = \left(\frac{\text{EstimatedCostSavings}}{\text{TotalAiCost}}\right) \times 100\%$$

### 2. Unit Economics
- **Cost per Resolution:** $\frac{\text{TotalAiCost}}{\text{SucceededExecutions}}$
- **Cost per 1,000 Tokens:** $\frac{\text{TotalAiCost}}{\text{TotalTokens}} \times 1000$

### 3. Multi-Dimensional Spend Attribution
Attribution breaks down spend and token consumption across three axes:
- **By Provider:** Gemini, Azure OpenAI, Anthropic, Mock
- **By Model:** `gemini-2.5-flash`, `gpt-4o-mini`, etc.
- **By Capability:** Chat, Workflow, Knowledge RAG, Redaction

### 4. Budget Run-Rate & Projections
- Monthly budget limit (`SettingKeys.MonthlyBudgetZar`)
- Month-to-date spend & linear run-rate projection to month-end:
  $$\text{ProjectedMonthEnd} = \frac{\text{MonthToDateSpend}}{\text{DaysElapsed}} \times \text{DaysInMonth}$$
- Budget Health Status: `Healthy` (<80%), `Warning` (80–100%), `Critical` (>100% or projected overrun), `Uncapped`.

### 5. Actionable FinOps Recommendations
Dynamically generated recommendations detect:
- Projected monthly budget overruns with mitigation guidance
- Unattributed invocations due to missing provider rate cards
- High cost-per-token invocations suitable for routing to lighter model tiers (e.g. Gemini Flash) or prompt caching.

---

## API & Endpoints

Rooted at `/api/workspaces/{workspaceId}/analytics`:
- `GET .../finops` — Full Executive FinOps summary (TCO, ROI, Attributions, Recommendations). Protected by `ViewCostAnalytics`.
- `GET .../finops-dashboard` — Category dashboard representation for chart widgets.
- `GET .../overview` — Executive summary measures.
- `GET .../cost` — Granular provider cost data.
- `GET .../budget` — Monthly budget tracking.
- `GET .../usage` — Operational activity counts.
- `GET .../quality` — Quality gate scores.
- `GET .../governance` — Policy and guardrail decisions.
- `GET .../performance` — Latency and duration metrics.
- `GET .../adoption` — Active actor engagement.
- `GET .../events` — Paginated safe raw events.

---

## Cost Semantics

All monetary values use ZAR:
- `Actual`: explicitly reported billed cost.
- `Estimated`: `inputTokens / 1000 × inputPrice + outputTokens / 1000 × outputPrice`.
- `Unavailable`: token or pricing evidence is incomplete; it is never represented as zero.
- Policy-prevented provider calls record explicit zero usage/cost with `ProviderInvocationPrevented=true`.
