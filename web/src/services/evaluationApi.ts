import { api } from "./apiClient";
import type {
  EvaluationOverview,
  EvaluationPreview,
  EvaluationPreviewRequest,
  EvaluationRun,
  EvaluationScorecard,
  CreateEvaluationScorecardRequest,
} from "../types/evaluation";

export async function getEvaluationOverview(): Promise<EvaluationOverview> {
  const response = await api.get<EvaluationOverview>("/api/evaluation/overview");
  return response.data;
}

export async function listEvaluationRuns(limit = 100): Promise<EvaluationRun[]> {
  const response = await api.get<EvaluationRun[]>("/api/evaluation/runs", { params: { limit } });
  return response.data;
}

export async function listEvaluationScorecards(): Promise<EvaluationScorecard[]> {
  const response = await api.get<EvaluationScorecard[]>("/api/evaluation/scorecards");
  return response.data;
}

export async function createEvaluationScorecard(
  request: CreateEvaluationScorecardRequest,
): Promise<EvaluationScorecard> {
  const response = await api.post<EvaluationScorecard>("/api/evaluation/scorecards", request);
  return response.data;
}

export async function previewEvaluation(request: EvaluationPreviewRequest): Promise<EvaluationPreview> {
  const response = await api.post<EvaluationPreview>("/api/evaluation/preview", request);
  return response.data;
}
