import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Activity,
  AlertTriangle,
  DatabaseBackup,
  Gauge,
  KeyRound,
  RefreshCw,
  ServerCog,
  ShieldAlert,
  ShieldCheck,
  Cpu,
  Layers,
  Lock,
  Play,
  RotateCcw,
  CheckCircle2,
  XCircle,
  Clock,
} from "lucide-react";
import { ErrorState, LoadingState } from "../components/AsyncStates";
import { MetricCard } from "../components/MetricCard";
import { getApiErrorMessage } from "../services/apiClient";
import {
  getAnalyticsPipeline,
  getAuthenticationEvidence,
  getBackups,
  createBackup,
  verifyBackup,
  getBuildEvidence,
  getOperationsStatus,
  getReadiness,
  getSecretProviders,
  getTelemetryEvidence,
  updateSafeMode,
  type DependencyState,
} from "../services/operationsApi";
import "../functional-workspaces.css";
import "../operations.css";

type TabKey = "overview" | "backups" | "auth" | "telemetry" | "build";

export function OperationsPage() {
  const queryClient = useQueryClient();
  const [activeTab, setActiveTab] = useState<TabKey>("overview");
  const statusQuery = useQuery({ queryKey: ["operations", "status"], queryFn: getOperationsStatus, refetchInterval: 30_000 });

  const [reason, setReason] = useState("");
  const [confirmation, setConfirmation] = useState("");

  const readiness = useQuery({ queryKey: ["operations", "readiness"], queryFn: getReadiness, staleTime: 30_000 });
  const analytics = useQuery({ queryKey: ["operations", "analytics"], queryFn: getAnalyticsPipeline, staleTime: 30_000 });
  const authentication = useQuery({ queryKey: ["operations", "authentication"], queryFn: getAuthenticationEvidence, staleTime: 30_000 });
  const secrets = useQuery({ queryKey: ["operations", "secrets"], queryFn: getSecretProviders, staleTime: 30_000 });
  const backups = useQuery({ queryKey: ["operations", "backups"], queryFn: getBackups, staleTime: 30_000 });
  const build = useQuery({ queryKey: ["operations", "build"], queryFn: getBuildEvidence, staleTime: 30_000 });
  const telemetry = useQuery({ queryKey: ["operations", "telemetry"], queryFn: getTelemetryEvidence, staleTime: 30_000 });

  const [verificationResult, setVerificationResult] = useState<Record<string, unknown> | null>(null);

  const backupMutation = useMutation({
    mutationFn: createBackup,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["operations", "backups"] });
      await queryClient.invalidateQueries({ queryKey: ["operations", "status"] });
    },
  });

  const verifyMutation = useMutation({
    mutationFn: () => verifyBackup("current"),
    onSuccess: (data) => {
      setVerificationResult(data as Record<string, unknown>);
    },
  });

  const mutation = useMutation({
    mutationFn: updateSafeMode,
    onSuccess: async () => {
      setReason("");
      setConfirmation("");
      await queryClient.invalidateQueries({ queryKey: ["platform-status"] });
      window.dispatchEvent(new Event("convolab:platform-status"));
    },
    onSettled: async () => {
      await queryClient.invalidateQueries({ queryKey: ["operations"] });
    },
  });

  if (statusQuery.isLoading) return <LoadingState label="Loading Operations Center…" />;
  if (statusQuery.isError || !statusQuery.data) {
    return <ErrorState title="Operations Center unavailable" message={getApiErrorMessage(statusQuery.error)} onRetry={() => void statusQuery.refetch()} />;
  }

  const status = statusQuery.data;
  const safe = status.safeMode;
  const targetEnabled = !safe.persistedSafeModeEnabled;
  const exactConfirmation = targetEnabled ? "ACTIVATE SAFE MODE" : "DEACTIVATE SAFE MODE";

  return (
    <div className="operations-page">
      <header className="studio-page-header operations-header">
        <div>
          <span className="page-eyebrow">Platform Administration & Governance</span>
          <h1>Operations Center</h1>
          <p>Real-time telemetry, backup health, IAM state, and disaster recovery administration.</p>
        </div>
        <button className="secondary-button" onClick={() => void queryClient.invalidateQueries({ queryKey: ["operations"] })} disabled={statusQuery.isFetching}>
          <RefreshCw size={16} className={statusQuery.isFetching ? "spin" : ""} /> Refresh Telemetry
        </button>
      </header>

      {/* Primary KPI Ribbon */}
      <section className="metric-grid operations-metrics">
        <MetricCard icon={Gauge} label="Overall Health" value={status.status} detail={`${status.version} · ${status.environment}`} tone={status.status === "Healthy" ? "positive" : "warning"} />
        <MetricCard icon={ServerCog} label="Worker State" value={status.worker.state} detail={`Heartbeat: ${status.worker.staleAfterSeconds}s TTL`} />
        <MetricCard icon={Activity} label="OTLP Telemetry" value={status.telemetry} detail="Collector probe evidence" />
        <MetricCard icon={ShieldAlert} label="Safe Mode" value={safe.effectiveSafeModeEnabled ? "Active" : "Inactive"} detail={`Revision ${safe.revision}`} tone={safe.effectiveSafeModeEnabled ? "warning" : "positive"} />
      </section>

      {/* Segmented Navigation Tabs */}
      <nav className="operations-tabs" role="tablist">
        <button className={`tab-button ${activeTab === "overview" ? "active" : ""}`} onClick={() => setActiveTab("overview")}>
          <Gauge size={16} /> Overview & Health
        </button>
        <button className={`tab-button ${activeTab === "backups" ? "active" : ""}`} onClick={() => setActiveTab("backups")}>
          <DatabaseBackup size={16} /> Backup & DR
        </button>
        <button className={`tab-button ${activeTab === "auth" ? "active" : ""}`} onClick={() => setActiveTab("auth")}>
          <Lock size={16} /> Authentication & IAM
        </button>
        <button className={`tab-button ${activeTab === "telemetry" ? "active" : ""}`} onClick={() => setActiveTab("telemetry")}>
          <Layers size={16} /> Telemetry & Secrets
        </button>
        <button className={`tab-button ${activeTab === "build" ? "active" : ""}`} onClick={() => setActiveTab("build")}>
          <Cpu size={16} /> Build & Manifest
        </button>
      </nav>

      {/* Tab Content: Overview & Health */}
      {activeTab === "overview" && (
        <div className="tab-pane">
          <section className={`panel safe-mode-panel ${safe.effectiveSafeModeEnabled ? "active" : ""}`}>
            <div className="panel-header">
              <div>
                <span className="panel-eyebrow">Emergency Control Gate</span>
                <h3>Safe Mode Override</h3>
              </div>
              <AlertTriangle size={18} />
            </div>
            <p>{safe.reason || "Safe mode is inactive. Normal platform execution is enabled."}</p>
            <dl className="operations-facts">
              <div><dt>Effective State</dt><dd>{safe.effectiveSafeModeEnabled ? "Active" : "Inactive"}</dd></div>
              <div><dt>Deterministic Verification</dt><dd>{safe.allowDeterministicVerification ? "Permitted" : "Blocked"}</dd></div>
              <div><dt>Analytics Exports</dt><dd>{safe.blockAnalyticsExports === true ? "Blocked in safe mode" : safe.blockAnalyticsExports === false ? "Remain available" : "Decision unavailable"}</dd></div>
            </dl>
            <div className="safe-mode-form">
              <label>Reason<input value={reason} onChange={event => setReason(event.target.value)} placeholder="Operational reason (min 8 chars)" /></label>
              <label>Type {exactConfirmation}<input value={confirmation} onChange={event => setConfirmation(event.target.value)} /></label>
              <button
                className={targetEnabled ? "danger-button" : "primary-button"}
                disabled={mutation.isPending || reason.trim().length < 8 || confirmation !== exactConfirmation || (!targetEnabled && safe.environmentOverrideEnabled)}
                onClick={() => mutation.mutate({ enabled: targetEnabled, expectedRevision: safe.revision, reason, confirmation })}
              >
                {targetEnabled ? "Activate Safe Mode" : "Deactivate Safe Mode"}
              </button>
            </div>
            {mutation.error && <div className="provider-warning">{getApiErrorMessage(mutation.error)}</div>}
          </section>

          <div className="operations-grid-two">
            <article className="panel">
              <div className="panel-header">
                <div>
                  <span className="panel-eyebrow">System Subsystems</span>
                  <h3>Readiness Evidence</h3>
                </div>
                <ShieldCheck size={18} />
              </div>
              {readiness.isLoading && <LoadingState label="Loading readiness…" />}
              {readiness.data && <Evidence value={readiness.data} />}
            </article>

            <article className="panel">
              <div className="panel-header">
                <div>
                  <span className="panel-eyebrow">Outbox & Workers</span>
                  <h3>Analytics Maintenance Pipeline</h3>
                </div>
                <Activity size={18} />
              </div>
              {analytics.isLoading && <LoadingState label="Loading pipeline…" />}
              {analytics.data && <Evidence value={analytics.data} />}
            </article>
          </div>
        </div>
      )}

      {/* Tab Content: Backup & DR */}
      {activeTab === "backups" && (
        <div className="tab-pane">
          {/* Action Toolbar */}
          <div className="backup-action-bar panel">
            <div className="backup-action-info">
              <span className="panel-eyebrow">Interactive Operations</span>
              <h3>Backup & Recovery Orchestration</h3>
              <p>Trigger on-demand cryptographic snapshots or run deep recovery verification.</p>
            </div>
            <div className="backup-action-buttons">
              <button
                className="primary-button"
                onClick={() => backupMutation.mutate()}
                disabled={backupMutation.isPending}
              >
                <Play size={16} className={backupMutation.isPending ? "spin" : ""} />
                {backupMutation.isPending ? "Creating Snapshot…" : "Create Snapshot Now"}
              </button>
              <button
                className="secondary-button"
                onClick={() => verifyMutation.mutate()}
                disabled={verifyMutation.isPending}
              >
                <RotateCcw size={16} className={verifyMutation.isPending ? "spin" : ""} />
                {verifyMutation.isPending ? "Verifying…" : "Run Deep Verification Drill"}
              </button>
            </div>
          </div>

          {backupMutation.error && (
            <div className="provider-warning">
              <AlertTriangle size={16} />
              <span>{getApiErrorMessage(backupMutation.error)}</span>
            </div>
          )}

          {verifyMutation.error && (
            <div className="provider-warning">
              <AlertTriangle size={16} />
              <span>{getApiErrorMessage(verifyMutation.error)}</span>
            </div>
          )}

          {/* Verification Results Modal / Card */}
          {verificationResult && (
            <section className="panel verification-result-panel">
              <div className="panel-header">
                <div>
                  <span className="panel-eyebrow">Automated Drill Evaluation</span>
                  <h3>Deep Recovery Verification Results</h3>
                </div>
                {verificationResult.isHealthy ? (
                  <span className="status-pill status-healthy"><CheckCircle2 size={13} /> Reconciled & Healthy</span>
                ) : (
                  <span className="status-pill status-danger"><XCircle size={13} /> Inconsistencies Found</span>
                )}
              </div>
              <div className="verification-details">
                <dl className="operations-facts">
                  <div>
                    <dt>Database State</dt>
                    <dd>{(verificationResult.database as Record<string, unknown>)?.canConnect ? "Connected & Verified" : "Failed"}</dd>
                  </div>
                  <div>
                    <dt>Document Reconciliation</dt>
                    <dd>{(verificationResult.documents as Record<string, unknown>)?.reconciled ? "0 Missing / 0 Orphans" : "Mismatch Detected"}</dd>
                  </div>
                  <div>
                    <dt>Data Protection Key Ring</dt>
                    <dd>{(verificationResult.dataProtection as Record<string, unknown>)?.protectUnprotectVerified ? "Verified (Roundtrip OK)" : "Failed"}</dd>
                  </div>
                </dl>
                {Array.isArray(verificationResult.inconsistencies) && (verificationResult.inconsistencies as string[]).length > 0 && (
                  <div className="inconsistency-list">
                    <strong>Reported Discrepancies:</strong>
                    <ul>
                      {(verificationResult.inconsistencies as string[]).map((inc, i) => (
                        <li key={i}>{inc}</li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>
            </section>
          )}

          {/* Policy & Cadence Summary */}
          <div className="operations-grid-two">
            <section className="panel">
              <div className="panel-header">
                <div>
                  <span className="panel-eyebrow">PostgreSQL, Documents & Keys</span>
                  <h3>Live Backup Evidence</h3>
                </div>
                <DatabaseBackup size={18} />
              </div>
              {backups.isLoading && <LoadingState label="Loading backup telemetry…" />}
              {backups.data && <Evidence value={backups.data} />}
            </section>

            <section className="panel">
              <div className="panel-header">
                <div>
                  <span className="panel-eyebrow">Configured SLAs & Retention</span>
                  <h3>Disaster Recovery Policies</h3>
                </div>
                <Clock size={18} />
              </div>
              <dl className="operations-facts">
                <div><dt>Target RPO</dt><dd>24 hours (1440 min)</dd></div>
                <div><dt>Target RTO</dt><dd>&lt; 4 hours</dd></div>
                <div><dt>Daily Retention</dt><dd>14 days</dd></div>
                <div><dt>Weekly Retention</dt><dd>8 weeks</dd></div>
                <div><dt>Monthly Retention</dt><dd>6 months</dd></div>
                <div><dt>Encryption</dt><dd>AES-256-GCM</dd></div>
              </dl>
            </section>
          </div>
        </div>
      )}

      {/* Tab Content: Authentication & IAM */}
      {activeTab === "auth" && (
        <div className="tab-pane">
          <section className="panel">
            <div className="panel-header">
              <div>
                <span className="panel-eyebrow">Identity & Session Telemetry</span>
                <h3>Authentication & Break-Glass Evidence</h3>
              </div>
              <Lock size={18} />
            </div>
            {authentication.isLoading && <LoadingState label="Loading auth evidence…" />}
            {authentication.data && <Evidence value={authentication.data} />}
          </section>
        </div>
      )}

      {/* Tab Content: Telemetry & Secrets */}
      {activeTab === "telemetry" && (
        <div className="tab-pane operations-grid-two">
          <section className="panel">
            <div className="panel-header">
              <div>
                <span className="panel-eyebrow">Secret Store Providers</span>
                <h3>Configuration Secrets</h3>
              </div>
              <KeyRound size={18} />
            </div>
            {secrets.isLoading && <LoadingState label="Loading secret providers…" />}
            {secrets.data && <Evidence value={secrets.data} />}
          </section>

          <section className="panel">
            <div className="panel-header">
              <div>
                <span className="panel-eyebrow">OpenTelemetry Exporter</span>
                <h3>Telemetry Collectors</h3>
              </div>
              <Activity size={18} />
            </div>
            {telemetry.isLoading && <LoadingState label="Loading telemetry evidence…" />}
            {telemetry.data && <Evidence value={telemetry.data} />}
          </section>
        </div>
      )}

      {/* Tab Content: Build & Manifest */}
      {activeTab === "build" && (
        <div className="tab-pane">
          <section className="panel">
            <div className="panel-header">
              <div>
                <span className="panel-eyebrow">Provenance & Deployment Metadata</span>
                <h3>Build & System Information</h3>
              </div>
              <Cpu size={18} />
            </div>
            {build.isLoading && <LoadingState label="Loading build evidence…" />}
            {build.data && <Evidence value={build.data} />}
          </section>
        </div>
      )}
    </div>
  );
}

function Evidence({ value }: { value: unknown }) {
  if (typeof value !== "object" || value === null) return <p>No evidence returned.</p>;
  const record = value as Record<string, unknown>;

  if (Array.isArray(record.components)) {
    return (
      <div className="dependency-list">
        {(record.components as Array<Record<string, unknown>>).map(component => (
          <div key={String(component.component)}>
            <strong>{String(component.component)}</strong>
            <State value={String(component.state) as DependencyState} />
            <span>{Number(component.durationMs).toFixed(0)} ms</span>
          </div>
        ))}
      </div>
    );
  }

  if ("pendingCount" in record) {
    return (
      <dl className="operations-facts">
        <Fact label="Pipeline Status" value={String(record.status)} />
        <Fact label="Pending Outbox Records" value={String(record.pendingCount)} />
        <Fact label="Failed Outbox Records" value={String(record.failedCount)} />
        <Fact label="Oldest Pending Age" value={Seconds(record.oldestPendingAgeSeconds)} />
        <Fact label="Oldest Failed Age" value={Seconds(record.oldestFailedAgeSeconds)} />
        <Fact label="Aggregation Lag" value={Seconds(record.maximumAggregationLagSeconds)} />
      </dl>
    );
  }

  if ("otlpDependencyState" in record) {
    return (
      <dl className="operations-facts">
        <div><dt>OTLP Exporter State</dt><dd><State value={String(record.otlpDependencyState) as DependencyState} /></dd></div>
        <Fact label="Endpoint Configured" value={String(record.endpointConfigured)} />
        <Fact label="Trace Export Enabled" value={String(record.traceExportEnabled)} />
        <Fact label="Metric Export Enabled" value={String(record.metricExportEnabled)} />
        <Fact label="Service Identifier" value={String(record.serviceName)} />
        <Fact label="Last Live Probe" value={Timestamp(record.lastLiveValidatedAt)} />
      </dl>
    );
  }

  if ("breakGlassState" in record) {
    return (
      <dl className="operations-facts">
        <Fact label="Authentication Mode" value={String(record.mode)} />
        <Fact label="Local Login Enabled" value={String(record.localLoginEnabled)} />
        <Fact label="Entra SSO Enabled" value={String(record.entraEnabled)} />
        <Fact label="Active Application Sessions" value={String(record.activeSessions)} />
        <Fact label="Linked Active Users" value={String(record.linkedActiveUsers)} />
        <Fact label="External Identities" value={String(record.externalIdentityCount)} />
        <Fact label="Break-Glass Status" value={String(record.breakGlassState)} />
        <Fact label="Break-Glass Uses (24h)" value={String(record.breakGlassUsesLast24Hours)} />
        <Fact label="Last Break-Glass Success" value={Timestamp(record.lastBreakGlassSuccessfulUseAt)} />
      </dl>
    );
  }

  if (Array.isArray(record.providers)) {
    return (
      <div className="dependency-list">
        {(record.providers as Array<Record<string, unknown>>).map(provider => (
          <div key={String(provider.provider)}>
            <strong>{String(provider.provider)}</strong>
            <State value={String(provider.state) as DependencyState} />
            <span>{String(provider.lastErrorCode ?? "Healthy")}</span>
          </div>
        ))}
      </div>
    );
  }

  if ("configuredRpo" in record && String(record.state) !== "NotConfigured") {
    return (
      <div>
        <dl className="operations-facts">
          <div><dt>Backup Health</dt><dd><State value={String(record.state) as DependencyState} /></dd></div>
          <Fact label="Status Message" value={String(record.message)} />
          <Fact label="Last Snapshot Completed" value={Timestamp(record.lastBackupCompletedAt)} />
          <Fact label="Configured RPO Policy" value={record.configuredRpo ? String(TimeSpanToMinutes(String(record.configuredRpo))) + " minutes" : "Not configured"} />
          <Fact label="Last Snapshot Size" value={record.lastBackupSizeBytes ? FormatBytes(Number(record.lastBackupSizeBytes)) : "Unknown"} />
        </dl>
      </div>
    );
  }

  if (String(record.state) === "NotConfigured") {
    return (
      <div className="not-configured">
        <DatabaseBackup size={20} />
        <div>
          <strong>Not Configured</strong>
          <p>{String(record.message || "No backup storage path is configured.")}</p>
        </div>
      </div>
    );
  }

  return <pre className="operations-json">{JSON.stringify(value, null, 2)}</pre>;
}

function TimeSpanToMinutes(timeSpan: string): number {
  const [hours, minutes] = timeSpan.split(":").map(Number);
  return (hours || 0) * 60 + (minutes || 0);
}

function FormatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
}

function State({ value }: { value: DependencyState }) {
  return <span className={`dependency-state state-${value.toLowerCase()}`}>{value}</span>;
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}

function Seconds(value: unknown) {
  return value === null || value === undefined ? "None" : `${Number(value).toFixed(0)}s`;
}

function Timestamp(value: unknown) {
  return value ? new Date(String(value)).toLocaleString() : "None recorded";
}
