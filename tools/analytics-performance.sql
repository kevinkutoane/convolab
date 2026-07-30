\set ON_ERROR_STOP on
\timing on
\pset pager off

BEGIN;

CREATE TEMP TABLE benchmark_scope AS
SELECT
    e."OrganisationId",
    e."WorkspaceId",
    e."Id" AS "EnvironmentId"
FROM "RuntimeEnvironments" e
ORDER BY e."CreatedAt"
LIMIT 1;

\echo 'Seeding 100,000 transaction-local analytics events'
INSERT INTO "AnalyticsEvents" (
    "Id",
    "EventKey",
    "OrganisationId",
    "WorkspaceId",
    "EnvironmentId",
    "ActorId",
    "ActorType",
    "ActorRole",
    "Capability",
    "EventType",
    "Outcome",
    "Provider",
    "Model",
    "InputTokens",
    "OutputTokens",
    "CostZar",
    "CostType",
    "PricingRevision",
    "DurationMs",
    "QualityScore",
    "ProviderInvocationPrevented",
    "SourceExecutionId",
    "SourceType",
    "SourceId",
    "PromptName",
    "WorkflowName",
    "KnowledgeCollectionName",
    "PolicyOutcome",
    "EvaluationOutcome",
    "Groundedness",
    "Relevance",
    "Safety",
    "OverallQuality",
    "ConfigurationRevision",
    "CorrelationId",
    "OccurredAt")
