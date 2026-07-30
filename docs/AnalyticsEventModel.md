# Analytics event model

Platform Analytics stores append-only operational evidence. An event answers what happened, where, when, who initiated it, which governed configuration applied, and which source execution or resource supports it.

## Event and execution semantics

`EventCount` counts every retained analytics event. `ExecutionCount` counts distinct `SourceExecutionId` values only on terminal execution events. Environment selection, login, configuration, and other operational events therefore increase event count without inflating execution count.

The canonical measures are:

- simulation: `SimulationCompleted` or `SimulationFailed`;
- replay: `ReplayCompleted` or `ReplayFailed`;
- evaluation: `EvaluationCompleted` or `EvaluationFailed`;
- provider invocation: completed, failed, or timed out provider events;
- provider prevention: `ProviderInvocationPrevented`;
- trace: `TraceCompleted`;
- policy: `PolicyEvaluated`;
- plugin operations: lifecycle, compatibility, and health events.

One governed execution can produce several events with the same `SourceExecutionId`, `CorrelationId`, and `ConfigurationRevision`.

## Safe dimensions

Events may contain organisation, workspace, environment, actor and role, capability, event type, outcome, provider/model, token counts, classified ZAR cost, duration, policy/evaluation outcomes, safe source references, configuration revision, and correlation.

Events never contain prompts, customer messages, provider request/response bodies, trace content, credentials, bearer tokens, or resolved secret values. Prompt, workflow, and knowledge fields are asset names used for filtering, not their content.

## Reliability

Operational writes enqueue a deterministic event in `AnalyticsOutbox` within the same database transaction. The worker claims rows with PostgreSQL row locking, inserts by unique event key, records retry state, and marks dirty aggregation ranges. Duplicate dispatch cannot create duplicate events.

Historical Alpha 13 rows are attributed to their former workspace default environment and marked `BackfilledDefaultEnvironment` with configuration revision `legacy:alpha13-unattributed`; they are never presented as original configuration evidence.
