import { api } from "./apiClient";
import type { AnalyticsDashboard, AnalyticsEvent, AnalyticsEventPage, AnalyticsExport, AnalyticsFilterOptions } from "../types/analytics";

export interface AnalyticsFilters {
  environmentId?: string;
  from: string;
  to: string;
  granularity: "day" | "hour";
  provider?: string;
  model?: string;
  capability?: string;
  outcome?: string;
  configurationRevision?: string;
  prompt?: string;
  workflow?: string;
  knowledgeCollection?: string;
  actorId?: string;
  eventType?: string;
  costType?: string;
}

const params = (filters: AnalyticsFilters) => {
  const value = new URLSearchParams();
  Object.entries(filters).forEach(([key, entry]) => {
    if (entry !== undefined && entry !== "") value.set(key, entry);
  });
  return value.toString();
};

export const getAnalyticsDashboard = async (workspaceId: string, category: string, filters: AnalyticsFilters) =>
  (await api.get<AnalyticsDashboard>(`/api/workspaces/${workspaceId}/analytics/${category}?${params(filters)}`)).data;

export const getAnalyticsFilterOptions = async (workspaceId: string, filters: AnalyticsFilters) =>
  (await api.get<AnalyticsFilterOptions>(`/api/workspaces/${workspaceId}/analytics/filter-options?${params(filters)}`)).data;

export const getAnalyticsEvents = async (workspaceId: string, filters: AnalyticsFilters, cursor?: string) => {
  const query = new URLSearchParams(params(filters));
  query.set("take", "100");
  if (cursor) query.set("cursor", cursor);
  return (await api.get<AnalyticsEventPage>(`/api/workspaces/${workspaceId}/analytics/events?${query}`)).data;
};

export const getAnalyticsEvent = async (workspaceId: string, eventId: string) =>
  (await api.get<AnalyticsEvent>(
    `/api/workspaces/${workspaceId}/analytics/events/${eventId}`,
  )).data;

export const getAnalyticsCorrelation = async (workspaceId: string, correlationId: string) =>
  (await api.get<AnalyticsEvent[]>(
    `/api/workspaces/${workspaceId}/analytics/correlations/${encodeURIComponent(correlationId)}`,
  )).data;

export const createAnalyticsExport = async (workspaceId: string, filters: AnalyticsFilters) =>
  (await api.post<AnalyticsExport>(`/api/workspaces/${workspaceId}/analytics/exports`, {
    environmentId: filters.environmentId,
    from: filters.from,
    to: filters.to,
    provider: filters.provider,
    model: filters.model,
    capability: filters.capability,
    outcome: filters.outcome,
    configurationRevision: filters.configurationRevision,
    prompt: filters.prompt,
    workflow: filters.workflow,
    knowledgeCollection: filters.knowledgeCollection,
    actorId: filters.actorId,
    eventType: filters.eventType,
    costType: filters.costType,
  })).data;

export const getAnalyticsExports = async (workspaceId: string) =>
  (await api.get<AnalyticsExport[]>(`/api/workspaces/${workspaceId}/analytics/exports`)).data;