SELECT
    (substr(md5('benchmark-event-id-' || n), 1, 8) || '-' ||
     substr(md5('benchmark-event-id-' || n), 9, 4) || '-' ||
     substr(md5('benchmark-event-id-' || n), 13, 4) || '-' ||
     substr(md5('benchmark-event-id-' || n), 17, 4) || '-' ||
     substr(md5('benchmark-event-id-' || n), 21, 12))::uuid,
    md5('benchmark-event-key-' || n),
    s."OrganisationId",
    s."WorkspaceId",
    s."EnvironmentId",
    CASE WHEN n % 11 = 0 THEN NULL ELSE
        (substr(md5('benchmark-actor-' || (n % 37)), 1, 8) || '-' ||
         substr(md5('benchmark-actor-' || (n % 37)), 9, 4) || '-' ||
         substr(md5('benchmark-actor-' || (n % 37)), 13, 4) || '-' ||
         substr(md5('benchmark-actor-' || (n % 37)), 17, 4) || '-' ||
         substr(md5('benchmark-actor-' || (n % 37)), 21, 12))::uuid
    END,
    CASE WHEN n % 11 = 0 THEN 'System' ELSE 'User' END,
    CASE n % 4 WHEN 0 THEN 'Administrator' WHEN 1 THEN 'Engineer' WHEN 2 THEN 'Reviewer' ELSE 'Operator' END,
    CASE n % 8
        WHEN 0 THEN 'Simulation'
        WHEN 1 THEN 'Provider'
        WHEN 2 THEN 'Evaluation'
        WHEN 3 THEN 'Trace'
        WHEN 4 THEN 'Replay'
        WHEN 5 THEN 'Policy'
        WHEN 6 THEN 'Plugin'
        ELSE 'Environment'
    END,
    CASE n % 8
        WHEN 0 THEN 'SimulationCompleted'
        WHEN 1 THEN 'ProviderInvocationCompleted'
        WHEN 2 THEN 'EvaluationCompleted'
        WHEN 3 THEN 'TraceCompleted'
        WHEN 4 THEN 'ReplayCompleted'
        WHEN 5 THEN 'PolicyEvaluated'
        WHEN 6 THEN 'PluginHealthChecked'
        ELSE 'EnvironmentSelected'
    END,
    CASE WHEN n % 29 = 0 THEN 'Failed' WHEN n % 17 = 0 THEN 'Denied' ELSE 'Succeeded' END,
    CASE WHEN n % 8 IN (0, 1, 2, 4) THEN 'ConvoLab Deterministic' ELSE NULL END,
    CASE WHEN n % 8 IN (0, 1, 2, 4) THEN 'deterministic-v1' ELSE NULL END,
    CASE WHEN n % 8 = 1 THEN 80 + (n % 240) ELSE NULL END,
    CASE WHEN n % 8 = 1 THEN 30 + (n % 120) ELSE NULL END,
    CASE WHEN n % 8 = 1 THEN round((0.002 + ((n % 9)::numeric / 1000)), 6) ELSE NULL END,
    CASE WHEN n % 8 = 1 AND n % 3 = 0 THEN 'Actual'
         WHEN n % 8 = 1 THEN 'Estimated'
         ELSE 'Unavailable'
    END,
    CASE WHEN n % 8 = 1 THEN 'benchmark-pricing-v1' ELSE NULL END,
    CASE WHEN n % 8 IN (0, 1, 2, 4) THEN 35 + (n % 1900) ELSE NULL END,
    CASE WHEN n % 8 = 2 THEN 0.70 + ((n % 30)::double precision / 100) ELSE NULL END,
    n % 17 = 0,
    (substr(md5('benchmark-execution-' || ((n - 1) / 8)), 1, 8) || '-' ||
     substr(md5('benchmark-execution-' || ((n - 1) / 8)), 9, 4) || '-' ||
     substr(md5('benchmark-execution-' || ((n - 1) / 8)), 13, 4) || '-' ||
     substr(md5('benchmark-execution-' || ((n - 1) / 8)), 17, 4) || '-' ||
     substr(md5('benchmark-execution-' || ((n - 1) / 8)), 21, 12))::uuid,
    'SimulationRun',
    (substr(md5('benchmark-source-' || ((n - 1) / 8)), 1, 8) || '-' ||
     substr(md5('benchmark-source-' || ((n - 1) / 8)), 9, 4) || '-' ||
     substr(md5('benchmark-source-' || ((n - 1) / 8)), 13, 4) || '-' ||
     substr(md5('benchmark-source-' || ((n - 1) / 8)), 17, 4) || '-' ||
     substr(md5('benchmark-source-' || ((n - 1) / 8)), 21, 12))::uuid,
    'Benchmark prompt ' || (n % 20),
    'Benchmark workflow ' || (n % 12),
    'Benchmark knowledge ' || (n % 8),
    CASE WHEN n % 8 = 5 AND n % 17 = 0 THEN 'Denied'
         WHEN n % 8 = 5 THEN 'Allowed'
         ELSE NULL
    END,
    CASE WHEN n % 8 = 2 AND n % 29 = 0 THEN 'Failed'
         WHEN n % 8 = 2 THEN 'Passed'
         ELSE NULL
    END,
    CASE WHEN n % 8 = 2 THEN 0.72 + ((n % 27)::double precision / 100) ELSE NULL END,
    CASE WHEN n % 8 = 2 THEN 0.74 + ((n % 25)::double precision / 100) ELSE NULL END,
    CASE WHEN n % 8 = 2 THEN 0.90 + ((n % 10)::double precision / 100) ELSE NULL END,
    CASE WHEN n % 8 = 2 THEN 0.70 + ((n % 30)::double precision / 100) ELSE NULL END,
    'benchmark-config-' || (n % 5),
    'benchmark-correlation-' || lpad(((n - 1) / 8)::text, 6, '0'),
    CURRENT_TIMESTAMP - make_interval(secs => n * 25)
FROM generate_series(1, 100000) n
CROSS JOIN benchmark_scope s;

ANALYZE "AnalyticsEvents";

\echo 'Overview query — approximately 10,000 recent events'
EXPLAIN (ANALYZE, BUFFERS)
SELECT
    count(*) AS event_count,
    count(DISTINCT "SourceExecutionId") AS execution_count,
    count(*) FILTER (WHERE "EventType" = 'SimulationCompleted') AS simulations,
    count(*) FILTER (WHERE "EventType" = 'EvaluationCompleted') AS evaluations,
    avg("DurationMs") AS average_duration_ms
