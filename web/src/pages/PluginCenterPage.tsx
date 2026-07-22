import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import {
  Activity,
  Boxes,
  BookOpen,
  CheckCircle2,
  CircleAlert,
  Clock3,
  FileJson2,
  Filter,
  HeartPulse,
  History,
  LoaderCircle,
  PackagePlus,
  Pencil,
  PlugZap,
  Power,
  PowerOff,
  RefreshCw,
  Save,
  ShieldCheck,
  Tags,
  X,
  XCircle,
} from "lucide-react";
import { MetricCard } from "../components/MetricCard";
import { getApiErrorMessage } from "../services/apiClient";
import { ErrorState, LoadingState } from "../components/AsyncStates";
import {
  checkPluginHealth,
  getPlugin,
  getPluginOverview,
  registerPlugin,
  transitionPlugin,
  updatePlugin,
  updatePluginVersion,
} from "../services/pluginApi";
import type {
  PluginCategory,
  PluginDetail,
  PluginHealthStatus,
  PluginStatus,
  RegisterPluginInput,
} from "../types/plugin";
import "../functional-workspaces.css";
import "../plugin-center.css";

const categories: PluginCategory[] = [
  "Provider",
  "Tool",
  "KnowledgeConnector",
  "Channel",
  "Evaluator",
  "TraceExporter",
  "WorkflowNode",
  "EnterpriseConnector",
];
const statuses: PluginStatus[] = ["Installed", "Active", "Inactive", "Deprecated"];
const healthStatuses: PluginHealthStatus[] = ["Unknown", "Healthy", "Degraded", "Unhealthy"];

type EditorMode = "register" | "edit" | "version" | null;

type PluginForm = {
  key: string;
  name: string;
  description: string;
  publisher: string;
  version: string;
  category: PluginCategory;
  manifestUrl: string;
  entryPoint: string;
  platformApiVersion: string;
  capabilitiesText: string;
  permissionsText: string;
  configurationSchema: string;
  metadataText: string;
};

const emptyForm: PluginForm = {
  key: "",
  name: "",
  description: "",
  publisher: "ConvoLab Community",
  version: "1.0.0",
  category: "Tool",
  manifestUrl: "https://",
  entryPoint: "",
  platformApiVersion: "1.0",
  capabilitiesText: "",
  permissionsText: "",
  configurationSchema: "{}",
  metadataText: "trustLevel=community",
};

