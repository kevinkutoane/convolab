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
  readiness: { status: string; lastEvaluatedAt?: string | null };
  safeMode: SafeModeState;
  telemetry: DependencyState;
  worker: { state: DependencyState; staleAfterSeconds: number };
  analytics: { pendingCount: number; failedCount: number; status: string };
  correlationId: string;
}

export interface ReadinessEvidence {
  status: string;
  version: string;
  thresholds: Record<string, number>;
  components: Array<{ component: string; state: DependencyState; status: string; durationMs: number }>;
  correlationId: string;
}

export interface AuthenticationEvidence {
  mode: "Local" | "Entra" | "Hybrid";
  localLoginEnabled: boolean;
  entraEnabled: boolean;
  tenantConfigurationState: "NotConfigured" | "Configured";
  clientAuthentication: {
    configured: boolean;
    secretProviderScheme?: string | null;
  };
  state: DependencyState;
  lastValidationAt?: string | null;
  lastFailureCode?: string | null;
  externalIdentityCount: number;
  linkedActiveUsers: number;
  externalLoginSuccessesLast24Hours: number;
  externalLoginFailuresLast24Hours: number;
  activeSessions: number;
  breakGlassEnabled: boolean;
  breakGlassAvailable: boolean;
  breakGlassState: "Disabled" | "Available" | "Locked" | "Unavailable";
  breakGlassUsesLast24Hours: number;
  breakGlassFailuresLast24Hours: number;
  lastBreakGlassSuccessfulUseAt?: string | null;
  correlationId: string;
}

export async function getOperationsStatus() { return (await api.get<OperationsStatus>("/api/operations/status")).data; }
export async function getReadiness() { return (await api.get<ReadinessEvidence>("/api/operations/readiness")).data; }
export async function getWorkers() { return (await api.get("/api/operations/workers")).data; }
export async function getAnalyticsPipeline() { return (await api.get("/api/operations/analytics-pipeline")).data; }
export async function getAuthenticationEvidence() { return (await api.get<AuthenticationEvidence>("/api/operations/authentication")).data; }
export async function getSecretProviders() { return (await api.get("/api/operations/secret-providers")).data; }
export async function getBackups() { return (await api.get("/api/operations/backups")).data; }
export async function listAvailableBackups() { return (await api.get("/api/operations/backups/list")).data; }
export async function createBackup() { return (await api.post("/api/operations/backups")).data; }
export async function verifyBackup(id: string = "current") { return (await api.post(`/api/operations/backups/${id}/verify`)).data; }
export async function restoreBackup(id: string, allowDestructive: boolean = false) {
  return (await api.post(`/api/operations/backups/${id}/restore?allowDestructive=${allowDestructive}`)).data;
}
export async function getRecoveryStatus(operationId: string) {
  return (await api.get(`/api/operations/recovery/${operationId}`)).data;
}
export async function getBuildEvidence() { return (await api.get("/api/operations/build")).data; }
export async function getTelemetryEvidence() { return (await api.get("/api/operations/telemetry")).data; }
export async function updateSafeMode(input: { enabled: boolean; expectedRevision: number; reason: string; confirmation: string }) {
  return (await api.post<SafeModeState>("/api/operations/safe-mode", input)).data;
}
