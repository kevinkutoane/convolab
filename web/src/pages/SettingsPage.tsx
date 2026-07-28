import { useMemo, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  AlertTriangle,
  Archive,
  CheckCircle2,
  Download,
  History,
  KeyRound,
  Layers,
  Play,
  Plus,
  RotateCcw,
  ShieldCheck,
  Star,
  Upload,
  XCircle,
} from "lucide-react";
import { EmptyState, ErrorState, LoadingState } from "../components/AsyncStates";
import { useAuth } from "../contexts/useAuth";
import { useEnvironment } from "../contexts/EnvironmentContext";
import { getApiErrorMessage } from "../services/apiClient";
import {
  activateEnvironment,
  archiveEnvironment,
  createEnvironment,
  createSecretReference,
  deleteEnvironmentSetting,
  deleteWorkspaceSetting,
  disableSecretReference,
  exportConfiguration,
  getChangeHistory,
  getEffectiveEnvironmentSettings,
  importConfiguration,
  listSecretReferences,
  makeDefaultEnvironment,
  suspendEnvironment,
  upsertEnvironmentSetting,
  validateEnvironmentSettings,
  validateProvider,
  validateSecretReference,
} from "../services/settingsApi";
import type {
  ConfigurationChange,
  EffectiveSetting,
  RuntimeEnvironment,
  SecretReference,
} from "../types/settings";

// ─── Tab model ────────────────────────────────────────────────────────────────

interface SettingsTabDefinition {
  id: string;
  label: string;
  /** Effective-setting categories rendered by this tab; absent for bespoke tabs. */
  categories?: readonly string[];
}

const settingsTabs: readonly SettingsTabDefinition[] = [
  { id: "environments", label: "Environments" },
  { id: "general", label: "General", categories: ["General"] },
  { id: "provider", label: "AI Provider", categories: ["AI Provider"] },
  { id: "budget", label: "Budgets", categories: ["Budget"] },
  { id: "evaluation", label: "Evaluation", categories: ["Evaluation"] },
  { id: "retention", label: "Trace & Retention", categories: ["Trace & Retention", "Retention"] },
  { id: "features", label: "Feature Flags", categories: ["Feature Flags", "Features"] },
  { id: "governance", label: "Policies & Plugins", categories: ["Policy", "Policies", "Plugins", "Plugin"] },
  { id: "secrets", label: "Secrets" },
  { id: "history", label: "Change History" },
  { id: "transfer", label: "Import / Export" },
];

type SettingsTabId = string;

export function SettingsPage() {
  const auth = useAuth();
  const environment = useEnvironment();
  const workspaceId = auth.session?.activeWorkspaceId;
  const [tab, setTab] = useState<SettingsTabId>("environments");

  if (!workspaceId) {
    return <EmptyState title="No workspace selected" description="Select a workspace to manage its configuration." />;
  }

  return (
    <div className="settings-page">
      <nav className="settings-tabs" role="tablist" aria-label="Settings sections">
        {settingsTabs.map(item => (
          <button
            key={item.id}
            role="tab"
            aria-selected={tab === item.id}
            className={`settings-tab${tab === item.id ? " active" : ""}`}
            onClick={() => setTab(item.id)}
          >
            {item.label}
          </button>
        ))}
      </nav>

      {tab === "environments" && <EnvironmentsTab workspaceId={workspaceId} />}
      {tab === "secrets" && <SecretsTab workspaceId={workspaceId} />}
      {tab === "history" && <HistoryTab workspaceId={workspaceId} environmentId={environment.activeEnvironmentId} />}
      {tab === "transfer" && environment.activeEnvironmentId && (
        <TransferTab workspaceId={workspaceId} environmentId={environment.activeEnvironmentId} environmentName={environment.activeEnvironment?.name ?? ""} />
      )}
      {settingsTabs.find(item => item.id === tab)?.categories && environment.activeEnvironment && (
        <CategorySettingsTab
          key={`${tab}-${environment.activeEnvironmentId}`}
          workspaceId={workspaceId}
          environment={environment.activeEnvironment}
          categories={settingsTabs.find(item => item.id === tab)!.categories!}
          showProviderValidation={tab === "provider"}
        />
      )}
      {settingsTabs.find(item => item.id === tab)?.categories && !environment.activeEnvironment && (
        <EmptyState title="No environment" description="Create an environment first to configure settings." />
      )}
    </div>
  );
}

