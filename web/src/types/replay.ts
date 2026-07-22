import type { SimulationMode, SimulationOptions } from "./simulation";

export interface ReplayMetrics {
  totalExperiments: number;
  activeExperiments: number;
  totalCandidates: number;
  improvedCandidates: number;
  regressionCandidates: number;
  averageQualityDelta: number;
  averageLatencyDeltaMs: number;
  averageCostDelta: number;
  currency: string;
}

export interface ReplayConfiguration {
  workflow: string;
  promptVersion: string;
  knowledgeCollection: string;
  provider: string;
  model: string;
  temperature: number;
  maxOutputTokens: number;
  mode: SimulationMode;
}

export interface ReplayRunSnapshot {
  runId: string;
  status: string;
  workflow: string;
  workflowVersion: string;
  promptVersion: string;
  knowledgeCollection: string;
  provider: string;
  model: string;
  mode: SimulationMode;
  temperature: number;
  maxOutputTokens: number;
  qualityScore: number;
  groundedness: number;
  relevance: number;
  safety: number;
  verdict: string;
  durationMs: number;
  providerLatencyMs: number;
  totalTokens: number;
  actualCost: number;
  currency: string;
  attempts: number;
  fallbacksUsed: number;
  citationCount: number;
  responseLength: number;
  response?: string | null;
  failureReason?: string | null;
  createdAt: string;
}

export interface ReplaySource {
  simulationId: string;
  simulationTitle: string;
  runId: string;
  replayedFromRunId?: string | null;
  status: string;
  userMessage: string;
  response?: string | null;
  snapshot: ReplayRunSnapshot;
  createdAt: string;
}

export interface ReplayComparison {
  qualityDelta: number;
  groundednessDelta: number;
  relevanceDelta: number;
  safetyDelta: number;
  durationDeltaMs: number;
  providerLatencyDeltaMs: number;
  tokenDelta: number;
  costDelta: number;
  citationDelta: number;
  responseLengthDelta: number;
  outcome: "Improved" | "Regression" | "Efficient" | "Equivalent" | string;
  changedDimensions: string[];
  findings: string[];
}

export interface ReplayCandidate {
  id: string;
  experimentId: string;
  runId: string;
  label: string;
  status: string;
  configuration: ReplayConfiguration;
  snapshot: ReplayRunSnapshot;
  comparison: ReplayComparison;
  createdAt: string;
}

export interface ReplayExperimentSummary {
  id: string;
  name: string;
  simulationId: string;
  simulationTitle: string;
  sourceRunId: string;
  status: string;
  candidateCount: number;
  bestCandidateId?: string | null;
  bestQualityDelta: number;
  createdAt: string;
  updatedAt: string;
}

export interface ReplayExperimentDetail {
  summary: ReplayExperimentSummary;
  baseline: ReplaySource;
  candidates: ReplayCandidate[];
}

export interface ReplayOverview {
  metrics: ReplayMetrics;
  recentExperiments: ReplayExperimentSummary[];
  recentSources: ReplaySource[];
  options: SimulationOptions;
  generatedAt: string;
}

export interface ReplayCandidateInput {
  label: string;
  workflow?: string | null;
  promptVersion?: string | null;
  knowledgeCollection?: string | null;
  provider: string;
  model?: string | null;
  temperature: number;
  maxOutputTokens: number;
  mode: SimulationMode;
}

export interface CreateReplayExperimentInput {
  name: string;
  simulationId: string;
  sourceRunId: string;
  candidateLabel: string;
  workflow?: string | null;
  promptVersion?: string | null;
  knowledgeCollection?: string | null;
  provider: string;
  model?: string | null;
  temperature: number;
  maxOutputTokens: number;
  mode: SimulationMode;
}
