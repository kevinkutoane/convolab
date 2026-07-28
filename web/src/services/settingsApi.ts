import { api } from "./apiClient";
import type {
  ConfigurationChange,
  ConfigurationExport,
  CreateEnvironmentInput,
  EffectiveSetting,
  ProviderValidationResult,
  RuntimeEnvironment,
  SecretReference,
  SettingValue,
  SettingsValidationResult,
  UpdateEnvironmentInput,
  UpsertSettingInput,
} from "../types/settings";

// ─── Environments ─────────────────────────────────────────────────────────────

export const listEnvironments = async (workspaceId: string) =>
  (await api.get<RuntimeEnvironment[]>(`/api/workspaces/${workspaceId}/environments`)).data;

export const createEnvironment = async (workspaceId: string, input: CreateEnvironmentInput) =>
  (await api.post<RuntimeEnvironment>(`/api/workspaces/${workspaceId}/environments`, input)).data;

export const updateEnvironment = async (workspaceId: string, environmentId: string, input: UpdateEnvironmentInput) =>
  (await api.patch<RuntimeEnvironment>(`/api/workspaces/${workspaceId}/environments/${environmentId}`, input)).data;

export const activateEnvironment = async (workspaceId: string, environmentId: string, expectedRevision: number) =>
  (await api.post<RuntimeEnvironment>(`/api/workspaces/${workspaceId}/environments/${environmentId}/activate`, { expectedRevision })).data;

export const suspendEnvironment = async (workspaceId: string, environmentId: string, expectedRevision: number) =>
  (await api.post<RuntimeEnvironment>(`/api/workspaces/${workspaceId}/environments/${environmentId}/suspend`, { expectedRevision })).data;

export const archiveEnvironment = async (workspaceId: string, environmentId: string, expectedRevision: number) =>
  (await api.post<RuntimeEnvironment>(`/api/workspaces/${workspaceId}/environments/${environmentId}/archive`, { expectedRevision })).data;

export const makeDefaultEnvironment = async (workspaceId: string, environmentId: string, expectedRevision: number) =>
  (await api.post<RuntimeEnvironment>(`/api/workspaces/${workspaceId}/environments/${environmentId}/make-default`, { expectedRevision })).data;

// ─── Workspace-scope settings ─────────────────────────────────────────────────

export const listWorkspaceSettings = async (workspaceId: string) =>
  (await api.get<SettingValue[]>(`/api/workspaces/${workspaceId}/settings`)).data;

export const getEffectiveWorkspaceSettings = async (workspaceId: string, environmentId?: string) =>
  (await api.get<EffectiveSetting[]>(`/api/workspaces/${workspaceId}/settings/effective`, { params: { environmentId } })).data;

export const upsertWorkspaceSetting = async (workspaceId: string, settingKey: string, input: UpsertSettingInput) =>
  (await api.patch<SettingValue>(`/api/workspaces/${workspaceId}/settings/${encodeURIComponent(settingKey)}`, input)).data;

export const deleteWorkspaceSetting = async (workspaceId: string, settingKey: string) =>
  api.delete(`/api/workspaces/${workspaceId}/settings/${encodeURIComponent(settingKey)}`);

// ─── Environment-scope settings ───────────────────────────────────────────────

export const getEffectiveEnvironmentSettings = async (workspaceId: string, environmentId: string) =>
  (await api.get<EffectiveSetting[]>(`/api/workspaces/${workspaceId}/environments/${environmentId}/settings/effective`)).data;

export const listEnvironmentSettings = async (workspaceId: string, environmentId: string) =>
  (await api.get<SettingValue[]>(`/api/workspaces/${workspaceId}/environments/${environmentId}/settings`)).data;

export const upsertEnvironmentSetting = async (workspaceId: string, environmentId: string, settingKey: string, input: UpsertSettingInput) =>
  (await api.patch<SettingValue>(`/api/workspaces/${workspaceId}/environments/${environmentId}/settings/${encodeURIComponent(settingKey)}`, input)).data;

export const deleteEnvironmentSetting = async (workspaceId: string, environmentId: string, settingKey: string) =>
  api.delete(`/api/workspaces/${workspaceId}/environments/${environmentId}/settings/${encodeURIComponent(settingKey)}`);

export const getChangeHistory = async (workspaceId: string, environmentId?: string, take = 100) =>
  environmentId
    ? (await api.get<ConfigurationChange[]>(`/api/workspaces/${workspaceId}/environments/${environmentId}/settings/changes`, { params: { take } })).data
    : (await api.get<ConfigurationChange[]>(`/api/workspaces/${workspaceId}/settings/changes`, { params: { take } })).data;

// ─── Validation, import/export ────────────────────────────────────────────────

export const validateEnvironmentSettings = async (workspaceId: string, environmentId: string) =>
  (await api.post<SettingsValidationResult>(`/api/workspaces/${workspaceId}/environments/${environmentId}/settings/validate`)).data;

export const validateProvider = async (workspaceId: string, environmentId: string) =>
  (await api.post<ProviderValidationResult>(`/api/workspaces/${workspaceId}/environments/${environmentId}/settings/provider/validate`)).data;

export const exportConfiguration = async (workspaceId: string, environmentId: string) =>
  (await api.get<ConfigurationExport>(`/api/workspaces/${workspaceId}/environments/${environmentId}/settings/export`)).data;

export const importConfiguration = async (workspaceId: string, environmentId: string, settingsJson: string, validateOnly: boolean, reason?: string) =>
  (await api.post<ConfigurationChange[]>(`/api/workspaces/${workspaceId}/environments/${environmentId}/settings/import`, { settingsJson, validateOnly, reason })).data;

// ─── Secret references ────────────────────────────────────────────────────────

export const listSecretReferences = async (workspaceId: string) =>
  (await api.get<SecretReference[]>(`/api/workspaces/${workspaceId}/secret-references`)).data;

export const createSecretReference = async (workspaceId: string, displayName: string, reference: string) =>
  (await api.post<SecretReference>(`/api/workspaces/${workspaceId}/secret-references`, { displayName, reference })).data;

export const validateSecretReference = async (workspaceId: string, referenceId: string) =>
  (await api.post<SecretReference>(`/api/workspaces/${workspaceId}/secret-references/${referenceId}/validate`)).data;

export const disableSecretReference = async (workspaceId: string, referenceId: string, expectedRevision: number) =>
  (await api.post<SecretReference>(`/api/workspaces/${workspaceId}/secret-references/${referenceId}/disable`, { expectedRevision })).data;
