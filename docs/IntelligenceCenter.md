# Intelligence Center

Intelligence Center is the operational workspace for ConvoLab Studio's provider-neutral Intelligence Engine.

## Purpose

It makes intelligent execution decisions visible without moving orchestration into the browser. The Studio reads normalized API contracts; provider selection, retry, fallback, budget admission, and execution accounting remain platform responsibilities.

## Capabilities

- Provider and model catalogue
- Provider configuration and connection testing
- Declared model capabilities and limits
- Persisted execution history from Conversation Simulator
- Token, cost, latency, success, retry, and fallback analytics
- Monthly budget monitor
- Execution detail inspection
- Pre-execution plan preview and admission decisions

## API

- `GET /api/intelligence/overview`
- `GET /api/intelligence/executions?limit=100`
- `POST /api/intelligence/plan-preview`
- `GET /api/intelligence/providers`
- `POST /api/intelligence/providers/{provider}/test`

## Configuration

```env
GEMINI_API_KEY=
GEMINI_MODEL=gemini-2.5-flash
CONVOLAB_MONTHLY_AI_BUDGET_ZAR=500
GEMINI_INPUT_PRICE_ZAR_PER_1K=
GEMINI_OUTPUT_PRICE_ZAR_PER_1K=
```

New budgets, model prices, estimates, and execution costs use South African rand (ZAR) natively. Gemini pricing is intentionally optional because provider pricing changes independently of ConvoLab releases. When pricing is absent, Intelligence Center reports that cost admission is informational rather than inventing a value. Persisted legacy runs retain their recorded currency and non-ZAR costs are excluded from ZAR totals rather than being converted with an assumed exchange rate.

## Data source

Execution analytics are derived from immutable persisted simulation runs. This keeps the first version consistent with the Trace and Replay roadmap while avoiding a second execution-history store.

## Current limitations

- The monthly budget is environment-configured rather than editable in Studio.
- Provider health is configuration and execution-history based; active polling is only performed when the user selects **Test connection**.
- The provider catalogue includes the current deterministic and Gemini adapters. Future adapters should implement the same normalized configuration contract.