FROM "AnalyticsEvents" e
CROSS JOIN benchmark_scope s
WHERE e."WorkspaceId" = s."WorkspaceId"
  AND e."EnvironmentId" = s."EnvironmentId"
  AND e."OccurredAt" >= CURRENT_TIMESTAMP - INTERVAL '3 days'
  AND e."OccurredAt" < CURRENT_TIMESTAMP;

\echo 'Overview query — approximately 100,000 events'
EXPLAIN (ANALYZE, BUFFERS)
SELECT
    count(*) AS event_count,
    count(DISTINCT "SourceExecutionId") AS execution_count,
    count(*) FILTER (WHERE "EventType" = 'SimulationCompleted') AS simulations,
    count(*) FILTER (WHERE "EventType" = 'EvaluationCompleted') AS evaluations,
    avg("DurationMs") AS average_duration_ms
FROM "AnalyticsEvents" e
CROSS JOIN benchmark_scope s
WHERE e."WorkspaceId" = s."WorkspaceId"
  AND e."EnvironmentId" = s."EnvironmentId"
  AND e."OccurredAt" >= CURRENT_TIMESTAMP - INTERVAL '31 days'
  AND e."OccurredAt" < CURRENT_TIMESTAMP;

\echo 'Cost query — 100,000 events'
EXPLAIN (ANALYZE, BUFFERS)
SELECT "Provider", "Model", "CostType",
       sum("InputTokens"), sum("OutputTokens"), sum("CostZar"), count(*)
FROM "AnalyticsEvents" e
CROSS JOIN benchmark_scope s
WHERE e."WorkspaceId" = s."WorkspaceId"
  AND e."EnvironmentId" = s."EnvironmentId"
  AND e."OccurredAt" >= CURRENT_TIMESTAMP - INTERVAL '31 days'
  AND e."OccurredAt" < CURRENT_TIMESTAMP
  AND e."EventType" = 'ProviderInvocationCompleted'
GROUP BY "Provider", "Model", "CostType";

\echo 'Quality query — 100,000 events'
EXPLAIN (ANALYZE, BUFFERS)
SELECT
    avg("Groundedness"),
    avg("Relevance"),
    avg("Safety"),
    avg("OverallQuality"),
    count(*) FILTER (WHERE "EvaluationOutcome" = 'Passed'),
    count(*) FILTER (WHERE "EvaluationOutcome" = 'Failed')
FROM "AnalyticsEvents" e
CROSS JOIN benchmark_scope s
WHERE e."WorkspaceId" = s."WorkspaceId"
  AND e."EnvironmentId" = s."EnvironmentId"
  AND e."OccurredAt" >= CURRENT_TIMESTAMP - INTERVAL '31 days'
  AND e."OccurredAt" < CURRENT_TIMESTAMP
  AND e."EventType" = 'EvaluationCompleted';

\echo 'Governance query — 100,000 events'
EXPLAIN (ANALYZE, BUFFERS)
SELECT
    count(*) FILTER (WHERE "PolicyOutcome" = 'Allowed'),
    count(*) FILTER (WHERE "PolicyOutcome" = 'Denied'),
    count(*) FILTER (WHERE "ProviderInvocationPrevented"),
    count(*) FILTER (WHERE "EventType" = 'SensitiveTraceRevealed')
FROM "AnalyticsEvents" e
CROSS JOIN benchmark_scope s
WHERE e."WorkspaceId" = s."WorkspaceId"
  AND e."EnvironmentId" = s."EnvironmentId"
  AND e."OccurredAt" >= CURRENT_TIMESTAMP - INTERVAL '31 days'
  AND e."OccurredAt" < CURRENT_TIMESTAMP;

