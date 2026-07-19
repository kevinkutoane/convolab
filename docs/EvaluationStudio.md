# Evaluation Studio

Evaluation Studio turns persisted Conversation Simulator quality telemetry into visible quality gates for ConvoLab Studio.

## Capabilities

- Aggregates groundedness, relevance, safety, and weighted overall quality.
- Applies configurable thresholds to every persisted simulation run.
- Displays pass rate, failed gates, seven-day trends, and run-level details.
- Includes a policy sandbox for testing sample scores before changing environment configuration.
- Creates and persists named scorecards with reusable quality thresholds and failure actions.
- Runs policy-sandbox previews against either the environment default or a selected scorecard.
- Links evaluation records back to their originating simulation run.

## Default policy

| Metric | Default threshold |
|---|---:|
| Groundedness | 0.80 |
| Relevance | 0.80 |
| Safety | 0.95 |
| Overall | 0.82 |

Overall score uses a deterministic weighted formula: 40% groundedness, 35% relevance, and 25% safety.

## Environment variables

- `CONVOLAB_EVALUATION_MIN_GROUNDEDNESS`
- `CONVOLAB_EVALUATION_MIN_RELEVANCE`
- `CONVOLAB_EVALUATION_MIN_SAFETY`
- `CONVOLAB_EVALUATION_MIN_OVERALL`
- `CONVOLAB_EVALUATION_FAILURE_ACTION`

All thresholds accept values from 0 to 1. The default failure action is `Review`. Matching `Evaluation` keys in `appsettings.json` provide local defaults.

## API

- `GET /api/evaluation/overview`
- `GET /api/evaluation/runs?limit=100`
- `GET /api/evaluation/scorecards`
- `POST /api/evaluation/scorecards`
- `POST /api/evaluation/preview`

Evaluation Studio v1 evaluates telemetry already stored with simulation runs and persists reusable scorecards. Evaluation suites, human review queues, golden datasets, and model-based evaluators remain later milestones.
