export interface TraceMetrics {
  totalTraces: number;
  completedTraces: number;
  failedTraces: number;
  successRate: number;
  totalSpans: number;
  averageDurationMs: number;
  p95DurationMs: number;
  totalTokens: number;
  totalCost: number;
  currency: string;
}

export interface TraceDailyActivity {
  date: string;
  traces: number;
  failed: number;
  averageDurationMs: number;
}

export interface TraceCapabilityMetric {
  capability: string;
  spans: number;
  failed: number;
  averageDurationMs: number;
  share: number;
}

export interface TraceSummary {
  id: string;
  correlationId: string;
  operationName: string;
  source: string;
  status: string;
  simulationId?: string | null;
  simulationTitle?: string | null;
  sourceRunId?: string | null;
  replayedFromRunId?: string | null;
  provider?: string | null;
  model?: string | null;
  workflow?: string | null;
  promptVersion?: string | null;
  knowledgeCollection?: string | null;
  evaluationVerdict?: string | null;
  durationMs: number;
  spanCount: number;
  failedSpanCount: number;
  totalTokens: number;
  actualCost: number;
  currency: string;
  failureReason?: string | null;
  startedAt: string;
  completedAt?: string | null;
}

export interface TraceSpan {
  id: string;
  traceId: string;
  parentSpanId?: string | null;
  name: string;
  capability: string;
  status: string;
  detail: string;
  sequence: number;
  startedAt: string;
  completedAt?: string | null;
  durationMs: number;
  attributes: Record<string, string>;
}

export interface TraceEvent {
  id: string;
  traceId: string;
  spanId?: string | null;
  name: string;
  level: string;
  message: string;
  occurredAt: string;
  attributes: Record<string, string>;
}

export interface TraceArtifact {
  id: string;
  traceId: string;
  spanId?: string | null;
  kind: string;
  name: string;
  contentType: string;
  content: string;
  isSensitive: boolean;
  isRedacted: boolean;
  createdAt: string;
}

export interface TraceDetail {
  summary: TraceSummary;
  spans: TraceSpan[];
  events: TraceEvent[];
  artifacts: TraceArtifact[];
}

export interface TraceOverview {
  metrics: TraceMetrics;
  activity: TraceDailyActivity[];
  capabilities: TraceCapabilityMetric[];
  recentTraces: TraceSummary[];
  providers: string[];
  statuses: string[];
  generatedAt: string;
}

export interface TraceSearchFilters {
  query?: string;
  status?: string;
  capability?: string;
  provider?: string;
  limit?: number;
}