export function PluginCenterPage() {
  const queryClient = useQueryClient();
  const [selectedPluginId, setSelectedPluginId] = useState("");
  const [categoryFilter, setCategoryFilter] = useState<PluginCategory | "All">("All");
  const [statusFilter, setStatusFilter] = useState<PluginStatus | "All">("All");
  const [healthFilter, setHealthFilter] = useState<PluginHealthStatus | "All">("All");
  const [editorMode, setEditorMode] = useState<EditorMode>(null);
  const [form, setForm] = useState<PluginForm>(emptyForm);
  const [notice, setNotice] = useState("");

  const overviewQuery = useQuery({ queryKey: ["plugin-overview"], queryFn: getPluginOverview });
  const overview = overviewQuery.data;
  const filteredPlugins = useMemo(() => (overview?.plugins ?? []).filter(plugin =>
    (categoryFilter === "All" || plugin.category === categoryFilter)
    && (statusFilter === "All" || plugin.status === statusFilter)
    && (healthFilter === "All" || plugin.healthStatus === healthFilter)),
  [overview, categoryFilter, statusFilter, healthFilter]);
  const selectedVisible = filteredPlugins.some(plugin => plugin.id === selectedPluginId);
  const effectivePluginId = selectedVisible ? selectedPluginId : filteredPlugins[0]?.id || "";
  const detailQuery = useQuery({
    queryKey: ["plugin-detail", effectivePluginId],
    queryFn: () => getPlugin(effectivePluginId),
    enabled: Boolean(effectivePluginId),
  });
  const detail = detailQuery.data;

  const invalidate = async (pluginId?: string) => {
    await queryClient.invalidateQueries({ queryKey: ["plugin-overview"] });
    if (pluginId) await queryClient.invalidateQueries({ queryKey: ["plugin-detail", pluginId] });
    await queryClient.invalidateQueries({ queryKey: ["platform-status"] });
  };

  const registerMutation = useMutation({
    mutationFn: (input: RegisterPluginInput) => registerPlugin(input),
    onMutate: () => setNotice(""),
    onSuccess: async created => {
      setSelectedPluginId(created.summary.id);
      setEditorMode(null);
      setNotice(`${created.summary.name} v${created.summary.version} was registered.`);
      await invalidate(created.summary.id);
    },
  });
  const updateMutation = useMutation({
    mutationFn: ({ pluginId, input }: { pluginId: string; input: Parameters<typeof updatePlugin>[1] }) => updatePlugin(pluginId, input),
    onMutate: () => setNotice(""),
    onSuccess: async updated => {
      setEditorMode(null);
      setNotice(`${updated.summary.name} metadata was updated.`);
      await invalidate(updated.summary.id);
    },
  });
  const versionMutation = useMutation({
    mutationFn: ({ pluginId, version, manifestUrl, revision }: { pluginId: string; version: string; manifestUrl: string; revision: number }) =>
      updatePluginVersion(pluginId, { version, manifestUrl, revision }),
    onMutate: () => setNotice(""),
    onSuccess: async created => {
      setSelectedPluginId(created.summary.id);
      setEditorMode(null);
      setNotice(`${created.summary.name} v${created.summary.version} was created as an immutable successor.`);
      await invalidate(created.summary.id);
    },
  });
  const transitionMutation = useMutation({
    mutationFn: ({ pluginId, action }: { pluginId: string; action: string }) => transitionPlugin(pluginId, action),
    onMutate: () => setNotice(""),
    onSuccess: async updated => {
      setNotice(`${updated.summary.name} is now ${updated.summary.status.toLowerCase()}.`);
      await invalidate(updated.summary.id);
    },
  });
  const healthMutation = useMutation({
    mutationFn: (pluginId: string) => checkPluginHealth(pluginId),
    onMutate: () => setNotice(""),
    onSuccess: async result => {
      setNotice(`Health check completed with ${result.status.toLowerCase()} status.`);
      await invalidate(result.pluginId);
    },
  });

  const error = overviewQuery.error ?? detailQuery.error ?? registerMutation.error ?? updateMutation.error
    ?? versionMutation.error ?? transitionMutation.error ?? healthMutation.error;

  if (overviewQuery.isLoading) return <LoadingState label="Loading Plugin Center…" />;
  if (overviewQuery.isError && !overview) return <ErrorState message={getApiErrorMessage(overviewQuery.error)} onRetry={() => void overviewQuery.refetch()} />;

  const startRegister = () => {
    setForm({ ...emptyForm });
    setEditorMode("register");
  };
  const startEdit = () => {
    if (!detail) return;
    setForm(toForm(detail));
    setEditorMode("edit");
  };
  const startVersion = () => {
    if (!detail) return;
    setForm({ ...toForm(detail), version: incrementPatch(detail.summary.version) });
    setEditorMode("version");
  };
  const save = () => {
    if (editorMode === "register") {
      registerMutation.mutate(toRegisterInput(form));
      return;
    }
    if (!detail) return;
    if (editorMode === "version") {
      versionMutation.mutate({
        pluginId: detail.summary.id,
        version: form.version,
        manifestUrl: form.manifestUrl,
        revision: detail.summary.revision,
      });
      return;
    }
    if (editorMode === "edit") {
      updateMutation.mutate({
        pluginId: detail.summary.id,
        input: {
          name: form.name,
          description: form.description,
          publisher: form.publisher,
          category: form.category,
          entryPoint: form.entryPoint,
          platformApiVersion: form.platformApiVersion,
          capabilities: parseLines(form.capabilitiesText),
          permissions: parseLines(form.permissionsText),
          configurationSchema: form.configurationSchema || "{}",
          metadata: parsePairs(form.metadataText),
          revision: detail.summary.revision,
        },
      });
    }
  };

  const saving = registerMutation.isPending || updateMutation.isPending || versionMutation.isPending;

  return (
    <div className="plugin-center-page">
      <header className="studio-page-header plugin-page-header">
        <div>
          <span className="page-eyebrow">Extension governance capability</span>
          <h1>Plugin Center</h1>
          <p>Register provider, connector, tool, evaluator, channel, and exporter adapters without coupling Platform Core to vendor-specific implementations.</p>
        </div>
        <div className="plugin-header-actions">
          <Link className="secondary-button" to="/documentation/plugins"><BookOpen size={16} /> Documentation</Link>
          <button className="secondary-button" onClick={() => overviewQuery.refetch()} disabled={overviewQuery.isFetching}>
            <RefreshCw size={16} className={overviewQuery.isFetching ? "spin" : ""} /> Refresh
          </button>
          <button className="primary-button" onClick={startRegister}><PackagePlus size={16} /> Register plugin</button>
        </div>
      </header>

      {error && <div className="provider-warning"><XCircle size={16} /> {getApiErrorMessage(error)}</div>}
      {notice && <div className="plugin-success" role="status"><CheckCircle2 size={16} /> {notice}</div>}

      <section className="metric-grid plugin-metric-grid">
        <MetricCard icon={Boxes} label="Registered" value={formatNumber(overview?.metrics.registered)} detail={`${formatNumber(overview?.metrics.categories)} extension categories`} tone="accent" />
        <MetricCard icon={Power} label="Active" value={formatNumber(overview?.metrics.active)} detail="Available to Platform Core" tone="positive" />
        <MetricCard icon={HeartPulse} label="Healthy" value={formatNumber(overview?.metrics.healthy)} detail={`${formatNumber(overview?.metrics.healthChecks)} recorded probes`} />
        <MetricCard icon={CircleAlert} label="Unhealthy" value={formatNumber(overview?.metrics.unhealthy)} detail="Blocked from activation" tone={(overview?.metrics.unhealthy ?? 0) > 0 ? "warning" : "default"} />
      </section>

      {editorMode && (
        <PluginEditor
          mode={editorMode}
          form={form}
          onChange={setForm}
          onCancel={() => setEditorMode(null)}
          onSave={save}
          saving={saving}
        />
      )}

      <section className="plugin-workspace">
        <aside className="panel plugin-list-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Extension registry</span><h3>Plugins</h3></div><PlugZap size={18} /></div>
          <div className="plugin-filter-grid">
            <label><Filter size={13} /><select aria-label="Filter plugins by category" value={categoryFilter} onChange={event => setCategoryFilter(event.target.value as PluginCategory | "All")}><option>All</option>{categories.map(value => <option key={value}>{value}</option>)}</select></label>
            <label><select aria-label="Filter plugins by lifecycle status" value={statusFilter} onChange={event => setStatusFilter(event.target.value as PluginStatus | "All")}><option>All</option>{statuses.map(value => <option key={value}>{value}</option>)}</select></label>
            <label><select aria-label="Filter plugins by health" value={healthFilter} onChange={event => setHealthFilter(event.target.value as PluginHealthStatus | "All")}><option>All</option>{healthStatuses.map(value => <option key={value}>{value}</option>)}</select></label>
          </div>
          <div className="plugin-list">
            {filteredPlugins.map(plugin => (
              <button key={plugin.id} className={`plugin-list-item ${effectivePluginId === plugin.id ? "selected" : ""}`} aria-pressed={effectivePluginId === plugin.id} onClick={() => setSelectedPluginId(plugin.id)}>
                <span className={`plugin-health-dot health-${plugin.healthStatus.toLowerCase()}`} />
                <div><strong>{plugin.name}</strong><span>{friendly(plugin.category)} · v{plugin.version}</span><small>{plugin.publisher}</small></div>
                <span className={`plugin-status-pill status-${plugin.status.toLowerCase()}`}>{plugin.status}</span>
              </button>
            ))}
            {!overviewQuery.isLoading && filteredPlugins.length === 0 && <div className="plugin-empty-list">No plugins match the selected filters.</div>}
          </div>
        </aside>

        <main className="panel plugin-detail-panel">
          {detailQuery.isLoading && <div className="plugin-loading"><LoaderCircle className="spin" /> Loading plugin contract…</div>}
          {!detailQuery.isLoading && detail && (
            <PluginInspector
              key={detail.summary.id}
              detail={detail}
              onEdit={startEdit}
              onVersion={startVersion}
              onHealth={() => healthMutation.mutate(detail.summary.id)}
              onTransition={action => transitionMutation.mutate({ pluginId: detail.summary.id, action })}
              busy={healthMutation.isPending || transitionMutation.isPending}
              onSelectVersion={setSelectedPluginId}
            />
          )}
          {!detailQuery.isLoading && !detail && <div className="plugin-loading"><PlugZap /> Register a plugin to begin.</div>}
        </main>
      </section>

      <section className="panel plugin-health-panel">
        <div className="panel-header"><div><span className="panel-eyebrow">Operational evidence</span><h3>Recent health checks</h3></div><Activity size={18} /></div>
        <div className="plugin-health-table">
          {(overview?.recentHealthChecks ?? []).map(check => {
            const plugin = overview?.plugins.find(item => item.id === check.pluginId);
            return <div className="plugin-health-row" key={check.id}>
              <span className={`plugin-health-icon health-${check.status.toLowerCase()}`}>{check.status === "Healthy" ? <CheckCircle2 size={15} /> : <CircleAlert size={15} />}</span>
              <div><strong>{plugin?.name ?? "Plugin"}</strong><p>{check.message}</p></div>
              <span>{check.source}</span><span>{check.durationMs} ms</span><time>{formatDate(check.checkedAt)}</time>
            </div>;
          })}
          {!overviewQuery.isLoading && (overview?.recentHealthChecks.length ?? 0) === 0 && <div className="plugin-empty-list">No health checks have been recorded.</div>}
        </div>
      </section>
    </div>
  );
}