\echo 'Keyset event page — 50 rows'
EXPLAIN (ANALYZE, BUFFERS)
SELECT e."Id", e."EventType", e."Outcome", e."OccurredAt"
FROM "AnalyticsEvents" e
CROSS JOIN benchmark_scope s
WHERE e."WorkspaceId" = s."WorkspaceId"
  AND e."EnvironmentId" = s."EnvironmentId"
  AND e."OccurredAt" < CURRENT_TIMESTAMP - INTERVAL '7 days'
ORDER BY e."OccurredAt" DESC, e."Id" DESC
LIMIT 50;

\echo 'Correlation lookup'
EXPLAIN (ANALYZE, BUFFERS)
SELECT e."Id", e."EventType", e."SourceExecutionId", e."OccurredAt"
FROM "AnalyticsEvents" e
CROSS JOIN benchmark_scope s
WHERE e."WorkspaceId" = s."WorkspaceId"
  AND e."CorrelationId" = 'benchmark-correlation-000100'
ORDER BY e."OccurredAt", e."Id";

\echo 'Filtered export materialisation'
EXPLAIN (ANALYZE, BUFFERS)
CREATE TEMP TABLE benchmark_export AS
SELECT e."EventType", e."Outcome", e."Provider", e."Model", e."ConfigurationRevision", e."OccurredAt"
FROM "AnalyticsEvents" e
CROSS JOIN benchmark_scope s
WHERE e."WorkspaceId" = s."WorkspaceId"
  AND e."EnvironmentId" = s."EnvironmentId"
  AND e."OccurredAt" >= CURRENT_TIMESTAMP - INTERVAL '31 days'
  AND e."OccurredAt" < CURRENT_TIMESTAMP
  AND e."Provider" = 'ConvoLab Deterministic'
  AND e."WorkflowName" = 'Benchmark workflow 1';

\echo 'Incremental daily aggregation'
EXPLAIN (ANALYZE, BUFFERS)
CREATE TEMP TABLE benchmark_daily_aggregates AS
SELECT
    date_trunc('day', e."OccurredAt") AS bucket,
    e."Capability",
    e."Outcome",
    count(*) AS event_count,
    count(DISTINCT e."SourceExecutionId") AS execution_count,
    sum(e."InputTokens") AS input_tokens,
    sum(e."OutputTokens") AS output_tokens,
    sum(e."CostZar") FILTER (WHERE e."CostType" = 'Actual') AS actual_cost,
    sum(e."CostZar") FILTER (WHERE e."CostType" = 'Estimated') AS estimated_cost
FROM "AnalyticsEvents" e
CROSS JOIN benchmark_scope s
WHERE e."WorkspaceId" = s."WorkspaceId"
  AND e."EnvironmentId" = s."EnvironmentId"
  AND e."OccurredAt" >= CURRENT_TIMESTAMP - INTERVAL '1 day'
  AND e."OccurredAt" < CURRENT_TIMESTAMP
GROUP BY date_trunc('day', e."OccurredAt"), e."Capability", e."Outcome";

\echo 'Late-event one-day rebuild'
EXPLAIN (ANALYZE, BUFFERS)
SELECT
    date_trunc('hour', e."OccurredAt") AS bucket,
    e."Capability",
    e."Outcome",
    count(*) AS event_count,
    count(DISTINCT e."SourceExecutionId") AS execution_count
FROM "AnalyticsEvents" e
CROSS JOIN benchmark_scope s
WHERE e."WorkspaceId" = s."WorkspaceId"
  AND e."EnvironmentId" = s."EnvironmentId"
  AND e."OccurredAt" >= CURRENT_TIMESTAMP - INTERVAL '8 days'
  AND e."OccurredAt" < CURRENT_TIMESTAMP - INTERVAL '7 days'
GROUP BY date_trunc('hour', e."OccurredAt"), e."Capability", e."Outcome";

ROLLBACK;
