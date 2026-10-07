# ConvoLab Engine Architecture

> Core orchestration, capability boundaries, and execution pipeline for the ConvoLab platform.

---

## 1. Architectural Philosophy

ConvoLab's execution architecture is founded on Domain-Driven Design (DDD) and Clean Architecture:

1. **Separation of Concerns**: Conversation does not select model providers; Workflow does not implement LLM retry logic; Prompt templates do not execute document retrievals; Knowledge does not assemble prompts; Intelligence does not enforce business policies.
2. **Provider Neutrality**: LLM providers (Google Gemini, OpenAI, Claude, local deterministic stubs) are strictly decoupled through domain abstractions (`IIntelligenceProvider`, `ITokenCounter`).
3. **Sealed Execution Units**: Prompts and knowledge packages are sealed and immutable at execution time, enabling deterministic replays and auditability.
4. **Governed Execution Loop**: All AI interactions pass through pre-flight guardrails, policy decisions, PII redaction, execution planning, and post-flight evaluation scoring.

---

## 2. Core Engines & Capability Boundaries

```text
┌─────────────────────────────────────────────────────────────────────────────────┐
│                           ConvoLab Studio (React 19)                            │
└────────────────────────────────────────┬────────────────────────────────────────┘
                                         │ HTTP / REST
┌────────────────────────────────────────▼────────────────────────────────────────┐
│                              ASP.NET Core Platform API                          │
│     (Security Headers, PII Redaction, Request Auth, Rate Limiting, Audit)       │
└────────────────────────────────────────┬────────────────────────────────────────┘
                                         │
┌────────────────────────────────────────▼────────────────────────────────────────┐
│                              Application Orchestration                          │
│                                                                                 │
│   ┌───────────────────────────┐             ┌───────────────────────────────┐   │
│   │    Conversation Engine    │◄───────────►│        Workflow Engine        │   │
│   │   (Sessions, Memory)      │             │  (State Machine, Transitions) │   │
│   └─────────────┬─────────────┘             └───────────────┬───────────────┘   │
│                 │                                           │                   │
│                 ▼                                           ▼                   │
│   ┌───────────────────────────┐             ┌───────────────────────────────┐   │
│   │       Prompt Engine       │             │       Knowledge Engine        │   │
│   │ (Templates, Guardrails)   │             │   (Hybrid RAG 2.0, RRF $k=60$) │   │
│   └─────────────┬─────────────┘             └───────────────┬───────────────┘   │
│                 │                                           │                   │
│                 └─────────────────────┬─────────────────────┘                   │
│                                       │ Sealed Package + Guardrails             │
│                                       ▼                                         │
│                         ┌───────────────────────────┐                           │
│                         │       Policy Center       │                           │
│                         │ (Pre-Flight Rule Check)   │                           │
│                         └─────────────┬─────────────┘                           │
│                                       │ Approved                                │
│                                       ▼                                         │
│                         ┌───────────────────────────┐                           │
│                         │    Intelligence Engine    │                           │
│                         │ (Execution Plan, Budget)  │                           │
│                         └─────────────┬─────────────┘                           │
│                                       │                                         │
│                 ┌─────────────────────┴─────────────────────┐                   │
│                 ▼                                           ▼                   │
│   ┌───────────────────────────┐             ┌───────────────────────────────┐   │
│   │     Evaluation Studio     │             │        Trace Explorer         │   │
│   │  (Quality Scorecards)     │             │ (OpenTelemetry Spans & Events)│   │
│   └───────────────────────────┘             └───────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Capability Matrix

| Engine | Primary Aggregate | Key Responsibilities | Detailed Spec |
|---|---|---|---|
| **Conversation** | `Conversation` | Multi-turn sessions, participants, chronological messages, active context, human handoff triggers | [Conversation Engine Spec](Capability3_ConversationEngine_OperationalBehavior.md) |
| **Workflow** | `WorkflowDefinition` | Visual graph transitions, state machine transitions, execution runs | [Workflow Studio](WorkflowStudio.md) |
| **Prompt** | `PromptTemplate` | Governed templates, variable bindings, version approval lifecycle, pre-execution injection shields | [Prompt Studio](PromptStudio.md) |
| **Knowledge** | `KnowledgeCollection` | Document ingestion, chunking, BM25 + Dense Semantic RRF ($k=60$) hybrid retrieval, sealed packages | [Knowledge Studio](KnowledgeStudio.md) |
| **Intelligence** | `IntelligenceExecution` | Provider routing (Gemini, Local), token budgeting, fallback/retry policies | [Intelligence Center](IntelligenceCenter.md) |
| **Policy** | `PolicySet` | Scoped governance rules, PII redaction enforcement, execution authorization | [Compliance Controls](security/ComplianceControls.md) |
| **Evaluation** | `EvaluationScorecard` | Automated quality scoring, Golden Dataset regression CI/CD, batch evaluations | [Evaluation Studio](EvaluationStudio.md) |
| **Analytics & FinOps** | `AnalyticsEvent` | Append-only audit events, outbox dispatch, ZAR TCO/ROI calculation, spend attribution | [Platform Analytics](PlatformAnalytics.md) |

---

## 3. The Execution Lifecycle

Every conversation turn executes through the following deterministic pipeline:

1. **Ingress & Sanitization**:
   - Inbound message arrives via Studio API (`/api/simulations`) or Omnichannel Webhook (`/api/connectors/infobip/webhook`).
   - `RegexPiiRedactionEngine` identifies and masks PII/PHI (national IDs, credit cards, emails, phone numbers, IBANs), generating surrogate tokens.
2. **Context & State Resolution**:
   - `Conversation` aggregate resolves the session state, active participant personas, and recent history.
   - If human handoff conditions are met (sentiment escalation or keyword triggers), the session transitions to human agent queue.
3. **Knowledge Retrieval**:
   - `HybridKnowledgeRetriever` performs parallel BM25 lexical keyword search and 1024-dimensional dense semantic vector matching.
   - Reciprocal Rank Fusion (RRF $k=60$) merges result sets within the configured token citation budget.
   - A sealed `KnowledgePackage` is generated and pinned.
4. **Prompt Rendering & Guardrails**:
   - The approved `PromptTemplate` is rendered with variables and retrieved context.
   - Pre-flight guardrails scan for prompt injection, jailbreaking, and system prompt leakage.
5. **Policy Verification**:
   - `PolicyCenter` evaluates constraints against the prompt, context, and tenant budget.
6. **Intelligence Execution**:
   - `IntelligenceEngine` constructs an execution plan, enforces token quotas, and dispatches to the configured provider (e.g. Gemini 1.5 Pro).
   - If provider failure occurs, retry or fallback rules execute automatically.
7. **Telemetry, Tracing & Evaluation**:
   - Distributed trace spans are captured via OpenTelemetry.
   - `EvaluationStudio` calculates quality scores across accuracy, relevance, and safety dimensions.
   - Analytics event is enqueued via transactional outbox to update FinOps dashboards (ZAR costs and token counts).
   - Cryptographic audit record is sealed with `AuditHashChain`.

---

## 4. Architectural Decision Records (ADRs)

Key architectural decisions governing this engine topology:

- [ADR 0001: Execution Bounded Context](adr/0001-execution-bounded-context.md)
- [ADR 0002: Centralized Orchestration](adr/0002-centralized-orchestration.md)
- [ADR 0003: Provider Abstractions](adr/0003-provider-abstractions.md)
- [ADR 0004: Distributed Tracing Model](adr/0004-distributed-tracing-model.md)
- [ADR 0006: Separate Execution from AI Orchestration](adr/0006-separate-execution-from-ai-orchestration.md)
- [ADR 0010: Conversation Engine as Central Aggregate](adr/0010-conversation-engine-as-central-aggregate.md)
- [ADR 0011: Prompt Engine Domain Model](adr/0011-prompt-engine-domain-model.md)
