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
] as const;

export function OperationsPage() {
  const queryClient = useQueryClient();
  const statusQuery = useQuery({ queryKey: ["operations-status"], queryFn: getOperationsStatus, refetchInterval: 30_000 });
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
  const detailQueries = { readiness, workers, analytics, authentication, secrets, backups, build };
  const mutation = useMutation({
    mutationFn: updateSafeMode,
    onSuccess: async () => {
      setReason(""); setConfirmation("");
      await queryClient.invalidateQueries({ queryKey: ["operations-status"] });
      window.dispatchEvent(new Event("convolab:platform-status"));
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
      <div><span className="page-eyebrow">alpha.15 operational foundation · in progress</span><h1>Operations Center</h1><p>Sanitized readiness, worker, Analytics, secret-provider, telemetry, and safe-mode evidence for platform administrators.</p></div>
      <button className="secondary-button" onClick={() => void statusQuery.refetch()} disabled={statusQuery.isFetching}><RefreshCw size={16} className={statusQuery.isFetching ? "spin" : ""} /> Refresh summary</button>
    </header>

    <section className="metric-grid operations-metrics">
      <MetricCard icon={Gauge} label="Overall" value={status.status} detail={`${status.version} · ${status.environment}`} tone={status.status === "Healthy" ? "positive" : "warning"} />
      <MetricCard icon={ServerCog} label="Worker" value={status.worker.state} detail={`Stale after ${status.worker.staleAfterSeconds}s`} />
      <MetricCard icon={Activity} label="OTLP" value={status.telemetry} detail="Collector availability never blocks startup" />
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
  if (String(record.state) === "NotConfigured") return <div className="not-configured"><DatabaseBackup size={18} /><strong>NotConfigured</strong><p>{String(record.message || "No backend is configured.")}</p></div>;
  return <pre className="operations-json">{JSON.stringify(value, null, 2)}</pre>;
}

function State({ value }: { value: DependencyState }) { return <span className={`dependency-state state-${value.toLowerCase()}`}>{value}</span>; }
