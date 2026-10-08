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

## Configuration & Zero-Secret Architecture

ConvoLab strictly enforces a **zero-secret storage model**: raw API keys are never persisted to PostgreSQL or transmitted to client browsers.

1. **Host Environment Variable**:
   In your root `.env` or container environment:
   ```env
   GEMINI_API_KEY=your_gemini_api_key
   GEMINI_MODEL=gemini-2.5-flash
   CONVOLAB_MONTHLY_AI_BUDGET_ZAR=500
   ```
2. **Secret Reference**:
   In **Workspace Administration** → **Settings** → **API Keys & Secrets** (or **Settings** → **Secrets**), register a reference named `Google Gemini API Key` with URI `env:GEMINI_API_KEY`.
3. **Runtime AI Provider Setting**:
   In **Settings** (`/settings`) → **AI Provider**:
   - `ai.provider`: `Gemini`
   - `ai.secret_reference`: `env:GEMINI_API_KEY`
   - `ai.model`: `gemini-2.5-flash`
   - Use **Validate provider** to perform a cost-free model metadata probe without consuming tokens.
4. **Intelligence Center Health Probe**:
   In **Intelligence Center** (`/intelligence`), the Google Gemini adapter displays status **Ready**. Click **Test connection** (`POST /api/intelligence/providers/Gemini/test`) to verify live latency and authentication.
5. **Execution in Conversation Simulator**:
   In **Conversation Simulator** (`/conversations`), select **Google Gemini** in the Provider dropdown, choose your model (`gemini-2.5-flash`, `gemini-1.5-pro`), and execute live governed simulations.

## Pricing & Currency

New budgets, model prices, estimates, and execution costs use South African rand (ZAR) natively. Gemini pricing is intentionally optional because provider pricing changes independently of ConvoLab releases. When pricing is absent, Intelligence Center reports that cost admission is informational rather than inventing a value. Persisted legacy runs retain their recorded currency and non-ZAR costs are excluded from ZAR totals rather than being converted with an assumed exchange rate.

## Data source

Execution analytics are derived from immutable persisted simulation runs. This keeps the first version consistent with the Trace and Replay roadmap while avoiding a second execution-history store.

## Current limitations

- The monthly budget is environment-configured rather than editable in Studio.
- Provider health is configuration and execution-history based; active polling is only performed when the user selects **Test connection**.
- The provider catalogue includes the current deterministic and Gemini adapters. Future adapters should implement the same normalized configuration contract.
