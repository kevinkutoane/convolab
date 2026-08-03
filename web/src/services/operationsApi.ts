import { api } from "./apiClient";

export type DependencyState = "NotConfigured" | "Configured" | "StubValidated" | "LiveValidated" | "Unavailable" | "Degraded";

export interface SafeModeState {
  persistedSafeModeEnabled: boolean;
  environmentOverrideEnabled: boolean;
  effectiveSafeModeEnabled: boolean;
  allowDeterministicVerification: boolean;
  blockAnalyticsExports?: boolean | null;
  reason?: string;
  revision: number;
  changedAt: string;
}

export interface OperationsStatus {
  status: string;
  version: string;
  workstream: string;
  releaseStatus: string;
  environment: string;
  safeMode: SafeModeState;
  telemetry: DependencyState;
  worker: { state: DependencyState; staleAfterSeconds: number };
  analytics: { pendingOutbox: number };
  correlationId: string;
}

export interface ReadinessEvidence {
  status: string;
  version: string;
  thresholds: Record<string, number>;
  components: Array<{ component: string; state: DependencyState; status: string; durationMs: number }>;
  correlationId: string;
}

export async function getOperationsStatus() { return (await api.get<OperationsStatus>("/api/operations/status")).data; }
export async function getReadiness() { return (await api.get<ReadinessEvidence>("/api/operations/readiness")).data; }
export async function getWorkers() { return (await api.get("/api/operations/workers")).data; }
export async function getAnalyticsPipeline() { return (await api.get("/api/operations/analytics-pipeline")).data; }
export async function getAuthenticationEvidence() { return (await api.get("/api/operations/authentication")).data; }
export async function getSecretProviders() { return (await api.get("/api/operations/secret-providers")).data; }
export async function getBackups() { return (await api.get("/api/operations/backups")).data; }
export async function getBuildEvidence() { return (await api.get("/api/operations/build")).data; }
export async function updateSafeMode(input: { enabled: boolean; expectedRevision: number; reason: string; confirmation: string }) {
  return (await api.post<SafeModeState>("/api/operations/safe-mode", input)).data;
}
