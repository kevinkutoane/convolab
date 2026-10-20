# Executive Business Case: ConvoLab Conversational AI Platform

## 1. Problem
Enterprise customer engagement teams face surging inquiry volumes across mobile channels (notably WhatsApp) while constrained by high human agent costs and strict data protection laws (POPIA, GDPR). Deploying unmanaged LLMs directly to customers risks PII leakage, hallucinations, regulatory fines, and runaway cloud API expenditures without auditable governance or measurable ROI.

## 2. Who Is Affected
- **Contact Center & Operations Teams**: Overwhelmed by repetitive queries; human handling costs average **R45.00 ZAR** per resolved conversation (coded benchmark in `AnalyticsService.cs`).
- **Information Security & Compliance Officers**: Risk exposure if customer PII (e.g., South African national ID numbers, credit card details, phone numbers) is transmitted unredacted to third-party model providers.
- **Finance & Executive Leadership**: Lack real-time attribution connecting conversational AI compute spend to tangible cost savings in local currency (ZAR).
- **Customers**: Suffer from long wait times on digital channels when automated assistants fail to safely resolve inquiries.

## 3. How ConvoLab Addresses It
ConvoLab provides a governed, provider-agnostic conversational platform that automates high-volume WhatsApp interactions behind automated guardrails:
- **Zero-Secret & PII Masking Pipeline**: Intercepts customer messages, masks sensitive PII before external model dispatch, and verifies policy guardrails.
- **Provider-Independent Intelligence Engine**: Routes traffic across deterministic logic and external LLMs (e.g., Google Gemini Flash) under hard monthly budget caps (`CONVOLAB_MONTHLY_AI_BUDGET_ZAR=500`).
- **Omnichannel Delivery**: Native Infobip WhatsApp adapter supporting text, interactive buttons, list options, and document delivery.
- **Executive FinOps in ZAR**: Quantifies unit economics (cost per resolution, token efficiency, net cost savings against the R45.00 human handling benchmark) with role-based cost visibility.

## 4. Measurable Impact
- **Unit Economics**: AI resolution cost averages **R0.18 ZAR – R0.35 ZAR** per successful conversation (based on verified platform integration tests in `ExecutiveFinOpsTests.cs`), compared to the **R45.00 ZAR** human agent benchmark.
- **Projected Handling Volume**: [TEAM TO FILL: current monthly conversation volume, e.g., 50,000 conversations/month]
- **Estimated Net Savings**: [TEAM TO FILL: e.g., Volume × (R45.00 - R0.35) = projected monthly savings in ZAR]
- **Current Operational Budget**: Default sandbox cap of R500 ZAR/month per workspace.

## 5. End-to-End Operational Scenario
1. **Inbound Ingestion**: A customer sends a WhatsApp query containing an account inquiry and ID number. Infobip delivers the payload to `POST /api/connectors/infobip/webhook`.
2. **PII Masking**: `PiiMaskingProvider` intercepts the message, masking the ID number to `[ID_NUMBER_1]`. The masked payload is logged to the tamper-evident audit store (`POST /api/audit/log-batch`).
3. **Guardrail Evaluation**: `PolicyEvaluator` evaluates the prompt against safety and groundedness policies (`POST /api/policies/evaluate`). Verified in **Policy Center** (`/policies`).
4. **Governed AI Resolution / Handoff**:
   - *Automated Path*: Routed to Gemini Flash (`POST /api/intelligence/plan-preview`). The generated reply is dispatched via `OmnichannelService` using Infobip interactive buttons (`/whatsapp/1/message/interactive/buttons`).
   - *Escalation Path*: If confidence is below threshold, an interactive handoff menu is sent, transferring the session to an agent. Audited in **Trace Explorer** (`/traces`).
5. **Cost & ROI Attribution**: The execution, tokens, and latency are recorded. Executives view live unit economics, cost per resolution, and cumulative savings in **Platform Analytics** → **Executive FinOps** (`GET /api/analytics/overview?view=finops`).

## 6. Stakeholders
- **Executive Sponsor**: [TEAM TO FILL: Name / Role]
- **Product Owner**: [TEAM TO FILL: Name / Role]
- **Head of Customer Care / Contact Center**: [TEAM TO FILL: Name / Role]
- **Chief Information Security Officer (CISO)**: [TEAM TO FILL: Name / Role]

## 7. Assumptions
1. Human handling benchmark is calibrated at R45.00 ZAR per resolved conversation (default system metric).
2. WhatsApp infrastructure connectivity is provisioned via an authorized Infobip WhatsApp Business Account (WABA).
3. Primary AI inference utilizes Google Gemini Flash; enterprise Azure OpenAI / Copilot endpoints can be substituted via `IIntelligenceExecutor`.
4. Live Microsoft Entra authentication tenant is provisioned by enterprise IT prior to production rollout (environment gate).
