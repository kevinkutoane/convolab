import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Activity, AlertTriangle, DatabaseBackup, Gauge, KeyRound, RefreshCw, ServerCog, ShieldAlert } from "lucide-react";
import { ErrorState, LoadingState } from "../components/AsyncStates";
import { MetricCard } from "../components/MetricCard";
import { getApiErrorMessage } from "../services/apiClient";
import {
  getAnalyticsPipeline,
  getAuthenticationEvidence,
  getBackups,
  getBuildEvidence,
  getOperationsStatus,
  getReadiness,
  getSecretProviders,
  getTelemetryEvidence,
  getWorkers,
  updateSafeMode,
  type DependencyState,
} from "../services/operationsApi";
import "../functional-workspaces.css";
import "../operations.css";

const details = [
  ["readiness", "Readiness evidence", getReadiness],
  ["workers", "Workers and leases", getWorkers],
  ["analytics", "Analytics pipeline", getAnalyticsPipeline],
  ["authentication", "Authentication", getAuthenticationEvidence],
  ["secrets", "Secret providers", getSecretProviders],
  ["backups", "Backups", getBackups],
  ["build", "Build and deployment", getBuildEvidence],
  ["telemetry", "Telemetry", getTelemetryEvidence],
] as const;

export function OperationsPage() {
  const queryClient = useQueryClient();
  const statusQuery = useQuery({ queryKey: ["operations", "status"], queryFn: getOperationsStatus, refetchInterval: 30_000 });
  const [opened, setOpened] = useState<string>();
  const [reason, setReason] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const readiness = useQuery({ queryKey: ["operations", "readiness"], queryFn: getReadiness, enabled: opened === "readiness", staleTime: 0 });
  const workers = useQuery({ queryKey: ["operations", "workers"], queryFn: getWorkers, enabled: opened === "workers", staleTime: 0 });
  const analytics = useQuery({ queryKey: ["operations", "analytics"], queryFn: getAnalyticsPipeline, enabled: opened === "analytics", staleTime: 0 });
  const authentication = useQuery({ queryKey: ["operations", "authentication"], queryFn: getAuthenticationEvidence, enabled: opened === "authentication", staleTime: 0 });
  const secrets = useQuery({ queryKey: ["operations", "secrets"], queryFn: getSecretProviders, enabled: opened === "secrets", staleTime: 0 });
  const backups = useQuery({ queryKey: ["operations", "backups"], queryFn: getBackups, enabled: opened === "backups", staleTime: 0 });
  const build = useQuery({ queryKey: ["operations", "build"], queryFn: getBuildEvidence, enabled: opened === "build", staleTime: 0 });
  const telemetry = useQuery({ queryKey: ["operations", "telemetry"], queryFn: getTelemetryEvidence, enabled: opened === "telemetry", staleTime: 0 });
  const detailQueries = { readiness, workers, analytics, authentication, secrets, backups, build, telemetry };
  const mutation = useMutation({
    mutationFn: updateSafeMode,
    onSuccess: async () => {
      setReason(""); setConfirmation("");
      await queryClient.invalidateQueries({ queryKey: ["platform-status"] });
      window.dispatchEvent(new Event("convolab:platform-status"));
    },
    onSettled: async () => {
      await queryClient.invalidateQueries({ queryKey: ["operations"] });
    },
  });

  if (statusQuery.isLoading) return <LoadingState label="Loading Operations Center…" />;
  if (statusQuery.isError || !statusQuery.data) return <ErrorState title="Operations Center unavailable" message={getApiErrorMessage(statusQuery.error)} onRetry={() => void statusQuery.refetch()} />;
  const status = statusQuery.data;
  const safe = status.safeMode;
  const targetEnabled = !safe.persistedSafeModeEnabled;
  const exactConfirmation = targetEnabled ? "ACTIVATE SAFE MODE" : "DEACTIVATE SAFE MODE";

  return <div className="operations-page">
    <header className="studio-page-header operations-header">
      <div><span className="page-eyebrow">alpha.15 — Microsoft Entra ID, External Identities & Hybrid Authentication</span><h1>Operations Center</h1><p>Sanitized readiness, authentication, Analytics, secret-provider, telemetry, and safe-mode evidence for platform administrators.</p></div>
      <button className="secondary-button" onClick={() => void statusQuery.refetch()} disabled={statusQuery.isFetching}><RefreshCw size={16} className={statusQuery.isFetching ? "spin" : ""} /> Refresh summary</button>
    </header>

    <section className="metric-grid operations-metrics">
      <MetricCard icon={Gauge} label="Overall" value={status.status} detail={`${status.version} · ${status.environment} · readiness ${status.readiness.status}`} tone={status.status === "Healthy" ? "positive" : "warning"} />
      <MetricCard icon={ServerCog} label="Worker" value={status.worker.state} detail={`Stale after ${status.worker.staleAfterSeconds}s`} />
      <MetricCard icon={Activity} label="OTLP" value={status.telemetry} detail="Reachability evidence never blocks startup" />
      <MetricCard icon={ShieldAlert} label="Safe mode" value={safe.effectiveSafeModeEnabled ? "Active" : "Inactive"} detail={safe.environmentOverrideEnabled ? "Environment override active" : `Revision ${safe.revision}`} tone={safe.effectiveSafeModeEnabled ? "warning" : "positive"} />
    </section>

    <section className={`panel safe-mode-panel${safe.effectiveSafeModeEnabled ? " active" : ""}`}>
      <div className="panel-header"><div><span className="panel-eyebrow">Emergency control</span><h3>Safe mode</h3></div><AlertTriangle size={18} /></div>
      <p>{safe.reason || "No persisted safe-mode reason."}</p>
      <dl className="operations-facts">
        <div><dt>Effective state</dt><dd>{safe.effectiveSafeModeEnabled ? "Active" : "Inactive"}</dd></div>
        <div><dt>Deterministic verification</dt><dd>{safe.allowDeterministicVerification ? "Permitted" : "Blocked"}</dd></div>
        <div><dt>Analytics exports</dt><dd>{safe.blockAnalyticsExports === true ? "Blocked in safe mode" : safe.blockAnalyticsExports === false ? "Remain available" : "Decision unavailable"}</dd></div>
      </dl>
      <div className="safe-mode-form">
        <label>Reason<input value={reason} onChange={event => setReason(event.target.value)} placeholder="Operational reason (minimum eight characters)" /></label>
        <label>Type {exactConfirmation}<input value={confirmation} onChange={event => setConfirmation(event.target.value)} /></label>
        <button className={targetEnabled ? "danger-button" : "primary-button"} disabled={mutation.isPending || reason.trim().length < 8 || confirmation !== exactConfirmation || (!targetEnabled && safe.environmentOverrideEnabled)} onClick={() => mutation.mutate({ enabled: targetEnabled, expectedRevision: safe.revision, reason, confirmation })}>{targetEnabled ? "Activate safe mode" : "Deactivate safe mode"}</button>
      </div>
      {mutation.error && <div className="provider-warning">{getApiErrorMessage(mutation.error)}</div>}
    </section>

    <section className="operations-detail-grid">
      {details.map(([key, label]) => {
        const query = detailQueries[key];
        const Icon = key === "backups" ? DatabaseBackup : key === "secrets" ? KeyRound : Activity;
        return <article className="panel operations-detail" key={key}>
          <div className="panel-header"><div><span className="panel-eyebrow">On-demand evidence</span><h3>{label}</h3></div><Icon size={17} /></div>
          {opened !== key && <button className="secondary-button" onClick={() => setOpened(key)}>Load evidence</button>}
          {opened === key && query.isLoading && <LoadingState label={`Loading ${label.toLowerCase()}…`} />}
          {opened === key && query.error && <ErrorState message={getApiErrorMessage(query.error)} onRetry={() => void query.refetch()} />}
          {opened === key && query.data && <Evidence value={query.data} />}
        </article>;
      })}
    </section>
  </div>;
}