function PluginInspector({
  detail,
  onEdit,
  onVersion,
  onHealth,
  onTransition,
  busy,
  onSelectVersion,
}: {
  detail: PluginDetail;
  onEdit: () => void;
  onVersion: () => void;
  onHealth: () => void;
  onTransition: (action: string) => void;
  busy: boolean;
  onSelectVersion: (id: string) => void;
}) {
  const plugin = detail.summary;
  const [confirmingDeprecation, setConfirmingDeprecation] = useState(false);
  const canEdit = plugin.status !== "Active" && plugin.status !== "Deprecated";
  return <div className="plugin-detail-content">
    <div className="plugin-detail-heading">
      <div>
        <div className="plugin-detail-badges"><span className={`plugin-status-pill status-${plugin.status.toLowerCase()}`}>{plugin.status}</span><span className={`plugin-health-pill health-${plugin.healthStatus.toLowerCase()}`}>{plugin.healthStatus}</span>{plugin.compatible ? <span className="plugin-compatible"><ShieldCheck size={13} /> API compatible</span> : <span className="plugin-incompatible"><CircleAlert size={13} /> API incompatible</span>}</div>
        <h2>{plugin.name} <small>v{plugin.version}</small></h2>
        <p>{plugin.description || "No description supplied."}</p>
      </div>
      <div className="plugin-detail-actions">
        <button className="secondary-button" onClick={onHealth} disabled={busy}><HeartPulse size={15} /> Check health</button>
        {canEdit && <button className="secondary-button" onClick={onEdit}><Pencil size={15} /> Edit</button>}
        <button className="secondary-button" onClick={onVersion} disabled={plugin.status === "Deprecated"}><PackagePlus size={15} /> New version</button>
        {plugin.status === "Active" ? <button className="secondary-button" onClick={() => onTransition("deactivate")} disabled={busy}><PowerOff size={15} /> Deactivate</button> : plugin.status !== "Deprecated" && <button className="primary-button" onClick={() => onTransition("activate")} disabled={busy || !plugin.compatible}><Power size={15} /> Activate</button>}
      </div>
    </div>

    <dl className="plugin-fact-grid">
      <div><dt>Registry key</dt><dd>{plugin.key}</dd></div>
      <div><dt>Category</dt><dd>{friendly(plugin.category)}</dd></div>
      <div><dt>Publisher</dt><dd>{plugin.publisher}</dd></div>
      <div><dt>Platform API</dt><dd>{plugin.platformApiVersion}</dd></div>
      <div><dt>Entry point</dt><dd>{detail.entryPoint || "Manifest defined"}</dd></div>
      <div><dt>Last check</dt><dd>{plugin.lastHealthCheckAt ? formatDate(plugin.lastHealthCheckAt) : "Never"}</dd></div>
    </dl>

    <div className="plugin-contract-grid">
      <section><div className="section-heading"><div><span className="panel-eyebrow">Capability contract</span><h3>Capabilities</h3></div><Tags size={16} /></div><div className="plugin-chip-list">{plugin.capabilities.map(value => <code key={value}>{value}</code>)}{plugin.capabilities.length === 0 && <em>No capabilities declared</em>}</div></section>
      <section><div className="section-heading"><div><span className="panel-eyebrow">Security contract</span><h3>Permissions</h3></div><ShieldCheck size={16} /></div><div className="plugin-chip-list">{detail.permissions.map(value => <code key={value}>{value}</code>)}{detail.permissions.length === 0 && <em>No elevated permissions</em>}</div></section>
    </div>

    <section className="plugin-manifest-section">
      <div className="section-heading"><div><span className="panel-eyebrow">Distribution contract</span><h3>Manifest</h3></div><FileJson2 size={16} /></div>
      <div className="plugin-manifest-card"><code>{plugin.manifestUrl}</code><p>{plugin.healthMessage}</p></div>
    </section>

    <section className="plugin-version-section">
      <div className="section-heading"><div><span className="panel-eyebrow">Immutable history</span><h3>Versions</h3></div><History size={16} /></div>
      <div className="plugin-version-list">{detail.versionHistory.map(version => <button key={version.id} className={version.id === plugin.id ? "selected" : ""} aria-pressed={version.id === plugin.id} onClick={() => onSelectVersion(version.id)}><strong>v{version.version}</strong><span>{version.status}</span><small>{formatDate(version.updatedAt)}</small></button>)}</div>
    </section>

    <section className="plugin-history-section">
      <div className="section-heading"><div><span className="panel-eyebrow">Probe history</span><h3>Health evidence</h3></div><Clock3 size={16} /></div>
      <div className="plugin-mini-history">{detail.healthHistory.slice(0, 8).map(check => <div key={check.id}><span className={`plugin-health-dot health-${check.status.toLowerCase()}`} /><strong>{check.status}</strong><p>{check.message}</p><time>{formatDate(check.checkedAt)} · {check.durationMs} ms</time></div>)}{detail.healthHistory.length === 0 && <em>No health evidence recorded for this version.</em>}</div>
    </section>

    {plugin.status !== "Deprecated" && (confirmingDeprecation
      ? <div className="plugin-deprecation-confirm" role="dialog" aria-label="Confirm plugin deprecation"><div><CircleAlert size={17} /><span><strong>Deprecate v{plugin.version}?</strong><small>This version becomes immutable and cannot be activated again.</small></span></div><div><button className="secondary-button" onClick={() => setConfirmingDeprecation(false)}>Cancel</button><button className="plugin-deprecate-button" onClick={() => { setConfirmingDeprecation(false); onTransition("deprecate"); }} disabled={busy}>Deprecate version</button></div></div>
      : <button className="plugin-deprecate-button" onClick={() => setConfirmingDeprecation(true)} disabled={busy}><CircleAlert size={14} /> Deprecate this version</button>)}
  </div>;
}

