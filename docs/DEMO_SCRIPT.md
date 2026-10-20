# ConvoLab Hackathon Demo Script (5-Minute Runbook)

Mapped to hackathon judging criteria: Business Value, Technical Execution, Production Readiness, and Responsible AI.

---

## Timeline Overview

| Timestamp | Duration | Section | Focus & Judging Criteria |
|---|---|---|---|
| **0:00 – 1:00** | 1.0 min | **1. Problem & Business Context** | Business impact, R45 human benchmark vs R0.18 AI cost, compliance risk |
| **1:00 – 3:30** | 2.5 min | **2. Live End-to-End Walkthrough** | WhatsApp flow, PII masking, Policy guardrail, Studio UI, Executive FinOps |
| **3:30 – 4:30** | 1.0 min | **3. Production Readiness & Security** | Clean Architecture, 553 tests, zero-secret model, audit hash chain, fast rollback |
| **4:30 – 5:00** | 0.5 min | **4. Responsible AI & Next Steps** | Approved AI tooling register, known gaps, roadmap (embeddings, Azure OpenAI) |

---

## Detailed Script & Speaker Notes

### 1. Problem & Business Context (0:00 – 1:00)
- **Speaker:** 
  > "Good morning/afternoon judges. Enterprise contact centers are inundated with millions of digital inquiries—especially across WhatsApp. Today, human agent handling costs average **R45.00 ZAR** per resolved conversation.
  > 
  > Direct-to-consumer LLMs promise automation but introduce severe enterprise risks: unmasked customer PII violating POPIA and GDPR, unchecked hallucinations, and uncontrolled cloud API costs without financial attribution.
  > 
  > We built **ConvoLab**: an enterprise-grade, provider-agnostic conversational AI platform that pairs automated PII masking, cryptographic audit logging, policy guardrails, and real-time FinOps in South African Rand (ZAR) with native WhatsApp omnichannel delivery."

---

### 2. Live End-to-End Walkthrough (1:00 – 3:30)
*Action: Screen share browser with ConvoLab Studio at `http://localhost:3000`.*

1. **Inbound Simulation & PII Masking (1:00 – 1:45)**:
   - Navigate to **Conversation Simulator** (`/conversations`).
   - Enter a realistic customer query containing a South African ID number: `"Hi, my name is John Doe, ID number 8501015009087. Can you check my account balance?"`
   - *Point out:* The system intercepts the message and passes it through `PiiDataSanitizer`.
   - Open **Trace Explorer** (`/traces`): Show that the raw ID is masked to `[ID_NUMBER_1]` before the prompt touches the model or persistent logs.

2. **Policy Guardrails & Omnichannel Delivery (1:45 – 2:30)**:
   - Navigate to **Policy Center** (`/policies`): Show active evaluation gates (injection prevention, safety threshold 0.95, groundedness 0.80).
   - Show how the governed response triggers an interactive WhatsApp message (`/whatsapp/1/message/interactive/buttons`) via Infobip with quick action buttons ("View Statement", "Transfer Agent").
   - Mention human escalation: If safety or confidence falls below threshold, the session automatically dispatches an escalation menu.

3. **Executive FinOps in ZAR (2:30 – 3:30)**:
   - Navigate to **Platform Analytics** → **Executive FinOps** (`/analytics?view=finops`).
   - *Highlight metrics:*
     - **Cost per Resolution:** e.g., R0.18 ZAR vs. the R45.00 human benchmark.
     - **Net Cost Savings:** Real-time ZAR savings calculated directly from successful resolutions.
     - **Budget Cap Guardrail:** Enforces monthly workspace budget limit (e.g., R500 ZAR) with automated admission rejection on overspend.

---

### 3. Production Readiness & Security (3:30 – 4:30)
*Action: Switch to IDE / Terminal showing tests and docs.*

- **Speaker:**
  > "ConvoLab is built for day-one enterprise deployment, not just a hackathon demo:
  > 1. **Verified Test Quality:** 100% passing test suite across 5 test projects (**553 tests passed**, 0 failed).
  > 2. **Zero-Secret Model:** Raw secrets are never stored in the database or client state; they are injected via memory references (`env:GEMINI_API_KEY`) via `ISecretStore`.
  > 3. **Tamper-Evident Audit Chain:** All security, prompt, and membership events form an immutable SHA-256 hash chain.
  > 4. **Resilience & ALM:** Automated pre-migration database backup gates, AES-256-GCM encrypted snapshots, and a verified 15-to-24 second container rollback pipeline."

---

### 4. Responsible AI & Next Steps (4:30 – 5:00)
- **Speaker:**
  > "We hold ourselves to rigorous AI governance:
  > - All AI tools used during development are documented with human verification in our [`docs/AI_USAGE.md`](AI_USAGE.md) register.
  > - Transparent known gaps: Our retrieval currently uses high-speed FNV-1a hash clustering with BM25 Reciprocal Rank Fusion; our next milestone plugs in learned dense embeddings (`text-embedding-004`) and an Azure OpenAI / Copilot executor via our provider-independent `IIntelligenceExecutor` interface.
  > 
  > Thank you! We are ready for your questions."

---

## Contingency & Fallback Plan

If live execution encounters connectivity or infrastructure hurdles during the live pitch:

| Failure Scenario | Immediate Action | Fallback Asset |
|---|---|---|
| **Local Docker container failure** | Run frontend dev server against test mocks: `npm run dev` in `web/` | Pre-seeded mock state in `web/src/data/platform.ts` and `apiClient.ts` |
| **External LLM latency / quota limit** | Switch model provider in Conversation Simulator to **Internal Deterministic** adapter (`[PROVIDER:Internal]`) | Zero-latency, zero-cost deterministic responses pre-configured in `DeterministicIntelligenceExecutor.cs` |
| **Infobip API network timeout** | Use the built-in Conversation Simulator trace view | Demonstrates outbound payload serialization and interactive button schemas without live HTTP egress |
| **Entra ID authentication bypass** | Use local admin bootstrap login (`admin@example.com` / `ReplaceWithStrongPassword123!`) | Verified local JWT authentication handler with full administrative permissions |
