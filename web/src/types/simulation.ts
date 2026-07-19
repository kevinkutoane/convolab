export type SimulationMode = "Normal" | "RetryOnce" | "Fallback";

export interface SimulationOptions {
  workflows: string[];
  promptVersions: string[];
  knowledgeCollections: string[];
  modes: SimulationMode[];
  providers: SimulationProviderOption[];
}

export interface SimulationProviderOption {
  key: string;
  displayName: string;
  defaultModel: string;
  isConfigured: boolean;
  isLive: boolean;
  status: string;
  configurationHint?: string | null;
}

export interface SimulationSummary {
  id: string;
  title: string;
  status: string;
  workflow: string;
  promptVersion: string;
  knowledgeCollection: string;
  messageCount: number;
  runCount: number;
  lastMessage?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface SimulationConversation {
  id: string;
  title: string;
  status: string;
  workflow: string;
  promptVersion: string;
  knowledgeCollection: string;
  messages: SimulationMessage[];
  runs: SimulationRun[];
  createdAt: string;
  updatedAt: string;
}

export interface SimulationMessage {
  id: string;
  role: "user" | "assistant" | string;
  content: string;
  isReplay: boolean;
  createdAt: string;
}

export interface SimulationRun {
  id: string;
  userMessageId: string;
  assistantMessageId?: string | null;
  replayedFromRunId?: string | null;
  status: string;
  mode: SimulationMode;
  workflow?: SimulationWorkflowSnapshot | null;
  renderedPrompt: string;
  knowledgePackage: SimulationKnowledgePackage;
  executionPlan?: SimulationExecutionPlan | null;
  metrics?: SimulationExecutionMetrics | null;
  evaluation: SimulationEvaluation;
  timeline: SimulationTimelineStep[];
  failureReason?: string | null;
  createdAt: string;
}


export interface SimulationWorkflowSnapshot {
  workflowId?: string | null;
  versionId?: string | null;
  name: string;
  version: string;
  source: string;
  nodes: SimulationWorkflowNode[];
  transitions: SimulationWorkflowTransition[];
}

export interface SimulationWorkflowNode {
  id: string;
  name: string;
  kind: string;
  sequence: number;
}

export interface SimulationWorkflowTransition {
  id: string;
  fromNodeId: string;
  toNodeId: string;
  label: string;
  condition?: string | null;
}

export interface SimulationKnowledgePackage {
  id: string;
  collection: string;
  retrievalStrategy: string;
  confidence: number;
  tokenEstimate: number;
  citations: SimulationCitation[];
}

export interface SimulationCitation {
  source: string;
  section: string;
  snippet: string;
}

export interface SimulationExecutionPlan {
  id: string;
  provider: string;
  model: string;
  streaming: boolean;
  toolsAllowed: boolean;
  maxAttempts: number;
  fallbackCount: number;
  estimatedInputTokens: number;
  estimatedOutputTokens: number;
  estimatedCost: number;
  currency: string;
  estimatedLatencyMs: number;
  attempts: number;
  fallbacksUsed: number;
}

export interface SimulationExecutionMetrics {
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  actualCost: number;
  currency: string;
  totalDurationMs: number;
  providerLatencyMs: number;
}

export interface SimulationEvaluation {
  groundedness: number;
  relevance: number;
  safety: number;
  verdict: string;
}

export interface SimulationTimelineStep {
  id: string;
  name: string;
  capability: string;
  status: string;
  detail: string;
  startedAt: string;
  durationMs: number;
}

export interface CreateSimulationRequest {
  title: string;
  workflow: string;
  promptVersion: string;
  knowledgeCollection: string;
}

export interface SendSimulationMessageRequest {
  content: string;
  mode: SimulationMode;
  provider: string;
  model?: string;
  temperature: number;
  maxOutputTokens: number;
}

export interface ReplaySimulationRequest {
  runId: string;
  mode: SimulationMode;
  provider: string;
  model?: string;
  temperature: number;
  maxOutputTokens: number;
}
