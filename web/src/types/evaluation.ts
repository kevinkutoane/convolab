export interface EvaluationMetricDefinition {
  id: string;
  key: string;
  displayName: string;
  description: string;
  weight: number;
  threshold: number;
  required: boolean;
}

export interface EvaluationScorecard {
  id: string;
  name: string;
  description: string;
  status: string;
  version: string;
  qualityGateThreshold: number;
  isDefault: boolean;
  revision: number;
  metrics: EvaluationMetricDefinition[];
  createdAt: string;
  updatedAt: string;
}

export interface EvaluationMetricResult {
  id: string;
  key: string;
  displayName: string;
  score: number;
  threshold: number;
  weight: number;
  passed: boolean;
  detail: string;
}

export interface EvaluationRun {
  id: string;
  simulationId: string;
  simulationTitle: string;
  sourceRunId: string;
  scorecardId: string;
  scorecardName: string;
  scorecardVersion: string;
  status: string;
  verdict: string;
  overallScore: number;
  metrics: EvaluationMetricResult[];
  failureReason?: string | null;
  reviewStatus: string;
  reviewNotes?: string | null;
  reviewer?: string | null;
  reviewedAt?: string | null;
  createdAt: string;
}

export interface EvaluationTestCase {
  id: string;
  name: string;
  description: string;
  simulationId: string;
  sourceRunId: string;
  scorecardId?: string | null;
  expectedVerdict: string;
  tags: string[];
  status: string;
  revision: number;
  createdAt: string;
  updatedAt: string;
}

export interface EvaluationBatchItem {
  id: string;
  testCaseId: string;
  testCaseName: string;
  evaluationRunId?: string | null;
  status: string;
  actualVerdict: string;
  expectedVerdict: string;
  passed: boolean;
  detail: string;
}

export interface EvaluationBatch {
  id: string;
  name: string;
  scorecardId: string;
  scorecardName: string;
  status: string;
  totalCases: number;
  passedCases: number;
  passRate: number;
  items: EvaluationBatchItem[];
  startedAt: string;
  completedAt?: string | null;
}

export interface EvaluationMetrics {
  totalRuns: number;
  passedRuns: number;
  reviewRuns: number;
  failedRuns: number;
  passRate: number;
  averageScore: number;
  publishedScorecards: number;
  testCases: number;
  regressionCount: number;
}

export interface EvaluationDailyQuality {
  date: string;
  runs: number;
  averageScore: number;
  passRate: number;
}

export interface EvaluationOverview {
  metrics: EvaluationMetrics;
  qualityTrend: EvaluationDailyQuality[];
  scorecards: EvaluationScorecard[];
  recentRuns: EvaluationRun[];
  testCases: EvaluationTestCase[];
  recentBatches: EvaluationBatch[];
  generatedAt: string;
}

export interface CreateEvaluationScorecardRequest {
  name: string;
  description: string;
  version: string;
  qualityGateThreshold: number;
  metrics?: Array<{
    key: string;
    displayName: string;
    description: string;
    weight: number;
    threshold: number;
    required: boolean;
  }>;
  isDefault?: boolean;
}

export interface CreateEvaluationTestCaseRequest {
  name: string;
  description: string;
  simulationId: string;
  sourceRunId: string;
  scorecardId?: string | null;
  expectedVerdict: string;
  tags: string[];
}

export interface RunEvaluationBatchRequest {
  name: string;
  scorecardId: string;
  testCaseIds: string[];
}

export interface EvaluationComparisonMetric {
  key: string;
  displayName: string;
  baselineScore: number;
  candidateScore: number;
  delta: number;
  direction: string;
}

export interface EvaluationComparison {
  baseline: EvaluationRun;
  candidate: EvaluationRun;
  overallDelta: number;
  outcome: string;
  metrics: EvaluationComparisonMetric[];
  findings: string[];
}
