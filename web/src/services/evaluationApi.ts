import { api } from "./apiClient";
import type {
  CreateEvaluationScorecardRequest,
  CreateEvaluationTestCaseRequest,
  EvaluationBatch,
  EvaluationComparison,
  EvaluationOverview,
  EvaluationRun,
  EvaluationScorecard,
  EvaluationTestCase,
  RunEvaluationBatchRequest,
} from "../types/evaluation";

export async function getEvaluationOverview(): Promise<EvaluationOverview> {
  const response = await api.get<EvaluationOverview>("/api/evaluations/overview");
  return response.data;
}

export async function createEvaluationScorecard(request: CreateEvaluationScorecardRequest): Promise<EvaluationScorecard> {
  const response = await api.post<EvaluationScorecard>("/api/evaluations/scorecards", request);
  return response.data;
}

export async function publishEvaluationScorecard(id: string, revision: number): Promise<EvaluationScorecard> {
  const response = await api.post<EvaluationScorecard>(`/api/evaluations/scorecards/${id}/publish`, null, { params: { revision } });
  return response.data;
}

export async function reviewEvaluationRun(
  id: string,
  status: string,
  reviewer: string,
  notes?: string,
): Promise<EvaluationRun> {
  const response = await api.post<EvaluationRun>(`/api/evaluations/runs/${id}/review`, { status, reviewer, notes });
  return response.data;
}

export async function compareEvaluationRuns(baselineId: string, candidateId: string): Promise<EvaluationComparison> {
  const response = await api.get<EvaluationComparison>("/api/evaluations/compare", { params: { baselineId, candidateId } });
  return response.data;
}

export async function createEvaluationTestCase(request: CreateEvaluationTestCaseRequest): Promise<EvaluationTestCase> {
  const response = await api.post<EvaluationTestCase>("/api/evaluations/test-cases", request);
  return response.data;
}

export async function runEvaluationBatch(request: RunEvaluationBatchRequest): Promise<EvaluationBatch> {
  const response = await api.post<EvaluationBatch>("/api/evaluations/batches", request);
  return response.data;
}
