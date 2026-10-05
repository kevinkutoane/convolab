# Evaluation Studio & Regression CI/CD

Evaluation Studio turns conversation quality metrics into enforceable enterprise quality gates. It supports automated evaluation of simulation runs, persisted versioned scorecards, test case management, side-by-side comparison, and automated Golden Dataset regression testing in CI/CD pipelines.

---

## Core Capabilities

1. **Quality Gate Metrics:**
   - Groundedness, Relevance, Safety, and Weighted Overall Quality.
   - Deterministic score weighting: default 40% Groundedness, 35% Relevance, 25% Safety.
2. **Versioned Scorecards:**
   - Multi-metric custom thresholds and weights.
   - Immutable published revisions (`Draft` → `Published`).
3. **Test Cases & Batches:**
   - Tagged test scenarios linked to simulation runs and expected verdicts (`Passed`, `Review`, `Failed`).
   - Batch executions evaluating multiple test cases against selected scorecards.
4. **Side-by-Side Comparison:**
   - Evaluates a baseline run against a candidate run, calculating metric deltas and determining whether the candidate is `Improved`, `Equivalent`, or a `Regression`.

---

## Default Quality Policy

| Metric | Default Threshold |
|---|---:|
| Groundedness | 0.80 |
| Relevance | 0.80 |
| Safety | 0.95 |
| Overall | 0.82 |

Thresholds accept values from 0.0 to 1.0. The default failure action is `Review`.

---

## Automated Golden-Dataset Regression Runner (CI/CD)

To ensure that prompt changes, model upgrades, or workflow modifications never degrade conversational quality in production, ConvoLab includes a native regression runner ([`EvaluationRegressionSummaryDto`](file:///c:/Users/W1022804/convolab-main/src/Application/ConvoLab.Application/EvaluationStudio/EvaluationStudioContracts.cs)):

### Pipeline Workflow
1. Tag benchmark customer scenarios as `"Golden"` (or custom tags like `"Production"` or `"Tier1"`).
2. The CI/CD pipeline triggers the automated regression endpoint:
   ```http
   POST /api/evaluations/regression/golden
   Content-Type: application/json

   {
     "tag": "Golden",
     "minPassRateThreshold": 0.90,
     "triggeredBy": "GitHub Actions Release Gate"
   }
   ```
3. The runner executes the entire golden suite against the active published scorecard.
4. If `passRate >= minPassRateThreshold`, `qualityGatePassed` evaluates to `true`; otherwise it fails closed and returns specific regression items.

---

## API Endpoints

- `GET /api/evaluations/overview` — Quality summary and 7-day trend
- `GET /api/evaluations/scorecards` — List scorecards
- `POST /api/evaluations/scorecards` — Create new scorecard
- `POST /api/evaluations/scorecards/{id}/publish` — Publish scorecard revision
- `GET /api/evaluations/runs` — List evaluated runs
- `POST /api/evaluations/runs/evaluate` — Evaluate simulation run
- `POST /api/evaluations/runs/{id}/review` — Human review notes and signoff
- `GET /api/evaluations/compare` — Compare baseline vs candidate
- `GET /api/evaluations/test-cases` — List test cases
- `POST /api/evaluations/test-cases` — Create test case
- `POST /api/evaluations/batches` — Run custom batch
- `POST /api/evaluations/regression/golden` — Execute automated Golden Dataset regression suite
