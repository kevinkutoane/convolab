export interface IntelligenceModelDefinition {
  key: string;
  displayName: string;
  capabilities: string[];
  maxContextTokens: number;
  maxOutputTokens: number;
  typicalLatencyMs: number;
  inputPricePer1K?: number | null;
  outputPricePer1K?: number | null;
  currency: string;
}

export interface IntelligenceProviderDefinition {
  key: string;
  displayName: string;
  isConfigured: boolean;
  isLive: boolean;
  status: string;
  configurationHint?: string | null;
  models: IntelligenceModelDefinition[];
}

export interface IntelligenceMetrics {
  totalExecutions: number;
  successfulExecutions: number;
  successRate: number;
  averageLatencyMs: number;
  totalTokens: number;
  totalCost: number;
  currency: string;
  retryExecutions: number;
  fallbackExecutions: number;
}

export interface IntelligenceProviderUsage {
  provider: string;
  executions: number;
  successRate: number;
  averageLatencyMs: number;
  totalTokens: number;
  totalCost: number;
  currency: string;
}

export interface IntelligenceDailyUsage {
  date: string;
  executions: number;
  tokens: number;
  cost: number;
  averageLatencyMs: number;
}

export interface IntelligenceBudget {
  limit: number;
  consumed: number;
  remaining: number;
  utilisation: number;
  currency: string;
  periodStart: string;
  periodEnd: string;
  status: string;
}

export interface IntelligenceExecution {
  simulationId: string;
  simulationTitle: string;
  runId: string;
  status: string;
  mode: string;
  provider: string;
  model: string;
  attempts: number;
  fallbacksUsed: number;
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  cost: number;
  currency: string;
  durationMs: number;
  providerLatencyMs: number;
  groundedness: number;
  relevance: number;
  verdict: string;
  createdAt: string;
  failureReason?: string | null;
}

export interface IntelligenceOverview {
  metrics: IntelligenceMetrics;
  budget: IntelligenceBudget;
  providers: IntelligenceProviderDefinition[];
  providerUsage: IntelligenceProviderUsage[];
  dailyUsage: IntelligenceDailyUsage[];
  recentExecutions: IntelligenceExecution[];
  generatedAt: string;
}

export interface ExecutionPlanPreviewRequest {
  provider: string;
  model: string;
  estimatedInputTokens: number;
  maxOutputTokens: number;
  streaming: boolean;
  allowFallback: boolean;
  maxAttempts: number;
  requiredCapabilities: string[];
}

export interface ExecutionPlanDecision {
  name: string;
  status: string;
  detail: string;
}

export interface ExecutionPlanPreview {
  provider: string;
  model: string;
  isConfigured: boolean;
  capabilityMatch: boolean;
  estimatedInputTokens: number;
  estimatedOutputTokens: number;
  estimatedTotalTokens: number;
  estimatedCost?: number | null;
  currency: string;
  estimatedLatencyMs: number;
  budgetRemaining: number;
  withinBudget: boolean;
  decisions: ExecutionPlanDecision[];
  warnings: string[];
}

export interface ProviderTestResult {
  provider: string;
  status: string;
  latencyMs: number;
  model?: string;
}