function PluginEditor({ mode, form, onChange, onCancel, onSave, saving }: {
  mode: Exclude<EditorMode, null>;
  form: PluginForm;
  onChange: (form: PluginForm) => void;
  onCancel: () => void;
  onSave: () => void;
  saving: boolean;
}) {
  const field = <K extends keyof PluginForm>(key: K, value: PluginForm[K]) => onChange({ ...form, [key]: value });
  const versionOnly = mode === "version";
  return <section className="panel plugin-editor-panel">
    <div className="panel-header"><div><span className="panel-eyebrow">{mode === "register" ? "New extension" : mode === "version" ? "Immutable successor" : "Registry metadata"}</span><h3>{mode === "register" ? "Register plugin" : mode === "version" ? "Create plugin version" : "Edit plugin"}</h3></div><button className="icon-button" onClick={onCancel} aria-label="Close editor"><X size={16} /></button></div>
    {versionOnly ? <div className="plugin-editor-grid compact"><label>Version<input value={form.version} onChange={event => field("version", event.target.value)} /></label><label>Manifest URL<input value={form.manifestUrl} onChange={event => field("manifestUrl", event.target.value)} /></label></div> : <>
      <div className="plugin-editor-grid">
        <label>Registry key<input value={form.key} disabled={mode === "edit"} onChange={event => field("key", event.target.value)} placeholder="claims-tool" /></label>
        <label>Name<input value={form.name} onChange={event => field("name", event.target.value)} /></label>
        <label>Publisher<input value={form.publisher} onChange={event => field("publisher", event.target.value)} /></label>
        <label>Version<input value={form.version} disabled={mode === "edit"} onChange={event => field("version", event.target.value)} /></label>
        <label>Category<select value={form.category} onChange={event => field("category", event.target.value as PluginCategory)}>{categories.map(value => <option key={value}>{value}</option>)}</select></label>
        <label>Platform API<input value={form.platformApiVersion} onChange={event => field("platformApiVersion", event.target.value)} /></label>
        <label className="plugin-wide-field">Description<textarea value={form.description} onChange={event => field("description", event.target.value)} /></label>
        <label className="plugin-wide-field">Manifest URL<input value={form.manifestUrl} disabled={mode === "edit"} onChange={event => field("manifestUrl", event.target.value)} /></label>
        <label>Entry point<input value={form.entryPoint} onChange={event => field("entryPoint", event.target.value)} placeholder="Namespace.Adapter" /></label>
        <label>Capabilities<textarea value={form.capabilitiesText} onChange={event => field("capabilitiesText", event.target.value)} placeholder="One capability per line" /></label>
        <label>Permissions<textarea value={form.permissionsText} onChange={event => field("permissionsText", event.target.value)} placeholder="One permission per line" /></label>
        <label>Metadata<textarea value={form.metadataText} onChange={event => field("metadataText", event.target.value)} placeholder="key=value" /></label>
        <label className="plugin-wide-field">Configuration schema<textarea className="plugin-code-input" value={form.configurationSchema} onChange={event => field("configurationSchema", event.target.value)} /></label>
      </div>
    </>}
    <div className="plugin-editor-actions"><button className="secondary-button" onClick={onCancel}>Cancel</button><button className="primary-button" onClick={onSave} disabled={saving}>{saving ? <LoaderCircle size={15} className="spin" /> : <Save size={15} />} Save</button></div>
  </section>;
}

