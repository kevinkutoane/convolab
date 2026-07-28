import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  AlertTriangle, CheckCircle2, Coins,
  Download, FileDown, Gauge, ShieldCheck, Sparkles, UsersRound,
} from "lucide-react";
import { ErrorState, LoadingState, EmptyState } from "../components/AsyncStates";
import { MetricCard } from "../components/MetricCard";
import { useAuth } from "../contexts/useAuth";
import { useEnvironment } from "../contexts/EnvironmentContext";
import { createAnalyticsExport, getAnalyticsDashboard, getAnalyticsEvents, getAnalyticsExports, type AnalyticsFilters } from "../services/analyticsApi";
import { getApiErrorMessage } from "../services/apiClient";
import type { AnalyticsMetric, AnalyticsPoint } from "../types/analytics";
import "../analytics.css";

type AnalyticsTab = "overview" | "usage" | "cost" | "quality" | "governance" | "performance" | "adoption" | "events" | "exports";

const tabs: { id: AnalyticsTab; label: string; roles: string[] }[] = [
  { id: "overview", label: "Overview", roles: ["Administrator", "Engineer", "Reviewer", "Operator", "Viewer"] },
  { id: "usage", label: "Usage", roles: ["Administrator", "Engineer", "Reviewer", "Operator"] },
  { id: "cost", label: "Cost & Budget", roles: ["Administrator", "Engineer", "Operator"] },
  { id: "quality", label: "Quality", roles: ["Administrator", "Engineer", "Reviewer"] },
  { id: "governance", label: "Governance", roles: ["Administrator", "Reviewer", "Operator"] },
  { id: "performance", label: "Performance", roles: ["Administrator", "Engineer", "Reviewer", "Operator"] },
  { id: "adoption", label: "Adoption", roles: ["Administrator", "Reviewer"] },
  { id: "events", label: "Events", roles: ["Administrator", "Engineer", "Reviewer"] },
  { id: "exports", label: "Exports", roles: ["Administrator"] },
];

const endpointFor = (tab: AnalyticsTab) => tab === "cost" ? "cost" : tab;
const iconFor = (key: string) => key.includes("cost") ? Coins : key.includes("actor") ? UsersRound : key.includes("success") ? CheckCircle2 : key.includes("token") ? Sparkles : Gauge;