// ─── Environments tab ─────────────────────────────────────────────────────────

function EnvironmentsTab({ workspaceId }: { workspaceId: string }) {
  const environment = useEnvironment();
  const queryClient = useQueryClient();
  const [formOpen, setFormOpen] = useState(false);
  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");
  const [type, setType] = useState("Development");
  const [description, setDescription] = useState("");
  const [actionError, setActionError] = useState<string>();

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ["environments", workspaceId] });
    environment.refetch();
    window.dispatchEvent(new CustomEvent("convolab:environments-changed"));
  };

  const createMutation = useMutation({
    mutationFn: () => createEnvironment(workspaceId, { name, slug, environmentType: type, description, isDefault: false }),
    onSuccess: () => { setFormOpen(false); setName(""); setSlug(""); setDescription(""); refresh(); },
  });

  const runAction = useMutation({
    mutationFn: ({ action, target }: { action: string; target: RuntimeEnvironment }) => {
      switch (action) {
        case "activate": return activateEnvironment(workspaceId, target.id, target.revision);
        case "suspend": return suspendEnvironment(workspaceId, target.id, target.revision);
        case "archive": return archiveEnvironment(workspaceId, target.id, target.revision);
        default: return makeDefaultEnvironment(workspaceId, target.id, target.revision);
      }
    },
    onSuccess: () => { setActionError(undefined); refresh(); },
    onError: (error: unknown) => setActionError(getApiErrorMessage(error)),
  });

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="panel-eyebrow">Runtime isolation</span>
          <h2>Environments</h2>
        </div>
        <button className="primary-button" onClick={() => setFormOpen(value => !value)}><Plus size={15} />New environment</button>
      </div>

      {formOpen && (
        <form className="workspace-inline-form" onSubmit={(event: FormEvent) => { event.preventDefault(); createMutation.mutate(); }}>
          <input aria-label="Name" placeholder="Staging" value={name} onChange={event => { setName(event.target.value); setSlug(event.target.value.toLowerCase().replace(/[^a-z0-9]+/g, "-")); }} required />
          <input aria-label="Slug" placeholder="staging" value={slug} onChange={event => setSlug(event.target.value)} required />
          <select aria-label="Type" value={type} onChange={event => setType(event.target.value)}>
            {["Development", "Test", "Staging", "Production"].map(item => <option key={item}>{item}</option>)}
          </select>
          <input aria-label="Description" placeholder="Purpose of this environment" value={description} onChange={event => setDescription(event.target.value)} />
          <button className="primary-button" disabled={createMutation.isPending}>{createMutation.isPending ? "Creating…" : "Create"}</button>
          {createMutation.isError && <p role="alert">{getApiErrorMessage(createMutation.error)}</p>}
        </form>
      )}

      {actionError && <p className="settings-error" role="alert"><AlertTriangle size={14} /> {actionError}</p>}

      {environment.isLoading ? (
        <LoadingState compact />
      ) : !environment.environments.length ? (
        <EmptyState title="No environments" description="Create a Development environment to get started." />
      ) : (
        <div className="environment-list">
          {environment.environments.map(item => (
            <article key={item.id} className="environment-card">
              <div className="environment-card-head">
                <span className={`environment-type-badge type-${item.environmentType.toLowerCase()}`}>{item.environmentType}</span>
                {item.isDefault && <span className="default-badge"><Star size={12} /> Default</span>}
                <span className={`status-chip status-${item.status.toLowerCase()}`}>{item.status}</span>
              </div>
              <strong>{item.name}</strong>
              <small>{item.description || item.slug}</small>
              <div className="environment-card-actions">
                {item.status === "Suspended" && (
                  <button className="text-button" onClick={() => runAction.mutate({ action: "activate", target: item })}><Play size={13} /> Activate</button>
                )}
                {item.status === "Active" && (
                  <button className="text-button" onClick={() => runAction.mutate({ action: "suspend", target: item })}>Suspend</button>
                )}
                {!item.isDefault && (
                  <>
                    <button className="text-button" onClick={() => runAction.mutate({ action: "make-default", target: item })}><Star size={13} /> Make default</button>
                    <button className="text-button danger" onClick={() => runAction.mutate({ action: "archive", target: item })}><Archive size={13} /> Archive</button>
                  </>
                )}
              </div>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

// ─── Category settings tab (General / Provider / Budget / …) ─────────────────

function CategorySettingsTab({
  workspaceId,
  environment,
  categories,
  showProviderValidation,
}: {
  workspaceId: string;
  environment: RuntimeEnvironment;
  categories: readonly string[];
  showProviderValidation: boolean;
}) {
  const queryClient = useQueryClient();
  const effectiveQuery = useQuery({
    queryKey: ["effective-settings", workspaceId, environment.id],
    queryFn: () => getEffectiveEnvironmentSettings(workspaceId, environment.id),
  });

  const settings = useMemo(
    () => (effectiveQuery.data ?? []).filter(setting =>
      categories.some(category => setting.category.toLowerCase().includes(category.toLowerCase())),
    ),
    [effectiveQuery.data, categories],
  );

  const refresh = () => void queryClient.invalidateQueries({ queryKey: ["effective-settings", workspaceId, environment.id] });

  if (effectiveQuery.isLoading) return <LoadingState />;
  if (effectiveQuery.isError) return <ErrorState message={getApiErrorMessage(effectiveQuery.error)} onRetry={() => effectiveQuery.refetch()} />;

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="panel-eyebrow">Configuring <strong>{environment.name}</strong> ({environment.environmentType})</span>
          <h2>{categories[0]} settings</h2>
        </div>
        {showProviderValidation && <ProviderValidationButton workspaceId={workspaceId} environmentId={environment.id} />}
      </div>
      {environment.environmentType === "Production" && (
        <p className="production-warning" role="note">
          <ShieldCheck size={14} /> Production environment: every change requires a reason and is fully audited.
        </p>
      )}
      {!settings.length ? (
        <EmptyState title="No settings in this section" description="Definitions may still be provisioning for this workspace." />
      ) : (
        <div className="settings-grid">
          {settings.map(setting => (
            <SettingRow
              key={setting.key}
              workspaceId={workspaceId}
              environment={environment}
              setting={setting}
              onChanged={refresh}
            />
          ))}
        </div>
      )}
    </section>
  );
}

// ─── Single setting row with inline editing ───────────────────────────────────

function SettingRow({
  workspaceId,
  environment,
  setting,
  onChanged,
}: {
  workspaceId: string;
  environment: RuntimeEnvironment;
  setting: EffectiveSetting;
  onChanged: () => void;
}) {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(setting.effectiveValue?.replace(/^"|"$/g, "") ?? "");
  const [reason, setReason] = useState("");
  const [confirmProtected, setConfirmProtected] = useState(false);
  const isProduction = environment.environmentType === "Production";

  const encodeValue = (raw: string) => {
    if (setting.valueType === "Boolean") return raw === "true" ? "true" : "false";
    if (["Integer", "Decimal", "Percentage", "Currency", "Duration"].includes(setting.valueType)) return raw;
    if (setting.valueType === "Json") return raw;
    return JSON.stringify(raw);
  };

  const saveMutation = useMutation({
    mutationFn: () =>
      upsertEnvironmentSetting(workspaceId, environment.id, setting.key, {
        valueJson: encodeValue(value),
        reason: reason || undefined,
        confirmProtectedChange: confirmProtected,
      }),
    onSuccess: () => { setEditing(false); setReason(""); setConfirmProtected(false); onChanged(); },
  });

  const resetMutation = useMutation({
    mutationFn: async () => {
      // Remove the override at the scope it is set, falling back to inherit.
      if (setting.sourceScope === "Environment") {
        await deleteEnvironmentSetting(workspaceId, environment.id, setting.key);
      } else if (setting.sourceScope === "Workspace") {
        await deleteWorkspaceSetting(workspaceId, setting.key);
      }
    },
    onSuccess: onChanged,
  });

  const displayValue = setting.isSecret
    ? "••••••••"
    : setting.effectiveValue?.replace(/^"|"$/g, "") ?? "(not set)";

  return (
    <article className={`setting-row${editing ? " editing" : ""}`}>
      <div className="setting-meta">
        <strong>{setting.displayName}</strong>
        <small className="setting-key">{setting.key}</small>
        <div className="setting-chips">
          <span className={`scope-chip scope-${setting.sourceScope.toLowerCase()}`}>{setting.sourceScope}</span>
          {setting.isInherited && <span className="inherited-chip">Inherited</span>}
          {setting.requiresRestart && <span className="restart-chip">Restart required</span>}
          {setting.validationStatus && setting.validationStatus !== "Valid" && (
            <span className="validation-chip">{setting.validationStatus}</span>
          )}
        </div>
      </div>

      {!editing ? (
        <div className="setting-value-display">
          <code>{displayValue}</code>
          <div className="setting-row-actions">
            <button className="text-button" onClick={() => setEditing(true)}>Edit</button>
            {!setting.isInherited && setting.sourceScope !== "Platform" && (
              <button className="text-button" onClick={() => resetMutation.mutate()} disabled={resetMutation.isPending}>
                <RotateCcw size={12} /> Reset to inherited
              </button>
            )}
          </div>
        </div>
      ) : (
        <form
          className="setting-edit-form"
          onSubmit={(event: FormEvent) => { event.preventDefault(); saveMutation.mutate(); }}
        >
          {setting.valueType === "Boolean" ? (
            <select aria-label={`Value for ${setting.displayName}`} value={value} onChange={event => setValue(event.target.value)}>
              <option value="true">true</option>
              <option value="false">false</option>
            </select>
          ) : (
            <input
              aria-label={`Value for ${setting.displayName}`}
              value={value}
              onChange={event => setValue(event.target.value)}
              placeholder={setting.isSecret ? "env:VARIABLE_NAME" : "Value"}
            />
          )}
          {isProduction && (
            <input
              aria-label="Reason for change"
              value={reason}
              onChange={event => setReason(event.target.value)}
              placeholder="Reason (required in Production)"
              required
            />
          )}
          {isProduction && value === "false" && setting.key.includes("enforcement") && (
            <label className="confirm-protected">
              <input type="checkbox" checked={confirmProtected} onChange={event => setConfirmProtected(event.target.checked)} />
              I understand this disables enforcement in Production
            </label>
          )}
          <div className="setting-row-actions">
            <button className="primary-button" disabled={saveMutation.isPending}>{saveMutation.isPending ? "Saving…" : "Save"}</button>
            <button type="button" className="text-button" onClick={() => setEditing(false)}>Cancel</button>
          </div>
          {saveMutation.isError && <p role="alert" className="settings-error">{getApiErrorMessage(saveMutation.error)}</p>}
        </form>
      )}
    </article>
  );
}

// ─── Provider validation ──────────────────────────────────────────────────────

function ProviderValidationButton({ workspaceId, environmentId }: { workspaceId: string; environmentId: string }) {
  const settingsValidation = useMutation({ mutationFn: () => validateEnvironmentSettings(workspaceId, environmentId) });
  const providerValidation = useMutation({ mutationFn: () => validateProvider(workspaceId, environmentId) });

  return (
    <div className="provider-validation">
      <div className="provider-validation-buttons">
        <button className="secondary-button" onClick={() => settingsValidation.mutate()} disabled={settingsValidation.isPending}>
          <ShieldCheck size={15} /> {settingsValidation.isPending ? "Validating…" : "Validate settings"}
        </button>
        <button className="primary-button" onClick={() => providerValidation.mutate()} disabled={providerValidation.isPending}>
          <Play size={15} /> {providerValidation.isPending ? "Checking provider…" : "Test provider connection"}
        </button>
      </div>
      {providerValidation.data && (
        <p className={`validation-outcome ${providerValidation.data.outcome === "Valid" ? "ok" : "fail"}`} role="status">
          {providerValidation.data.outcome === "Valid" ? <CheckCircle2 size={14} /> : <XCircle size={14} />}
          {providerValidation.data.message} ({providerValidation.data.durationMs}ms)
        </p>
      )}
      {providerValidation.isError && <p className="settings-error" role="alert">{getApiErrorMessage(providerValidation.error)}</p>}
      {settingsValidation.data && (
        <p className={`validation-outcome ${settingsValidation.data.isValid ? "ok" : "fail"}`} role="status">
          {settingsValidation.data.isValid ? <CheckCircle2 size={14} /> : <XCircle size={14} />}
          {settingsValidation.data.checkedCount} settings checked — {settingsValidation.data.invalidCount} invalid, {settingsValidation.data.warningCount} warnings.
          {settingsValidation.data.entries.filter(entry => entry.status !== "Valid").slice(0, 3).map(entry => ` ${entry.key}: ${entry.message}`).join(";")}
        </p>
      )}
    </div>
  );
}

// ─── Secrets tab ──────────────────────────────────────────────────────────────

function SecretsTab({ workspaceId }: { workspaceId: string }) {
  const queryClient = useQueryClient();
  const [displayName, setDisplayName] = useState("");
  const [reference, setReference] = useState("");
  const query = useQuery({ queryKey: ["secret-references", workspaceId], queryFn: () => listSecretReferences(workspaceId) });
  const refresh = () => void queryClient.invalidateQueries({ queryKey: ["secret-references", workspaceId] });

  const createMutation = useMutation({
    mutationFn: () => createSecretReference(workspaceId, displayName, reference),
    onSuccess: () => { setDisplayName(""); setReference(""); refresh(); },
  });
  const validateMutation = useMutation({
    mutationFn: (id: string) => validateSecretReference(workspaceId, id),
    onSuccess: refresh,
  });
  const disableMutation = useMutation({
    mutationFn: (item: SecretReference) => disableSecretReference(workspaceId, item.id, item.revision),
    onSuccess: refresh,
  });

  const statusIcon = (status: SecretReference["status"]) =>
    status === "Valid" ? <CheckCircle2 size={14} className="ok" /> : status === "NotValidated" ? <History size={14} /> : <XCircle size={14} className="fail" />;

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="panel-eyebrow">Secret management</span>
          <h2>Secret references</h2>
        </div>
      </div>
      <p className="settings-note">
        Secrets are stored as <em>references</em> (for example <code>env:GEMINI_API_KEY</code>); the platform never stores or displays secret values.
      </p>
      <form className="workspace-inline-form" onSubmit={(event: FormEvent) => { event.preventDefault(); createMutation.mutate(); }}>
        <input aria-label="Display name" placeholder="Gemini production key" value={displayName} onChange={event => setDisplayName(event.target.value)} required />
        <input aria-label="Reference" placeholder="env:GEMINI_API_KEY" value={reference} onChange={event => setReference(event.target.value)} required />
        <button className="primary-button" disabled={createMutation.isPending}><KeyRound size={15} />{createMutation.isPending ? "Creating…" : "Register reference"}</button>
        {createMutation.isError && <p role="alert" className="settings-error">{getApiErrorMessage(createMutation.error)}</p>}
      </form>
      {query.isLoading ? (
        <LoadingState compact />
      ) : query.isError ? (
        <ErrorState message={getApiErrorMessage(query.error)} onRetry={() => query.refetch()} />
      ) : !query.data?.length ? (
        <EmptyState title="No secret references" description="Register a reference to use secrets in provider settings." />
      ) : (
        <div className="member-list">
          {query.data.map(item => (
            <article key={item.id}>
              <KeyRound size={16} />
              <div>
                <strong>{item.displayName}</strong>
                <small><code>{item.reference}</code> — {item.lastValidationOutcome ?? "Never validated"}</small>
              </div>
              <span className={`status-chip status-${item.status.toLowerCase()}`}>{statusIcon(item.status)} {item.status}</span>
              <span className="setting-row-actions">
                <button className="text-button" onClick={() => validateMutation.mutate(item.id)} disabled={validateMutation.isPending}>Validate</button>
                {!item.isDisabled && (
                  <button className="text-button danger" onClick={() => disableMutation.mutate(item)} disabled={disableMutation.isPending}>Disable</button>
                )}
              </span>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

// ─── Change history tab ───────────────────────────────────────────────────────

function HistoryTab({ workspaceId, environmentId }: { workspaceId: string; environmentId?: string }) {
  const query = useQuery({
    queryKey: ["setting-changes", workspaceId, environmentId],
    queryFn: () => getChangeHistory(workspaceId, environmentId),
  });

  if (query.isLoading) return <LoadingState />;
  if (query.isError) return <ErrorState message={getApiErrorMessage(query.error)} onRetry={() => query.refetch()} />;

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="panel-eyebrow">Audit trail</span>
          <h2>Configuration change history</h2>
        </div>
      </div>
      {!query.data?.length ? (
        <EmptyState title="No changes recorded" description="Configuration changes will appear here with full audit context." />
      ) : (
        <div className="change-history">
          {query.data.map((change: ConfigurationChange) => (
            <article key={change.id} className="change-entry">
              <History size={15} />
              <div>
                <strong>{change.settingKey}</strong>
                <small>
                  {change.previousValueSummary ? `${change.previousValueSummary} → ` : ""}{change.newValueSummary}
                  {change.reason ? ` — ${change.reason}` : ""}
                </small>
                <small className="change-meta">
                  {change.changedByDisplay} · {new Date(change.changedAt).toLocaleString("en-ZA")}
                  {change.environmentName ? ` · ${change.environmentName}` : ""} · corr {change.correlationId.slice(0, 12)}
                </small>
              </div>
              <span className={`status-chip status-${change.outcome.toLowerCase().replace(":", "-")}`}>{change.outcome}</span>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

// ─── Import / export tab ──────────────────────────────────────────────────────

function TransferTab({ workspaceId, environmentId, environmentName }: { workspaceId: string; environmentId: string; environmentName: string }) {
  const [importJson, setImportJson] = useState("");
  const [reason, setReason] = useState("");
  const [preview, setPreview] = useState<ConfigurationChange[]>();

  const exportMutation = useMutation({
    mutationFn: () => exportConfiguration(workspaceId, environmentId),
    onSuccess: data => {
      const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json" });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `convolab-${environmentName.toLowerCase().replace(/\s+/g, "-")}-config.json`;
      anchor.click();
      URL.revokeObjectURL(url);
    },
  });

  const previewMutation = useMutation({
    mutationFn: () => importConfiguration(workspaceId, environmentId, importJson, true, reason || undefined),
    onSuccess: setPreview,
  });

  const applyMutation = useMutation({
    mutationFn: () => importConfiguration(workspaceId, environmentId, importJson, false, reason || undefined),
    onSuccess: changes => { setPreview(changes); setImportJson(""); },
  });

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="panel-eyebrow">Environment promotion</span>
          <h2>Import / Export — {environmentName}</h2>
        </div>
        <button className="secondary-button" onClick={() => exportMutation.mutate()} disabled={exportMutation.isPending}>
          <Download size={15} /> {exportMutation.isPending ? "Exporting…" : "Export configuration"}
        </button>
      </div>
      <p className="settings-note">
        <Layers size={14} /> Export a snapshot of this environment's non-secret configuration, then import it into another environment. Secrets are never exported or imported.
      </p>
      <div className="transfer-import">
        <textarea
          aria-label="Import payload"
          rows={8}
          placeholder="Paste an exported configuration JSON here…"
          value={importJson}
          onChange={event => setImportJson(event.target.value)}
        />
        <input aria-label="Reason" placeholder="Reason for import (required in Production)" value={reason} onChange={event => setReason(event.target.value)} />
        <div className="setting-row-actions">
          <button className="secondary-button" onClick={() => previewMutation.mutate()} disabled={!importJson || previewMutation.isPending}>
            {previewMutation.isPending ? "Previewing…" : "Preview changes"}
          </button>
          <button className="primary-button" onClick={() => applyMutation.mutate()} disabled={!importJson || applyMutation.isPending}>
            <Upload size={15} /> {applyMutation.isPending ? "Importing…" : "Apply import"}
          </button>
        </div>
        {(previewMutation.isError || applyMutation.isError) && (
          <p role="alert" className="settings-error">{getApiErrorMessage(previewMutation.error ?? applyMutation.error)}</p>
        )}
        {preview && (
          <div className="import-preview">
            <strong>{preview.some(entry => entry.outcome.startsWith("Preview")) ? "Preview" : "Applied"} — {preview.length} entries</strong>
            {preview.map(entry => (
              <article key={entry.id} className="change-entry">
                <div>
                  <strong>{entry.settingKey}</strong>
                  <small>{entry.previousValueSummary ? `${entry.previousValueSummary} → ` : ""}{entry.newValueSummary}{entry.reason ? ` (${entry.reason})` : ""}</small>
                </div>
                <span className={`status-chip status-${entry.outcome.toLowerCase().replace(":", "-")}`}>{entry.outcome}</span>
              </article>
            ))}
          </div>
        )}
      </div>
    </section>
  );
}
