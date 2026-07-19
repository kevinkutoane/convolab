# Functional Conversation Simulator v1 — Implementation Report

## Delivered

ConvoLab Studio now contains its first working product vertical slice. The simulator exercises the platform from the React workspace through the ASP.NET Core API and the provider-neutral Intelligence Engine.

### Runtime flow

1. Create a simulation with a workflow, prompt version, and knowledge collection.
2. Send a customer message.
3. Build a governed deterministic knowledge package with citations.
4. Render a versioned prompt artifact.
5. Ask the Intelligence Engine to plan provider/model execution.
6. Execute through a local deterministic provider adapter.
7. Capture token usage, cost, attempts, fallback usage, and latency.
8. Produce a lightweight evaluation.
9. Record the cross-capability trace timeline.
10. Replay the same user message under a normal, retry-once, or fallback scenario.

## API surface

- `GET /api/simulations/options`
- `GET /api/simulations`
- `GET /api/simulations/{simulationId}`
- `POST /api/simulations`
- `POST /api/simulations/{simulationId}/messages`
- `POST /api/simulations/{simulationId}/replay`

The simulator uses in-memory state intentionally. No database or provider API key is required.

## Deterministic provider scenarios

- **Normal:** the primary local model completes successfully.
- **Retry once:** the first attempt throws a timeout and the Intelligence Engine retry policy completes the second attempt.
- **Fallback:** the primary model rejects execution and the planned fallback model completes the request.

These scenarios prove retry and fallback behavior without introducing provider SDKs into Domain or Application contracts.

## Studio experience

The Conversation Simulator includes:

- simulation creation and selection;
- customer and assistant message history;
- workflow, prompt, and knowledge configuration;
- live pending execution state;
- execution plan and actual telemetry;
- token and cost reporting;
- evaluation scores;
- knowledge citations and sealed package details;
- rendered prompt inspection and copy action;
- cross-capability trace timeline;
- replay against alternative deterministic failure scenarios.

## Validation completed

- `npm ci` — passed
- `npm run build` — passed
- `npm run lint` — passed

The current execution environment does not include the .NET SDK, so the .NET solution could not be compiled locally. GitHub Actions remains configured to restore, build, and test Platform Core using .NET 8.

## Run locally

Terminal 1:

```powershell
dotnet run --project src/Api/ConvoLab.Api/ConvoLab.Api.csproj
```

Terminal 2:

```powershell
cd web
npm install
npm run dev
```

Open `http://localhost:3000/conversations`.