export function AnalyticsPage() {
  const auth = useAuth();
  const environment = useEnvironment();
  const queryClient = useQueryClient();
  const workspaceId = auth.session?.activeWorkspaceId;
  const role = auth.session?.workspaces.find(item => item.id === workspaceId)?.role ?? "Viewer";
  const availableTabs = tabs.filter(tab => tab.roles.includes(role));
  const [tab, setTab] = useState<AnalyticsTab>("overview");
  const [days, setDays] = useState(30);
  const [granularity, setGranularity] = useState<"day" | "hour">("day");
  const [provider, setProvider] = useState("");
  const [model, setModel] = useState("");
  const [capability, setCapability] = useState("");
  const [outcome, setOutcome] = useState("");
  const [prompt, setPrompt] = useState("");
  const [workflow, setWorkflow] = useState("");
  const [configurationRevision, setConfigurationRevision] = useState("");
  const activeTab = availableTabs.some(item => item.id === tab) ? tab : availableTabs[0]?.id ?? "overview";
  const filters = useMemo<AnalyticsFilters>(() => {
    const to = new Date();
    return {
      environmentId: environment.activeEnvironmentId,
      from: new Date(to.getTime() - days * 86_400_000).toISOString(),
      to: to.toISOString(),
      granularity,
      provider: provider || undefined,
      model: model || undefined,
      capability: capability || undefined,
      outcome: outcome || undefined,
      prompt: prompt || undefined,
      workflow: workflow || undefined,
      configurationRevision: configurationRevision || undefined,
    };
  }, [environment.activeEnvironmentId, days, granularity, provider, model, capability, outcome, prompt, workflow, configurationRevision]);

  const dashboardQuery = useQuery({
    queryKey: ["analytics", workspaceId, environment.activeEnvironmentId, activeTab, filters],
    queryFn: () => getAnalyticsDashboard(workspaceId!, endpointFor(activeTab), filters),
    enabled: Boolean(workspaceId && !["events", "exports"].includes(activeTab)),
    retry: 1,
  });
  const eventsQuery = useQuery({
    queryKey: ["analytics-events", workspaceId, environment.activeEnvironmentId, filters],
    queryFn: () => getAnalyticsEvents(workspaceId!, filters),
    enabled: Boolean(workspaceId && activeTab === "events"),
    retry: 1,
  });
  const exportsQuery = useQuery({
    queryKey: ["analytics-exports", workspaceId],
    queryFn: () => getAnalyticsExports(workspaceId!),
    enabled: Boolean(workspaceId && activeTab === "exports"),
    refetchInterval: query => query.state.data?.some(item => item.status === "Pending") ? 2_000 : false,
  });
  const exportMutation = useMutation({
    mutationFn: () => createAnalyticsExport(workspaceId!, filters),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["analytics-exports", workspaceId] }),
  });

  if (!workspaceId) return <EmptyState title="Select a workspace" description="Analytics is scoped to an active workspace." />;

  return (
    <section className="analytics-page">
      <header className="analytics-header">
        <div>
          <span className="eyebrow">Platform Analytics v1</span>
          <h1>Operational evidence, without customer content</h1>
          <p>Usage, ZAR cost classification, quality, governance, performance and adoption for the selected runtime environment.</p>
        </div>
        <div className="analytics-scope">
          <strong>{environment.activeEnvironment?.name ?? "Resolving environment"}</strong>
          <span>{environment.activeEnvironment?.environmentType ?? "Workspace"} · last {days} days</span>
        </div>
      </header>

      <div className="analytics-filters" aria-label="Analytics filters">
        <label>Period<select value={days} onChange={event => setDays(Number(event.target.value))}><option value={7}>7 days</option><option value={30}>30 days</option><option value={90}>90 days</option></select></label>
        <label>Granularity<select value={granularity} onChange={event => setGranularity(event.target.value as "day" | "hour")}><option value="day">Daily</option><option value="hour">Hourly</option></select></label>
        <label>Provider<input value={provider} onChange={event => setProvider(event.target.value)} placeholder="All providers" /></label>
        <label>Model<input value={model} onChange={event => setModel(event.target.value)} placeholder="All models" /></label>
        <label>Capability<input value={capability} onChange={event => setCapability(event.target.value)} placeholder="All capabilities" /></label>
        <label>Outcome<select value={outcome} onChange={event => setOutcome(event.target.value)}><option value="">All outcomes</option><option>Succeeded</option><option>Failed</option><option>Denied</option></select></label>
        <label>Prompt<input value={prompt} onChange={event => setPrompt(event.target.value)} placeholder="Prompt reference" /></label>
        <label>Workflow<input value={workflow} onChange={event => setWorkflow(event.target.value)} placeholder="Workflow reference" /></label>
        <label>Configuration revision<input value={configurationRevision} onChange={event => setConfigurationRevision(event.target.value)} placeholder="All revisions" /></label>
      </div>

      <nav className="analytics-tabs" aria-label="Analytics views">
        {availableTabs.map(item => <button key={item.id} aria-current={activeTab === item.id ? "page" : undefined} className={activeTab === item.id ? "active" : ""} onClick={() => setTab(item.id)}>{item.label}</button>)}
      </nav>

      {!["events", "exports"].includes(activeTab) && (
        dashboardQuery.isPending ? <LoadingState label="Aggregating analytics…" /> :
        dashboardQuery.isError ? <ErrorState message={getApiErrorMessage(dashboardQuery.error)} onRetry={() => void dashboardQuery.refetch()} /> :
        dashboardQuery.data && <Dashboard data={dashboardQuery.data} />
      )}

      {activeTab === "events" && (
        eventsQuery.isPending ? <LoadingState label="Loading safe event metadata…" /> :
        eventsQuery.isError ? <ErrorState message={getApiErrorMessage(eventsQuery.error)} onRetry={() => void eventsQuery.refetch()} /> :
        eventsQuery.data?.items.length ? <div className="analytics-panel analytics-events"><div className="panel-heading"><div><span>Append-only evidence</span><h2>Events</h2></div><small>Actor identity appears only when your role permits it.</small></div><div className="analytics-table-wrap"><table><thead><tr><th>Occurred</th><th>Capability</th><th>Event</th><th>Outcome</th><th>Provider / model</th><th>Usage</th><th>Cost</th><th>Correlation</th></tr></thead><tbody>{eventsQuery.data.items.map(item => <tr key={item.id}><td>{new Date(item.occurredAt).toLocaleString("en-ZA")}</td><td>{item.capability}</td><td>{item.eventType}</td><td><span className={`analytics-outcome outcome-${item.outcome.toLowerCase()}`}>{item.outcome}</span></td><td>{item.provider ?? "—"}<small>{item.model}</small></td><td>{(item.inputTokens ?? 0) + (item.outputTokens ?? 0)} tokens</td><td>{item.costZar === undefined ? "Unavailable" : `R ${item.costZar.toFixed(4)}`}<small>{item.costType}</small></td><td><code>{item.correlationId.slice(0, 12)}</code></td></tr>)}</tbody></table></div></div> :
        <EmptyState title="No events in this period" description="Run a simulation in this environment or broaden the selected period." />
      )}

      {activeTab === "exports" && (
        <div className="analytics-panel analytics-exports">
          <div className="panel-heading"><div><span>Secure CSV</span><h2>Exports</h2></div><button className="primary-button" disabled={exportMutation.isPending} onClick={() => exportMutation.mutate()}><FileDown size={16} /> Create export</button></div>
          {exportMutation.isError && <p className="analytics-error">{getApiErrorMessage(exportMutation.error)}</p>}
          {exportsQuery.isPending ? <LoadingState label="Loading exports…" compact /> : exportsQuery.data?.length ? <div className="export-list">{exportsQuery.data.map(item => <article key={item.id}><FileDown size={20} /><div><strong>{item.fileName}</strong><span>{item.status} · {item.rowCount ?? 0} rows · expires {new Date(item.expiresAt).toLocaleDateString("en-ZA")}</span></div>{item.status === "Completed" && <a className="secondary-button" href={`/api/workspaces/${workspaceId}/analytics/exports/${item.id}/download`}><Download size={15} /> Download</a>}</article>)}</div> : <EmptyState title="No exports" description="Exports contain safe analytics metadata only and expire after seven days." />}
        </div>
      )}
    </section>
  );
}

