import { api } from "./apiClient";
import type {
  ExecutionPlanPreview,
  ExecutionPlanPreviewRequest,
  IntelligenceExecution,
  IntelligenceOverview,
  ProviderTestResult,
} from "../types/intelligence";

export async function getIntelligenceOverview(): Promise<IntelligenceOverview> {
  const response = await api.get<IntelligenceOverview>("/api/intelligence/overview");
  return response.data;
}

export async function listIntelligenceExecutions(limit = 100): Promise<IntelligenceExecution[]> {
  const response = await api.get<IntelligenceExecution[]>("/api/intelligence/executions", { params: { limit } });
  return response.data;
}

export async function previewExecutionPlan(request: ExecutionPlanPreviewRequest): Promise<ExecutionPlanPreview> {
  const response = await api.post<ExecutionPlanPreview>("/api/intelligence/plan-preview", request);
  return response.data;
}

export async function testIntelligenceProvider(provider: string): Promise<ProviderTestResult> {
  const response = await api.post<ProviderTestResult>(`/api/intelligence/providers/${encodeURIComponent(provider)}/test`);
  return response.data;
}
