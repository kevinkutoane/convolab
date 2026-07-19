export interface EvaluationPolicy {
  minimumGroundedness: number;
  minimumRelevance: number;
  minimumSafety: number;
  minimumOverallScore: number;
  failureAction: string;
}

export interface EvaluationScorecard {
  id: string;
  name: string;
  description: string;
  minimumGroundedness: number;
  minimumRelevance: number;
  minimumSafety: number;
  minimumOverallScore: number;
  failureAction: string;
  createdAt: string;
  updatedAt: string;
}

export type CreateEvaluationScorecardRequest = Omit<
  EvaluationScorecard,
  "id" | "createdAt" | "updatedAt"
>;

export interface EvaluationMetricSummary {
  name: string;
  average: number;
  minimum: number;
  maximum: number;
  threshold: number;
  passing: number;
  failing: number;
}

export interface EvaluationRun {
  simulationId: string;
  simulationTitle: string;
  runId: string;
  provider: string;
  model: string;
  status: string;
  groundedness: number;
  relevance: number;
  safety: number;
  overallScore: number;
  verdict: string;
  passed: boolean;
  failedGates: string[];
  createdAt: string;
}

export interface EvaluationDailyTrend {
  date: string;
  runs: number;
  averageScore: number;
  passRate: number;
}

export interface EvaluationOverview {
  totalRuns: number;
  evaluatedRuns: number;
  passingRuns: number;
  failingRuns: number;
  passRate: number;
  averageOverallScore: number;
  policy: EvaluationPolicy;
  metrics: EvaluationMetricSummary[];
  dailyTrend: EvaluationDailyTrend[];
  recentRuns: EvaluationRun[];
  generatedAt: string;
}

export interface EvaluationPreviewRequest {
  groundedness: number;
  relevance: number;
  safety: number;
  scorecardId?: string;
  minimumGroundedness?: number;
  minimumRelevance?: number;
  minimumSafety?: number;
  minimumOverallScore?: number;
}

export interface EvaluationGateDecision {
  name: string;
  score: number;
  threshold: number;
  status: string;
}

export interface EvaluationPreview {
  groundedness: number;
  relevance: number;
  safety: number;
  overallScore: number;
  passed: boolean;
  verdict: string;
  failedGates: string[];
  decisions: EvaluationGateDecision[];
}
