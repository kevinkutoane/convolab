# ConvoLab Functional Simulator v2

## Delivered

- SQLite-backed persistent simulation store using the existing Infrastructure DbContext.
- Automatic database creation on API startup.
- Provider-aware execution requests and replay settings.
- Deterministic and Google Gemini execution adapters behind `IIntelligenceExecutor`.
- Routing adapter that prevents provider SDK or wire-format leakage into Application and Domain.
- Provider catalogue and provider connection-test API.
- Studio controls for provider, model, temperature, and maximum output tokens.
- Browser-safe configuration: Gemini credentials remain on the API host.
- Existing prompt, knowledge, execution, evaluation, and trace inspection retained.

## Environment

Set these before starting the API to enable live Gemini execution:

```text
GEMINI_API_KEY=<your-key>
GEMINI_MODEL=gemini-2.5-flash
```

Without a key, ConvoLab remains fully usable through its deterministic provider.

## API additions

- `GET /api/intelligence/providers`
- `POST /api/intelligence/providers/{provider}/test`

Simulation message and replay requests now accept:

- `provider`
- `model`
- `temperature`
- `maxOutputTokens`

## Persistence

The default SQLite file is `convolab.db`. Complete simulation snapshots are serialized into a governed persistence record, preserving messages, runs, prompts, knowledge packages, execution plans, metrics, evaluations, and timelines across API restarts.

## Validation

- Studio TypeScript build: passed
- Vite production build: passed
- ESLint: passed
- .NET build: not executed because the current environment does not include the .NET SDK