function Dashboard({ data }: { data: import("../types/analytics").AnalyticsDashboard }) {
  if (data.metrics[0]?.value === 0 && data.series.length === 0) return <EmptyState title="No analytics yet" description="Activity recorded in the selected environment will appear here without requiring a page refresh." />;
  return <>
    {data.isPartial && <div className="analytics-notice"><AlertTriangle size={17} /> Some cost or quality dimensions are unavailable for this period.</div>}
    <div className="analytics-metrics">{data.metrics.slice(0, 6).map(metric => <Metric key={metric.key} metric={metric} />)}</div>
    <div className="analytics-grid">
      <div className="analytics-panel"><div className="panel-heading"><div><span>Execution trend</span><h2>{data.category[0].toUpperCase() + data.category.slice(1)}</h2></div><small>UTC · {data.scope.granularity}</small></div><TrendChart points={data.series} /></div>
      <aside className="analytics-panel"><div className="panel-heading"><div><span>Deterministic rules</span><h2>Operational indicators</h2></div><ShieldCheck size={19} /></div>{data.indicators.length ? <div className="indicator-list">{data.indicators.map(item => <article key={item.key}><AlertTriangle size={16} /><div><strong>{item.title}</strong><p>{item.detail}</p></div></article>)}</div> : <div className="analytics-healthy"><CheckCircle2 size={28} /><strong>No rule-based indicators</strong><span>The selected period is within configured operating thresholds.</span></div>}</aside>
    </div>
  </>;
}

function Metric({ metric }: { metric: AnalyticsMetric }) {
  const value = metric.value === undefined || metric.value === null ? "Unavailable" : metric.unit === "ZAR" ? `R ${metric.value.toFixed(2)}` : metric.unit === "percent" ? `${metric.value.toFixed(1)}%` : Intl.NumberFormat("en-ZA", { notation: metric.value > 9999 ? "compact" : "standard" }).format(metric.value);
  return <MetricCard label={metric.label} value={value} detail={metric.classification ?? metric.unit} icon={iconFor(metric.key)} tone={metric.classification === "Unavailable" ? "warning" : "default"} />;
}

function TrendChart({ points }: { points: AnalyticsPoint[] }) {
  const max = Math.max(1, ...points.map(point => point.executions));
  return <div className="analytics-chart"><svg viewBox="0 0 720 220" role="img" aria-labelledby="analytics-chart-title analytics-chart-desc"><title id="analytics-chart-title">Execution volume over time</title><desc id="analytics-chart-desc">{points.map(point => `${new Date(point.bucket).toLocaleDateString("en-ZA")}: ${point.executions}`).join(", ")}</desc>{points.map((point, index) => { const width = 680 / Math.max(1, points.length); const height = point.executions / max * 170; return <g key={point.bucket}><rect x={20 + index * width} y={190 - height} width={Math.max(3, width - 5)} height={height} rx="3" /><title>{new Date(point.bucket).toLocaleDateString("en-ZA")}: {point.executions} executions</title></g>; })}<line x1="20" y1="190" x2="700" y2="190" /></svg><div className="analytics-data-alternative"><table><thead><tr><th>Period</th><th>Executions</th><th>Succeeded</th><th>Failed</th><th>Denied</th></tr></thead><tbody>{points.slice(-10).map(point => <tr key={point.bucket}><td>{new Date(point.bucket).toLocaleDateString("en-ZA")}</td><td>{point.executions}</td><td>{point.succeeded}</td><td>{point.failed}</td><td>{point.denied}</td></tr>)}</tbody></table></div></div>;
}
