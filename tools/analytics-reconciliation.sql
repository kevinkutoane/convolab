\set ON_ERROR_STOP on
\pset pager off

\echo 'Analytics event inventory'
SELECT "EventType", "Outcome", count(*) AS "Count"
FROM "AnalyticsEvents"
GROUP BY "EventType", "Outcome"
ORDER BY "EventType", "Outcome";

\echo 'Latest attributed execution timelines'
WITH latest AS (
    SELECT "SourceExecutionId"
    FROM "AnalyticsEvents"
    WHERE "SourceExecutionId" IS NOT NULL
    GROUP BY "SourceExecutionId"
    ORDER BY max("OccurredAt") DESC
    LIMIT 5
)
SELECT
    e."SourceExecutionId",
    e."Id" AS "AnalyticsEventId",
    e."CorrelationId",
    e."OrganisationId",
    e."WorkspaceId",
    e."EnvironmentId",
    e."ActorId",
    e."ConfigurationRevision",
    e."EventType",
    e."Provider",
    e."Model",
    e."InputTokens",
    e."OutputTokens",
    e."CostZar",
    e."CostType",
    e."PolicyOutcome",
    e."EvaluationOutcome",
    e."SourceType",
    e."SourceId",
    e."OccurredAt"
FROM "AnalyticsEvents" e
JOIN latest l ON l."SourceExecutionId" = e."SourceExecutionId"
ORDER BY e."SourceExecutionId", e."OccurredAt", e."Id";

\echo 'Policy-prevented execution invariants'
SELECT
    "SourceExecutionId",
    "CorrelationId",
    bool_or("ProviderInvocationPrevented") AS "ProviderPrevented",
    coalesce(sum("InputTokens"), 0) AS "InputTokens",
    coalesce(sum("OutputTokens"), 0) AS "OutputTokens",
    coalesce(sum("CostZar"), 0) AS "CostZar",
    array_agg("EventType" ORDER BY "OccurredAt") AS "Events"
FROM "AnalyticsEvents"
WHERE "PolicyOutcome" = 'Denied'
   OR "EventType" = 'ProviderInvocationPrevented'
GROUP BY "SourceExecutionId", "CorrelationId"
ORDER BY max("OccurredAt") DESC;

\echo 'Execution attribution links'
SELECT
    a."SourceResourceType",
    a."SourceResourceId",
    a."OrganisationId",
    a."WorkspaceId",
    a."EnvironmentId",
    a."ActorId",
    a."ConfigurationRevision",
    a."CorrelationId",
    a."AttributionStatus"
FROM "ExecutionAttributions" a
WHERE EXISTS (
    SELECT 1
    FROM "AnalyticsEvents" e
    WHERE e."SourceId" = a."SourceResourceId"
       OR e."SourceExecutionId" = a."SourceResourceId")
ORDER BY a."CreatedAt" DESC
LIMIT 30;

\echo 'Hourly aggregate reconciliation totals'
SELECT
    "OrganisationId",
    "WorkspaceId",
    "EnvironmentId",
    sum("EventCount") AS "EventCount",
    sum("ExecutionCount") AS "ExecutionCount",
    sum("SimulationCount") AS "SimulationCount",
    sum("ProviderInvocationCount") AS "ProviderInvocationCount",
    sum("ProviderInvocationPreventedCount") AS "ProviderInvocationPreventedCount",
    sum("EvaluationCount") AS "EvaluationCount",
    sum("TraceCount") AS "TraceCount",
    sum("ReplayCount") AS "ReplayCount",
    sum("PolicyDeniedCount") AS "PolicyDeniedCount",
    sum("EstimatedCostZar") AS "EstimatedCostZar",
    sum("ActualCostZar") AS "ActualCostZar"
FROM "AnalyticsHourlyAggregates"
GROUP BY "OrganisationId", "WorkspaceId", "EnvironmentId";

\echo 'Daily aggregate reconciliation totals'
SELECT
    "OrganisationId",
    "WorkspaceId",
    "EnvironmentId",
    sum("EventCount") AS "EventCount",
    sum("ExecutionCount") AS "ExecutionCount",
    sum("SimulationCount") AS "SimulationCount",
    sum("ProviderInvocationCount") AS "ProviderInvocationCount",
    sum("ProviderInvocationPreventedCount") AS "ProviderInvocationPreventedCount",
    sum("EvaluationCount") AS "EvaluationCount",
    sum("TraceCount") AS "TraceCount",
    sum("ReplayCount") AS "ReplayCount",
    sum("PolicyDeniedCount") AS "PolicyDeniedCount",
    sum("EstimatedCostZar") AS "EstimatedCostZar",
    sum("ActualCostZar") AS "ActualCostZar"
FROM "AnalyticsDailyAggregates"
GROUP BY "OrganisationId", "WorkspaceId", "EnvironmentId";

\echo 'Sensitive customer-content scan — expected zero'
SELECT count(*) AS "PotentialSensitiveRows"
FROM "AnalyticsEvents"
WHERE concat_ws(
    ' ',
    "Capability",
    "EventType",
    "Outcome",
    "Provider",
    "Model",
    "PromptName",
    "WorkflowName",
    "KnowledgeCollectionName",
    "ConfigurationRevision",
    "CorrelationId") ~* '(can i claim for hail damage|this must be denied|customer message|bearer |password=|api[_-]?key=)';

\echo 'Export sensitive-content scan — expected zero'
SELECT count(*) AS "PotentialSensitiveExports"
FROM "AnalyticsExports"
WHERE "Content" IS NOT NULL
  AND convert_from("Content", 'UTF8') ~* '(can i claim for hail damage|this must be denied|customer message|bearer |password=|api[_-]?key=)';
