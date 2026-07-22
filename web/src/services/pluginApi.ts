import { api } from "./apiClient";
import type {
  PluginDetail,
  PluginHealthCheck,
  PluginOverview,
  PluginSummary,
  RegisterPluginInput,
  UpdatePluginInput,
  UpdatePluginVersionInput,
} from "../types/plugin";

export async function getPluginOverview(): Promise<PluginOverview> {
  const response = await api.get<PluginOverview>("/api/plugins/overview");
  return response.data;
}

export async function listPlugins(): Promise<PluginSummary[]> {
  const response = await api.get<PluginSummary[]>("/api/plugins");
  return response.data;
}

export async function getPlugin(pluginId: string): Promise<PluginDetail> {
  const response = await api.get<PluginDetail>(`/api/plugins/${pluginId}`);
  return response.data;
}

export async function registerPlugin(input: RegisterPluginInput): Promise<PluginDetail> {
  const response = await api.post<PluginDetail>("/api/plugins", input);
  return response.data;
}

export async function updatePlugin(pluginId: string, input: UpdatePluginInput): Promise<PluginDetail> {
  const response = await api.put<PluginDetail>(`/api/plugins/${pluginId}`, input);
  return response.data;
}

export async function updatePluginVersion(pluginId: string, input: UpdatePluginVersionInput): Promise<PluginDetail> {
  const response = await api.post<PluginDetail>(`/api/plugins/${pluginId}/versions`, input);
  return response.data;
}

export async function transitionPlugin(pluginId: string, action: string): Promise<PluginDetail> {
  const response = await api.post<PluginDetail>(`/api/plugins/${pluginId}/${action}`);
  return response.data;
}

export async function checkPluginHealth(pluginId: string): Promise<PluginHealthCheck> {
  const response = await api.post<PluginHealthCheck>(`/api/plugins/${pluginId}/health`);
  return response.data;
}