function Evidence({ value }: { value: unknown }) {
  if (typeof value !== "object" || value === null) return <p>No evidence returned.</p>;
  const record = value as Record<string, unknown>;
  if (Array.isArray(record.components)) return <div className="dependency-list">{(record.components as Array<Record<string, unknown>>).map(component => <div key={String(component.component)}><strong>{String(component.component)}</strong><State value={String(component.state) as DependencyState} /><span>{Number(component.durationMs).toFixed(0)} ms</span></div>)}</div>;
  if ("pendingCount" in record) return <dl className="operations-facts">
    <Fact label="Current status" value={String(record.status)} />
    <Fact label="Pending records" value={String(record.pendingCount)} />
    <Fact label="Failed records" value={String(record.failedCount)} />
    <Fact label="Oldest pending age" value={Seconds(record.oldestPendingAgeSeconds)} />
    <Fact label="Oldest failed age" value={Seconds(record.oldestFailedAgeSeconds)} />
    <Fact label="Aggregation dirty checkpoints" value={String(record.aggregationDirtyCheckpointCount)} />
    <Fact label="Aggregation failed checkpoints" value={String(record.aggregationFailedCheckpointCount)} />
    <Fact label="Aggregation lag" value={Seconds(record.maximumAggregationLagSeconds)} />
    <Fact label="Last successful dispatch" value={Timestamp(record.lastSuccessfulOutboxDispatchAt)} />
    <Fact label="Last successful aggregation" value={Timestamp(record.lastSuccessfulAggregationAt)} />
    <Fact label="Applied thresholds" value={JSON.stringify(record.thresholds)} />
  </dl>;
  if ("otlpDependencyState" in record) return <dl className="operations-facts">
    <div><dt>OTLP dependency state</dt><dd><State value={String(record.otlpDependencyState) as DependencyState} /></dd></div>
    <Fact label="Endpoint configured" value={String(record.endpointConfigured)} />
    <Fact label="Trace export enabled" value={String(record.traceExportEnabled)} />
    <Fact label="Metric export enabled" value={String(record.metricExportEnabled)} />
    <Fact label="Service name" value={String(record.serviceName)} />
    <Fact label="Release version" value={String(record.releaseVersion)} />
    <Fact label="Last live validation" value={Timestamp(record.lastLiveValidatedAt)} />
    <Fact label="Last failure code" value={String(record.lastFailureCode ?? "None")} />
  </dl>;
  if ("breakGlassState" in record) return <dl className="operations-facts">
    <Fact label="Break-glass enabled" value={String(record.breakGlassEnabled)} />
    <Fact label="Break-glass available" value={String(record.breakGlassAvailable)} />
    <Fact label="Break-glass state" value={String(record.breakGlassState)} />
    <Fact label="Break-glass failures (24h)" value={String(record.breakGlassFailuresLast24Hours)} />
    <Fact label="Last break-glass success" value={Timestamp(record.lastBreakGlassSuccessfulUseAt)} />
  </dl>;
  if (Array.isArray(record.providers)) return <div className="dependency-list">
    {(record.providers as Array<Record<string, unknown>>).map(provider => <div key={String(provider.provider)} data-dependency={String(provider.provider)}><strong>{String(provider.provider)}</strong><State value={String(provider.state) as DependencyState} /><span>{String(provider.lastErrorCode ?? "No failure")}</span></div>)}
    {(record.requiredEnvironments as Array<Record<string, unknown>> | undefined)?.map(environment => <div key={String(environment.environmentId)}><strong>{String(environment.environmentName)} · {String(environment.provider)}</strong><State value={String(environment.dependencyState) as DependencyState} /><span>{environment.required ? "Required" : "Not required"} · {String(environment.secretProviderScheme ?? "No secret provider")}</span></div>)}
  </div>;
  if (String(record.state) === "NotConfigured") return <div className="not-configured"><DatabaseBackup size={18} /><strong>NotConfigured</strong><p>{String(record.message || "No backend is configured.")}</p></div>;
  return <pre className="operations-json">{JSON.stringify(value, null, 2)}</pre>;
}

function State({ value }: { value: DependencyState }) { return <span className={`dependency-state state-${value.toLowerCase()}`}>{value}</span>; }
function Fact({ label, value }: { label: string; value: string }) { return <div><dt>{label}</dt><dd>{value}</dd></div>; }
function Seconds(value: unknown) { return value === null || value === undefined ? "None" : `${Number(value).toFixed(0)} seconds`; }
function Timestamp(value: unknown) { return value ? new Date(String(value)).toLocaleString() : "No successful run recorded"; }