function toForm(detail: PluginDetail): PluginForm {
  return {
    key: detail.summary.key,
    name: detail.summary.name,
    description: detail.summary.description,
    publisher: detail.summary.publisher,
    version: detail.summary.version,
    category: detail.summary.category,
    manifestUrl: detail.summary.manifestUrl,
    entryPoint: detail.entryPoint,
    platformApiVersion: detail.summary.platformApiVersion,
    capabilitiesText: detail.summary.capabilities.join("\n"),
    permissionsText: detail.permissions.join("\n"),
    configurationSchema: detail.configurationSchema,
    metadataText: Object.entries(detail.metadata).map(([key, value]) => `${key}=${value}`).join("\n"),
  };
}

function toRegisterInput(form: PluginForm): RegisterPluginInput {
  return {
    key: form.key,
    name: form.name,
    description: form.description,
    publisher: form.publisher,
    version: form.version,
    category: form.category,
    manifestUrl: form.manifestUrl,
    entryPoint: form.entryPoint,
    platformApiVersion: form.platformApiVersion,
    capabilities: parseLines(form.capabilitiesText),
    permissions: parseLines(form.permissionsText),
    configurationSchema: form.configurationSchema || "{}",
    metadata: parsePairs(form.metadataText),
  };
}

function parseLines(value: string): string[] {
  return value.split(/[\n,]/).map(item => item.trim()).filter(Boolean);
}
function parsePairs(value: string): Record<string, string> {
  return Object.fromEntries(value.split("\n").map(line => line.trim()).filter(Boolean).map(line => {
    const index = line.indexOf("=");
    return index < 0 ? [line, "true"] : [line.slice(0, index).trim(), line.slice(index + 1).trim()];
  }));
}
function incrementPatch(version: string): string {
  const parts = version.split(".").map(value => Number.parseInt(value, 10));
  if (parts.length >= 3 && parts.every(Number.isFinite)) return `${parts[0]}.${parts[1]}.${parts[2] + 1}`;
  return `${version}.1`;
}
function friendly(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, "$1 $2");
}
function formatNumber(value?: number): string {
  return new Intl.NumberFormat("en-ZA").format(value ?? 0);
}
function formatDate(value: string): string {
  return new Intl.DateTimeFormat("en-ZA", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value));
}
