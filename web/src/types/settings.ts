// Types mirroring the Settings API contracts (SettingsContracts.cs).

export interface RuntimeEnvironment {
  id: string;
  organisationId: string;
  workspaceId: string;
  name: string;
  slug: string;
  environmentType: "Development" | "Test" | "Staging" | "Production";
  description: string;
  status: "Active" | "Suspended" | "Archived";
  isDefault: boolean;
  createdAt: string;
  updatedAt: string;
  revision: number;
}

export interface SettingValue {
  id: string;
  definitionKey: string;
  displayName: string;
  category: string;
  scope: string;
  organisationId?: string;
  workspaceId?: string;
  environmentId?: string;
  valueJson: string;
  isSecret: boolean;
  valueType: string;
  updatedAt: string;
  revision: number;
}

export interface EffectiveSetting {
  key: string;
  effectiveValue?: string;
  valueType: string;
  sourceScope: string;
  sourceId?: string;
  isInherited: boolean;
  isSecret: boolean;
  validationStatus: string;
  requiresRestart: boolean;
  displayName: string;
  category: string;
  inheritedFromDisplay?: string;
  description: string;
  isRequired: boolean;
  allowedValues: string[];
  allowsEnvironmentOverride: boolean;
}

export interface SecretReference {
  id: string;
  workspaceId: string;
  displayName: string;
  reference: string;
  provider: string;
  status: "NotValidated" | "Valid" | "Missing" | "Invalid" | "Unavailable";
  lastValidatedAt?: string;
  lastValidationOutcome?: string;
  isDisabled: boolean;
  createdAt: string;
  updatedAt: string;
  revision: number;
}

export interface ConfigurationChange {
  id: string;
  settingKey: string;
  previousValueSummary?: string;
  newValueSummary: string;
  changedByDisplay: string;
  changedAt: string;
  reason?: string;
  correlationId: string;
  outcome: string;
  environmentName?: string;
}

export interface ProviderValidationResult {
  outcome: string;
  message: string;
  secretResolved: boolean;
  providerReachable: boolean;
  authSucceeded: boolean;
  modelAvailable: boolean;
  durationMs: number;
}

export interface SettingValidationEntry {
  key: string;
  displayName: string;
  category: string;
  status: "Valid" | "Invalid" | "Warning";
  message?: string;
  sourceScope: string;
}

export interface SettingsValidationResult {
  isValid: boolean;
  checkedCount: number;
  invalidCount: number;
  warningCount: number;
  entries: SettingValidationEntry[];
  validatedAt: string;
}

export interface ConfigurationExport {
  schemaVersion: string;
  organisation: string;
  workspace: string;
  environment: string;
  exportedAt: string;
  settings: { key: string; category: string; displayName: string; value?: string }[];
  featureFlags: { key: string; value?: string }[];
  providerMetadata?: { provider?: string; model?: string; providerEnabled: boolean };
}

export interface UpsertSettingInput {
  valueJson: string;
  reason?: string;
  expectedRevision?: number;
  confirmProtectedChange?: boolean;
}

export interface CreateEnvironmentInput {
  name: string;
  slug: string;
  environmentType: string;
  description?: string;
  isDefault: boolean;
}

export interface UpdateEnvironmentInput {
  name: string;
  description?: string;
  environmentType: string;
  expectedRevision: number;
}
