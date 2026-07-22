import { api } from "./apiClient";
import type { TraceDetail, TraceOverview, TraceSearchFilters, TraceSummary } from "../types/trace";

export async function getTraceOverview(): Promise<TraceOverview> {
  const response = await api.get<TraceOverview>("/api/traces/overview");
  return response.data;
}

export async function listTraces(filters: TraceSearchFilters = {}): Promise<TraceSummary[]> {
  const response = await api.get<TraceSummary[]>("/api/traces", { params: filters });
  return response.data;
}

export async function getTrace(traceId: string, includeSensitive = false): Promise<TraceDetail> {
  const response = await api.get<TraceDetail>(`/api/traces/${traceId}`, { params: { includeSensitive } });
  return response.data;
}
