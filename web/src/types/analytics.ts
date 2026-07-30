export interface AnalyticsScope {
  workspaceId: string;
  environmentId?: string;
  from: string;
  to: string;
  granularity: "day" | "hour";
  filters: Record<string, string | undefined>;
}

export interface AnalyticsMetric {
  key: string;
  label: string;
  value?: number;
  unit: string;
  classification?: "Actual" | "Estimated" | "Unavailable";
  detail?: string;
}

export interface AnalyticsPoint {
  bucket: string;
  eventCount: number;
  executionCount: number;
  simulationCount: number;
  evaluationCount: number;
  replayCount: number;
  providerInvocationCount: number;
  providerInvocationPreventedCount: number;
  policyEvaluationCount: number;
  succeeded: number;
  failed: number;
  denied: number;
  inputTokens?: number;
  outputTokens?: number;
  actualCostZar?: number;
  estimatedCostZar?: number;
  unknownCostCount?: number;
  averageDurationMs: number;
  averageQuality?: number;
}

export interface AnalyticsIndicator {
  key: string;
  severity: string;
  title: string;
  detail: string;
  detectedAt: string;
  rule: string;
  threshold?: number;
  observedValue?: number;
  from: string;
  to: string;
  environmentId?: string;
  sourceMetric: string;
}

export interface AnalyticsDashboard {
  category: string;
  scope: AnalyticsScope;
  metrics: AnalyticsMetric[];
  series: AnalyticsPoint[];
  indicators: AnalyticsIndicator[];
  isPartial: boolean;
  generatedAt: string;
}

export interface AnalyticsEvent {
  id: string;
  organisationId: string;
  workspaceId: string;
  environmentId: string;
  actorId?: string;
  actorType: string;
  actorRole?: string;
  capability: string;
  eventType: string;
  outcome: string;
  provider?: string;
  model?: string;
  inputTokens?: number;
  outputTokens?: number;
  costZar?: number;
  costType: string;
  pricingRevision?: string;
  durationMs?: number;
  qualityScore?: number;
  groundedness?: number;
  relevance?: number;
  safety?: number;
  overallQuality?: number;
  providerInvocationPrevented: boolean;
  sourceExecutionId?: string;
  sourceType: string;
  sourceId?: string;
  prompt?: string;
  workflow?: string;
  knowledgeCollection?: string;
  policyOutcome?: string;
  evaluationOutcome?: string;
  configurationRevision: string;
  correlationId: string;
  occurredAt: string;
}

export interface AnalyticsEventPage {
  scope: AnalyticsScope;
  items: AnalyticsEvent[];
  nextCursor?: string;
  totalCount: number;
}

export interface AnalyticsFilterOptions {
  providers: string[];
  models: string[];
  capabilities: string[];
  outcomes: string[];
  prompts: string[];
  workflows: string[];
  knowledgeCollections: string[];
  configurationRevisions: string[];
  eventTypes: string[];
  costTypes: string[];
}

export interface AnalyticsExport {
  id: string;
  status: string;
  fileName: string;
  rowCount?: number;
  sizeBytes?: number;
  checksum?: string;
  failureReason?: string;
  createdAt: string;
  expiresAt: string;
  completedAt?: string;
}
