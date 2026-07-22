export type PluginCategory =
  | "Provider"
  | "Tool"
  | "KnowledgeConnector"
  | "Channel"
  | "Evaluator"
  | "TraceExporter"
  | "WorkflowNode"
  | "EnterpriseConnector";

export type PluginStatus = "Installed" | "Active" | "Inactive" | "Deprecated";
export type PluginHealthStatus = "Unknown" | "Healthy" | "Degraded" | "Unhealthy";

export interface PluginMetrics {
  registered: number;
  active: number;
  healthy: number;
  unhealthy: number;
  categories: number;
  healthChecks: number;
}

export interface PluginSummary {
  id: string;
  key: string;
  name: string;
  description: string;
  publisher: string;
  version: string;
  category: PluginCategory;
  status: PluginStatus;
  healthStatus: PluginHealthStatus;
  healthMessage: string;
  manifestUrl: string;
  platformApiVersion: string;
  compatible: boolean;
  capabilities: string[];
  lastHealthCheckAt?: string | null;
  updatedAt: string;
  revision: number;
}

export interface PluginHealthCheck {
  id: string;
  pluginId: string;
  status: PluginHealthStatus;
  message: string;
  durationMs: number;
  source: string;
  checkedAt: string;
}

export interface PluginDetail {
  summary: PluginSummary;
  entryPoint: string;
  permissions: string[];
  configurationSchema: string;
  metadata: Record<string, string>;
  healthHistory: PluginHealthCheck[];
  versionHistory: PluginSummary[];
}

export interface PluginOverview {
  metrics: PluginMetrics;
  plugins: PluginSummary[];
  recentHealthChecks: PluginHealthCheck[];
  categories: string[];
  generatedAt: string;
}

export interface RegisterPluginInput {
  key: string;
  name: string;
  description: string;
  publisher: string;
  version: string;
  category: PluginCategory;
  manifestUrl: string;
  entryPoint: string;
  platformApiVersion: string;
  capabilities: string[];
  permissions: string[];
  configurationSchema: string;
  metadata: Record<string, string>;
}

export interface UpdatePluginInput {
  name: string;
  description: string;
  publisher: string;
  category: PluginCategory;
  entryPoint: string;
  platformApiVersion: string;
  capabilities: string[];
  permissions: string[];
  configurationSchema: string;
  metadata: Record<string, string>;
  revision: number;
}

export interface UpdatePluginVersionInput {
  version: string;
  manifestUrl: string;
  revision: number;
}

