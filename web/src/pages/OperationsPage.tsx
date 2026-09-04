import { useHelp } from "../contexts/HelpContext";
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
  AlertOctagon,
  Rocket,
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
  listAvailableBackups,
  restoreBackup,
  getRecoveryStatus,
  getDeployments,
  approveDeployment,
  getBuildEvidence,
  getOperationsStatus,
  getReadiness,
  getSecretProviders,
  getTelemetryEvidence,
  updateSafeMode,
  type DependencyState,
} from "../services/operationsApi";

type TabKey = "overview" | "deployments" | "backups" | "auth" | "telemetry" | "build";

export function OperationsPage() {
  useHelp({
    title: "Platform Operations",
    description: "Administrative controls for infrastructure, backups, and platform-wide maintenance.",
    usageSteps: [
        "View active background jobs and worker queues.",
        "Trigger manual backups of the PostgreSQL database.",
        "Review system logs and memory usage."
    ],
    examples: [
        "Flushing the Redis cache after a major configuration change.",
        "Exporting an encrypted backup of all workspace data."
    ],
    expectedOutput: "Successful execution of infrastructure maintenance tasks and system stability.",
    aiLayerRole: "The AI monitors system logs to proactively warn administrators of potential bottlenecks or failing background jobs."
  });

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
  const deployments = useQuery({ queryKey: ["operations", "deployments"], queryFn: () => getDeployments(), enabled: activeTab === "deployments", refetchInterval: 15_000 });
  const build = useQuery({ queryKey: ["operations", "build"], queryFn: getBuildEvidence, staleTime: 30_000 });
  const telemetry = useQuery({ queryKey: ["operations", "telemetry"], queryFn: getTelemetryEvidence, staleTime: 30_000 });
  const backupListQuery = useQuery({ queryKey: ["operations", "backups-list"], queryFn: listAvailableBackups, enabled: activeTab === "backups" });

  const [verificationResult, setVerificationResult] = useState<Record<string, unknown> | null>(null);
  const [snapshotResult, setSnapshotResult] = useState<Record<string, unknown> | null>(null);

  // Restore State
  const [showRestoreModal, setShowRestoreModal] = useState(false);
  const [selectedRestoreBackupId, setSelectedRestoreBackupId] = useState("");
  const [restoreConfirmText, setRestoreConfirmText] = useState("");
  const [activeRestoreOpId, setActiveRestoreOpId] = useState<string | null>(null);

  const restoreStatusQuery = useQuery({
    queryKey: ["operations", "recovery", activeRestoreOpId],
    queryFn: () => getRecoveryStatus(activeRestoreOpId!),
    enabled: Boolean(activeRestoreOpId),
    refetchInterval: (query) => {
      const data = query.state.data as Record<string, unknown> | undefined;
      if (data?.state === "Completed" || data?.state === "Failed") return false;
      return 2000;
    },
  });

  const restoreMutation = useMutation({
    mutationFn: ({ backupId }: { backupId: string }) => restoreBackup(backupId, true),
    onSuccess: (data) => {
      setActiveRestoreOpId((data as { operationId: string }).operationId);
    },
  });

  const backupMutation = useMutation({
    mutationFn: createBackup,
    onSuccess: async (data) => {
      setSnapshotResult(data as Record<string, unknown>);
      await queryClient.invalidateQueries({ queryKey: ["operations", "backups"] });
      await queryClient.invalidateQueries({ queryKey: ["operations", "backups-list"] });
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
        <button className={`tab-button ${activeTab === "deployments" ? "active" : ""}`} onClick={() => setActiveTab("deployments")}>
          <Rocket size={16} /> Deployments & Releases
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

      {/* Tab Content: Deployments & Releases */}
      {activeTab === "deployments" && (
        <div className="tab-pane">
          {/* Environment Promotion Pipeline */}
          <section className="panel">
            <div className="panel-header">
              <div>
                <span className="panel-eyebrow">Immutable Artifact Promotion</span>
                <h3>Active Environment State & Promotion Pipeline</h3>
              </div>
              <Rocket size={18} />
            </div>

            {deployments.isLoading && <LoadingState label="Loading deployment topology…" />}

            {deployments.data && (
              <div className="pipeline-grid" style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: "1rem", padding: "1.25rem" }}>
                {(deployments.data as { environments: Array<{
                  environment: string;
                  activeReleaseVersion: string;
                  activeApiDigest?: string;
                  activeStudioDigest?: string;
                  currentStatus: string;
                  lastDeployedAt?: string;
                  activeReleaseManifestId?: string;
                }> }).environments?.map((env) => (
                  <div key={env.environment} className="environment-card panel" style={{ padding: "1rem" }}>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "0.5rem" }}>
                      <strong>{env.environment}</strong>
                      <span className={`status-pill ${env.currentStatus === "Healthy" ? "status-healthy" : "status-warning"}`}>
                        {env.currentStatus}
                      </span>
                    </div>
                    <dl className="operations-facts" style={{ gap: "0.4rem" }}>
                      <div><dt>Release Version</dt><dd>{env.activeReleaseVersion}</dd></div>
                      <div><dt>API Digest</dt><dd style={{ fontFamily: "monospace", fontSize: "11px" }}>{env.activeApiDigest ? env.activeApiDigest.slice(0, 16) + "…" : "Local build"}</dd></div>
                      <div><dt>Studio Digest</dt><dd style={{ fontFamily: "monospace", fontSize: "11px" }}>{env.activeStudioDigest ? env.activeStudioDigest.slice(0, 16) + "…" : "Local build"}</dd></div>
                      <div><dt>Last Promotion</dt><dd>{env.lastDeployedAt ? new Date(env.lastDeployedAt).toLocaleString() : "Initial"}</dd></div>
                    </dl>
                  </div>
                ))}
              </div>
            )}
          </section>

          {/* Deployment History Table */}
          {deployments.data && (
            <section className="panel">
              <div className="panel-header">
                <div>
                  <span className="panel-eyebrow">Audited Promotion Lifecycle</span>
                  <h3>Deployment History & Evidence Records</h3>
                </div>
                <Clock size={18} />
              </div>
              <div className="execution-table-wrap">
                <table className="execution-table">
                  <thead>
                    <tr>
                      <th>Environment</th>
                      <th>Version / Manifest</th>
                      <th>Commit SHA</th>
                      <th>Status</th>
                      <th>Pre-Migration Backup</th>
                      <th>Approved By</th>
                      <th>Timestamp</th>
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(deployments.data as { history: Array<{
                      id: string;
                      environment: string;
                      releaseVersion: string;
                      releaseManifestId: string;
                      sourceCommitSha: string;
                      status: string;
                      backupIdBeforeMigration?: string;
                      approvedBy?: string;
                      createdAt: string;
                    }> }).history?.map((d) => (
                      <tr key={d.id}>
                        <td><strong>{d.environment}</strong></td>
                        <td>
                          <span>{d.releaseVersion}</span>
                          <small style={{ color: "var(--text-muted)", display: "block" }}>{d.releaseManifestId}</small>
                        </td>
                        <td><code style={{ fontSize: "11px" }}>{d.sourceCommitSha.slice(0, 8)}</code></td>
                        <td>
                          <span className={`status-pill ${d.status === "Healthy" ? "status-healthy" : d.status === "Pending" ? "status-warning" : "status-danger"}`}>
                            {d.status}
                          </span>
                        </td>
                        <td><small>{d.backupIdBeforeMigration ? d.backupIdBeforeMigration.slice(0, 16) + "…" : "N/A"}</small></td>
                        <td>{d.approvedBy ?? "Auto/CI"}</td>
                        <td>{new Date(d.createdAt).toLocaleString()}</td>
                        <td>
                          {d.status === "Pending" && (
                            <button
                              className="primary-button"
                              style={{ minHeight: "28px", padding: "0 10px", fontSize: "11px" }}
                              onClick={() => approveDeployment(d.id, "Platform Administrator UI Approval")}
                            >
                              Approve Promotion
                            </button>
                          )}
                        </td>
                      </tr>
                    ))}
                    {(!(deployments.data as { history: unknown[] }).history?.length) && (
                      <tr>
                        <td colSpan={8} style={{ textAlign: "center", color: "var(--text-muted)", padding: "1.5rem" }}>
                          No deployment promotion records registered yet.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </section>
          )}
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
                onClick={() => {
                  if (Array.isArray(backupListQuery.data) && backupListQuery.data.length > 0) {
                    setSelectedRestoreBackupId((backupListQuery.data[0] as { backupId: string }).backupId);
                  }
                  setShowRestoreModal(true);
                }}
              >
                <RotateCcw size={16} />
                Restore Snapshot…
              </button>
              <button
                className="secondary-button"
                onClick={() => verifyMutation.mutate()}
                disabled={verifyMutation.isPending}
              >
                <ShieldCheck size={16} className={verifyMutation.isPending ? "spin" : ""} />
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

          {/* Snapshot Created Success Banner / Card */}
          {snapshotResult && (
            <section className="panel verification-result-panel">
              <div className="panel-header">
                <div>
                  <span className="panel-eyebrow">Cryptographic Snapshot Created</span>
                  <h3>Backup Artifact Manifest</h3>
                </div>
                <span className="status-pill status-healthy"><CheckCircle2 size={13} /> Encrypted & Persisted</span>
              </div>
              <div className="verification-details">
                <dl className="operations-facts">
                  <div>
                    <dt>Backup ID</dt>
                    <dd>{String(snapshotResult.backupId ?? "")}</dd>
                  </div>
                  <div>
                    <dt>Created Timestamp</dt>
                    <dd>{new Date(String(snapshotResult.createdAt ?? "")).toLocaleString()}</dd>
                  </div>
                  <div>
                    <dt>Database Dump Size</dt>
                    <dd>{FormatBytes(Number((snapshotResult.database as Record<string, unknown>)?.sizeBytes ?? 0))}</dd>
                  </div>
                  <div>
                    <dt>Database SHA-256</dt>
                    <dd style={{ fontFamily: "monospace", fontSize: "11px" }}>{String((snapshotResult.database as Record<string, unknown>)?.sha256 ?? "N/A").slice(0, 16)}…</dd>
                  </div>
                </dl>
              </div>
            </section>
          )}

          {/* Available Snapshots Table */}
          {Array.isArray(backupListQuery.data) && backupListQuery.data.length > 0 && (
            <section className="panel">
              <div className="panel-header">
                <div>
                  <span className="panel-eyebrow">On-Disk Encrypted Archives</span>
                  <h3>Discovered Backup Snapshots</h3>
                </div>
                <DatabaseBackup size={18} />
              </div>
              <div className="execution-table-wrap">
                <table className="execution-table">
                  <thead>
                    <tr>
                      <th>Backup ID</th>
                      <th>Created At</th>
                      <th>Version</th>
                      <th>Database Size</th>
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(backupListQuery.data as Array<{
                      backupId: string;
                      createdAt: string;
                      platformVersion: string;
                      database: { sizeBytes: number; sha256: string };
                    }>).map((b) => (
                      <tr key={b.backupId}>
                        <td>
                          <strong>{b.backupId}</strong>
                          <span style={{ fontFamily: "monospace" }}>SHA: {b.database.sha256?.slice(0, 12)}…</span>
                        </td>
                        <td>{new Date(b.createdAt).toLocaleString()}</td>
                        <td><span className="status-pill status-healthy">{b.platformVersion}</span></td>
                        <td>{FormatBytes(b.database.sizeBytes)}</td>
                        <td>
                          <button
                            className="secondary-button"
                            style={{ minHeight: "28px", padding: "0 10px", fontSize: "11px" }}
                            onClick={() => {
                              setSelectedRestoreBackupId(b.backupId);
                              setShowRestoreModal(true);
                            }}
                          >
                            <RotateCcw size={12} /> Restore
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
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

      {/* Restore Confirmation & Progress Modal */}
      {showRestoreModal && (
        <div className="restore-modal-backdrop">
          <div className="restore-modal panel">
            <div className="panel-header">
              <div>
                <span className="panel-eyebrow">Disaster Recovery Orchestration</span>
                <h3>Restore System Snapshot</h3>
              </div>
              <AlertOctagon size={20} color="var(--danger)" />
            </div>

            <div className="restore-modal-body">
              {!activeRestoreOpId ? (
                <>
                  <div className="restore-warning-banner">
                    <AlertTriangle size={18} />
                    <div>
                      <strong>Destructive Environment Overwrite:</strong>
                      <p>Restoring this snapshot will completely overwrite the active database tables and reconcile document storage to the exact snapshot state.</p>
                    </div>
                  </div>

                  <div className="restore-form-group">
                    <label>Target Snapshot to Restore</label>
                    <select
                      value={selectedRestoreBackupId}
                      onChange={(e) => setSelectedRestoreBackupId(e.target.value)}
                    >
                      {(backupListQuery.data as Array<{ backupId: string; createdAt: string }>)?.map((b) => (
                        <option key={b.backupId} value={b.backupId}>
                          {b.backupId} ({new Date(b.createdAt).toLocaleString()})
                        </option>
                      ))}
                    </select>
                  </div>

                  <div className="restore-form-group">
                    <label>Type <code>RESTORE-CONFIRM</code> to unlock:</label>
                    <input
                      type="text"
                      placeholder="RESTORE-CONFIRM"
                      value={restoreConfirmText}
                      onChange={(e) => setRestoreConfirmText(e.target.value)}
                    />
                  </div>

                  {restoreMutation.error && (
                    <div className="provider-warning">
                      <AlertTriangle size={16} />
                      <span>{getApiErrorMessage(restoreMutation.error)}</span>
                    </div>
                  )}

                  <div className="restore-modal-actions">
                    <button
                      className="secondary-button"
                      onClick={() => {
                        setShowRestoreModal(false);
                        setRestoreConfirmText("");
                      }}
                    >
                      Cancel
                    </button>
                    <button
                      className="danger-button"
                      disabled={restoreConfirmText !== "RESTORE-CONFIRM" || !selectedRestoreBackupId || restoreMutation.isPending}
                      onClick={() => restoreMutation.mutate({ backupId: selectedRestoreBackupId })}
                    >
                      <RotateCcw size={16} className={restoreMutation.isPending ? "spin" : ""} />
                      {restoreMutation.isPending ? "Starting Restore…" : "Execute Destructive Restore"}
                    </button>
                  </div>
                </>
              ) : (
                <div className="restore-progress-container">
                  <span className="panel-eyebrow">Asynchronous Restore Pipeline</span>
                  <h3>Operation {activeRestoreOpId.slice(0, 8)}</h3>

                  <div className="restore-state-card">
                    {restoreStatusQuery.data?.state === "Completed" ? (
                      <div className="restore-state-badge state-completed">
                        <CheckCircle2 size={24} />
                        <strong>Restore & Integrity Verification Succeeded</strong>
                        <p>The database, documents, and key-ring were restored and verified healthy.</p>
                      </div>
                    ) : restoreStatusQuery.data?.state === "Failed" ? (
                      <div className="restore-state-badge state-failed">
                        <XCircle size={24} />
                        <strong>Restore Failed</strong>
                        <p>{restoreStatusQuery.data?.errorMessage}</p>
                      </div>
                    ) : (
                      <div className="restore-state-badge state-running">
                        <RefreshCw size={24} className="spin" />
                        <strong>Status: {restoreStatusQuery.data?.state ?? "Queued"}…</strong>
                        <p>Decrypting payload, executing PostgreSQL restore, and verifying recovery.</p>
                      </div>
                    )}
                  </div>

                  {(restoreStatusQuery.data?.state === "Completed" || restoreStatusQuery.data?.state === "Failed") && (
                    <div className="restore-modal-actions">
                      <button
                        className="primary-button"
                        onClick={() => {
                          setShowRestoreModal(false);
                          setActiveRestoreOpId(null);
                          setRestoreConfirmText("");
                          void queryClient.invalidateQueries({ queryKey: ["operations"] });
                        }}
                      >
                        Close & Refresh Telemetry
                      </button>
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>
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
