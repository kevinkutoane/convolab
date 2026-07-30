import { useEffect, useMemo, useState } from "react";
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router";
import {
  AlertTriangle, CheckCircle2, Coins,
  Download, FileDown, Gauge, ShieldCheck, Sparkles, UsersRound, X,
} from "lucide-react";
import { ErrorState, LoadingState, EmptyState } from "../components/AsyncStates";
import { MetricCard } from "../components/MetricCard";
import { SingletonSelect } from "../components/StudioPrimitives";
import { useAuth } from "../contexts/useAuth";
import { useEnvironment } from "../contexts/EnvironmentContext";
import { createAnalyticsExport, getAnalyticsCorrelation, getAnalyticsDashboard, getAnalyticsEvent, getAnalyticsEvents, getAnalyticsExports, getAnalyticsFilterOptions, type AnalyticsFilters } from "../services/analyticsApi";
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

const endpointFor = (tab: AnalyticsTab) => tab === "cost" ? "budget" : tab;
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
  const [knowledgeCollection, setKnowledgeCollection] = useState("");
  const [configurationRevision, setConfigurationRevision] = useState("");
  const [eventType, setEventType] = useState("");
  const [costType, setCostType] = useState("");
  const [selectedEventId, setSelectedEventId] = useState("");
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
      knowledgeCollection: knowledgeCollection || undefined,
      configurationRevision: configurationRevision || undefined,
      eventType: eventType || undefined,
      costType: costType || undefined,
    };
  }, [environment.activeEnvironmentId, days, granularity, provider, model, capability, outcome, prompt, workflow, knowledgeCollection, configurationRevision, eventType, costType]);

  const dashboardQuery = useQuery({
    queryKey: ["analytics", workspaceId, environment.activeEnvironmentId, activeTab, filters],
    queryFn: () => getAnalyticsDashboard(workspaceId!, endpointFor(activeTab), filters),
    enabled: Boolean(workspaceId && !["events", "exports"].includes(activeTab)),
    retry: 1,
  });
  const filterOptionsQuery = useQuery({
    queryKey: ["analytics-filter-options", workspaceId, filters],
    queryFn: () => getAnalyticsFilterOptions(workspaceId!, filters),
    enabled: Boolean(workspaceId && role !== "Viewer"),
    staleTime: 60_000,
  });
  const eventsQuery = useInfiniteQuery({
    queryKey: ["analytics-events", workspaceId, environment.activeEnvironmentId, filters],
    queryFn: ({ pageParam }) => getAnalyticsEvents(workspaceId!, filters, pageParam || undefined),
    initialPageParam: "",
    getNextPageParam: page => page.nextCursor,
    enabled: Boolean(workspaceId && activeTab === "events"),
    retry: 1,
  });
  const eventItems = eventsQuery.data?.pages.flatMap(page => page.items) ?? [];
  const eventTotal = eventsQuery.data?.pages[0]?.totalCount ?? 0;
  const selectedEventIdInScope = eventItems.some(item => item.id === selectedEventId)
    ? selectedEventId
    : "";
  const exportsQuery = useQuery({
    queryKey: ["analytics-exports", workspaceId],
    queryFn: () => getAnalyticsExports(workspaceId!),
    enabled: Boolean(workspaceId && activeTab === "exports"),
    refetchInterval: query => query.state.data?.some(item => item.status === "Pending") ? 2_000 : false,
  });
  const eventDetailQuery = useQuery({
    queryKey: ["analytics-event", workspaceId, selectedEventIdInScope],
    queryFn: () => getAnalyticsEvent(workspaceId!, selectedEventIdInScope),
    enabled: Boolean(workspaceId && selectedEventIdInScope),
  });
  const correlationQuery = useQuery({
    queryKey: ["analytics-correlation", workspaceId, eventDetailQuery.data?.correlationId],
    queryFn: () => getAnalyticsCorrelation(workspaceId!, eventDetailQuery.data!.correlationId),
    enabled: Boolean(workspaceId && eventDetailQuery.data?.correlationId),
  });
  const exportMutation = useMutation({
    mutationFn: () => createAnalyticsExport(workspaceId!, filters),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["analytics-exports", workspaceId] }),
  });

  useEffect(() => {
    const options = filterOptionsQuery.data;
    if (!options) return;
    const clearIfMissing = (
      selected: string,
      values: string[],
      clear: (value: string) => void,
    ) => {
      if (selected && !values.includes(selected)) clear("");
    };
    clearIfMissing(provider, options.providers, setProvider);
    clearIfMissing(model, options.models, setModel);
    clearIfMissing(capability, options.capabilities, setCapability);
    clearIfMissing(outcome, options.outcomes, setOutcome);
    clearIfMissing(prompt, options.prompts, setPrompt);
    clearIfMissing(workflow, options.workflows, setWorkflow);
    clearIfMissing(knowledgeCollection, options.knowledgeCollections, setKnowledgeCollection);
    clearIfMissing(configurationRevision, options.configurationRevisions, setConfigurationRevision);
    clearIfMissing(eventType, options.eventTypes, setEventType);
    clearIfMissing(costType, options.costTypes, setCostType);
  }, [filterOptionsQuery.data, provider, model, capability, outcome, prompt, workflow, knowledgeCollection, configurationRevision, eventType, costType]);

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
        <EnvironmentFilter
          environments={environment.environments.filter(item => item.status === "Active")}
          value={environment.activeEnvironmentId ?? ""}
          disabled={environment.isSwitching}
          onChange={value => void environment.setActiveEnvironmentId(value)}
        />
        <label>Period<select value={days} onChange={event => setDays(Number(event.target.value))}><option value={7}>7 days</option><option value={30}>30 days</option><option value={90}>90 days</option></select></label>
        <label>Granularity<select value={granularity} onChange={event => setGranularity(event.target.value as "day" | "hour")}><option value="day">Daily</option><option value="hour">Hourly</option></select></label>
        {role !== "Viewer" && <>
          <SingletonSelect label="Provider" value={provider} values={filterOptionsQuery.data?.providers} onChange={setProvider} />
          <SingletonSelect label="Model" value={model} values={filterOptionsQuery.data?.models} onChange={setModel} />
          <SingletonSelect label="Capability" value={capability} values={filterOptionsQuery.data?.capabilities} onChange={setCapability} />
          <SingletonSelect label="Outcome" value={outcome} values={filterOptionsQuery.data?.outcomes} onChange={setOutcome} />
          <SingletonSelect label="Prompt" value={prompt} values={filterOptionsQuery.data?.prompts} onChange={setPrompt} />
          <SingletonSelect label="Workflow" value={workflow} values={filterOptionsQuery.data?.workflows} onChange={setWorkflow} />
          <SingletonSelect label="Knowledge" value={knowledgeCollection} values={filterOptionsQuery.data?.knowledgeCollections} onChange={setKnowledgeCollection} />
          <SingletonSelect label="Configuration revision" value={configurationRevision} values={filterOptionsQuery.data?.configurationRevisions} onChange={setConfigurationRevision} />
          <SingletonSelect label="Event type" value={eventType} values={filterOptionsQuery.data?.eventTypes} onChange={setEventType} />
          <SingletonSelect label="Cost classification" value={costType} values={filterOptionsQuery.data?.costTypes} onChange={setCostType} />
        </>}
      </div>

      <div className="analytics-recording-note">
        <ShieldCheck size={19} />
        <div>
          <strong>{role === "Viewer" ? "Aggregated evidence" : "Recorded in this period"}</strong>
          <span>{role === "Viewer" ? "Your role receives overview totals without event or actor detail." : filterOptionsQuery.isPending ? "Discovering event types…" : filterOptionsQuery.data?.eventTypes.length ? filterOptionsQuery.data.eventTypes.join(", ") : "No events recorded for this environment and period."}</span>
          <p>Events contain safe operational metadata—scope, outcome, provider/model, tokens, classified ZAR cost, duration, quality, source, configuration revision, and correlation. Prompts, messages, trace content, credentials, and secret settings are never stored in analytics.</p>
        </div>
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
        eventItems.length ? <div className="analytics-panel analytics-events"><div className="panel-heading"><div><span>Append-only evidence</span><h2>{eventTotal.toLocaleString("en-ZA")} events</h2></div><small>Select a row for its safe metadata and correlation.</small></div><div className="analytics-table-wrap"><table><thead><tr><th>Occurred</th><th>Capability</th><th>Event</th><th>Outcome</th><th>Provider / model</th><th>Usage</th><th>Cost</th><th>Correlation</th></tr></thead><tbody>{eventItems.map(item => <tr key={item.id} tabIndex={0} className={selectedEventId === item.id ? "selected" : ""} onClick={() => setSelectedEventId(item.id)} onKeyDown={event => { if (event.key === "Enter" || event.key === " ") setSelectedEventId(item.id); }}><td>{new Date(item.occurredAt).toLocaleString("en-ZA")}</td><td>{item.capability}</td><td>{item.eventType}</td><td><span className={`analytics-outcome outcome-${item.outcome.toLowerCase()}`}>{item.outcome}</span></td><td>{item.provider ?? "—"}<small>{item.model}</small></td><td>{item.inputTokens === undefined && item.outputTokens === undefined ? "Restricted" : `${(item.inputTokens ?? 0) + (item.outputTokens ?? 0)} tokens`}</td><td>{item.costZar === undefined ? item.costType : `R ${item.costZar.toFixed(4)}`}<small>{item.costType}</small></td><td><code>{item.correlationId.slice(0, 12)}</code></td></tr>)}</tbody></table></div>{eventsQuery.hasNextPage && <button className="secondary-button analytics-load-more" disabled={eventsQuery.isFetchingNextPage} onClick={() => void eventsQuery.fetchNextPage()}>{eventsQuery.isFetchingNextPage ? "Loading…" : `Load more (${eventItems.length} of ${eventTotal})`}</button>}</div> :
        <EmptyState title="No events in this period" description="Run a simulation in this environment or broaden the selected period." />
      )}

      {selectedEventIdInScope && <EventDrawer
        loading={eventDetailQuery.isPending}
        item={eventDetailQuery.data}
        correlation={correlationQuery.data}
        onClose={() => setSelectedEventId("")}
      />}

      {activeTab === "exports" && (
        <div className="analytics-panel analytics-exports">
          <div className="panel-heading"><div><span>Secure CSV</span><h2>Exports</h2></div><button className="primary-button" disabled={exportMutation.isPending} onClick={() => exportMutation.mutate()}><FileDown size={16} /> Create export</button></div>
          {exportMutation.isError && <p className="analytics-error">{getApiErrorMessage(exportMutation.error)}</p>}
          {exportsQuery.isPending ? <LoadingState label="Loading exports…" compact /> : exportsQuery.data?.length ? <div className="export-list">{exportsQuery.data.map(item => <article key={item.id}><FileDown size={20} /><div><strong>{item.fileName}</strong><span>{item.status} · {item.rowCount ?? 0} rows · expires {new Date(item.expiresAt).toLocaleDateString("en-ZA")}</span></div>{item.status === "Completed" && <a className="secondary-button" href={`/api/workspaces/${workspaceId}/analytics/exports/${item.id}/download`}><Download size={15} /> Download</a>}</article>)}</div> : <EmptyState title="No exports" description="Exports contain safe analytics metadata and use the configured retention period." />}
        </div>
      )}
    </section>
  );
}

function EnvironmentFilter({
  environments,
  value,
  disabled,
  onChange,
}: {
  environments: { id: string; name: string; isDefault: boolean }[];
  value: string;
  disabled: boolean;
  onChange: (value: string) => void;
}) {
  if (!environments.length) return null;
  if (environments.length === 1) {
    return <div className="singleton-context"><span>Environment</span><strong>{environments[0].name}</strong></div>;
  }
  return <label>Environment<select value={value} onChange={event => onChange(event.target.value)} disabled={disabled}>{environments.map(item => <option key={item.id} value={item.id}>{item.name}{item.isDefault ? " (default)" : ""}</option>)}</select></label>;
}

function Dashboard({ data }: { data: import("../types/analytics").AnalyticsDashboard }) {
  if (data.metrics[0]?.value === 0 && data.series.length === 0) return <EmptyState title="No analytics yet" description="Activity recorded in the selected environment will appear here without requiring a page refresh." />;
  return <>
    {data.isPartial && <div className="analytics-notice"><AlertTriangle size={17} /> Some cost or quality dimensions are unavailable for this period.</div>}
    <div className="analytics-metrics">{data.metrics.map(metric => <Metric key={metric.key} metric={metric} />)}</div>
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
  const max = Math.max(1, ...points.map(point => point.executionCount));
  return <div className="analytics-chart"><svg viewBox="0 0 720 220" role="img" aria-labelledby="analytics-chart-title analytics-chart-desc"><title id="analytics-chart-title">Distinct execution volume over time</title><desc id="analytics-chart-desc">{points.map(point => `${new Date(point.bucket).toLocaleDateString("en-ZA")}: ${point.executionCount} executions from ${point.eventCount} events`).join(", ")}</desc>{points.map((point, index) => { const width = 680 / Math.max(1, points.length); const height = point.executionCount / max * 170; return <g key={point.bucket}><rect x={20 + index * width} y={190 - height} width={Math.max(3, width - 5)} height={height} rx="3" /><title>{new Date(point.bucket).toLocaleDateString("en-ZA")}: {point.executionCount} distinct executions</title></g>; })}<line x1="20" y1="190" x2="700" y2="190" /></svg><div className="analytics-data-alternative"><table><thead><tr><th>Period</th><th>Events</th><th>Executions</th><th>Succeeded</th><th>Failed</th><th>Denied</th></tr></thead><tbody>{points.slice(-10).map(point => <tr key={point.bucket}><td>{new Date(point.bucket).toLocaleDateString("en-ZA")}</td><td>{point.eventCount}</td><td>{point.executionCount}</td><td>{point.succeeded}</td><td>{point.failed}</td><td>{point.denied}</td></tr>)}</tbody></table></div></div>;
}

function EventDrawer({
  loading,
  item,
  correlation,
  onClose,
}: {
  loading: boolean;
  item?: import("../types/analytics").AnalyticsEvent;
  correlation?: import("../types/analytics").AnalyticsEvent[];
  onClose: () => void;
}) {
  useEffect(() => {
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    window.addEventListener("keydown", closeOnEscape);
    return () => window.removeEventListener("keydown", closeOnEscape);
  }, [onClose]);

  return <aside className="analytics-event-drawer" role="dialog" aria-modal="true" aria-label="Analytics event detail">
    <div className="panel-heading"><div><span>Safe event metadata</span><h2>{item?.eventType ?? "Loading event"}</h2></div><button autoFocus className="icon-button" onClick={onClose} aria-label="Close event detail"><X size={17} /></button></div>
    {loading || !item ? <LoadingState compact label="Loading event detail…" /> : <>
      <dl className="analytics-event-detail">
        <div><dt>Outcome</dt><dd>{item.outcome}</dd></div>
        <div><dt>Occurred</dt><dd>{new Date(item.occurredAt).toLocaleString("en-ZA")}</dd></div>
        <div><dt>Provider / model</dt><dd>{item.provider ? `${item.provider} / ${item.model ?? "resolved"}` : "Restricted"}</dd></div>
        <div><dt>Cost</dt><dd>{item.costZar === undefined ? item.costType : `R ${item.costZar.toFixed(4)} (${item.costType})`}</dd></div>
        <div><dt>Configuration revision</dt><dd><code>{item.configurationRevision}</code></dd></div>
        <div><dt>Correlation</dt><dd><code>{item.correlationId}</code></dd></div>
        <div><dt>Source</dt><dd>{item.sourceType}{item.sourceId ? ` / ${item.sourceId}` : ""}</dd></div>
      </dl>
      <div className="analytics-source-links">
        {item.sourceExecutionId && <Link className="secondary-button" to={`/conversations?run=${item.sourceExecutionId}`}>Open simulation</Link>}
        {item.sourceExecutionId && <Link className="secondary-button" to={`/traces?run=${item.sourceExecutionId}`}>Open trace</Link>}
        {item.sourceExecutionId && <Link className="secondary-button" to={`/evaluations?run=${item.sourceExecutionId}`}>Open evaluation</Link>}
      </div>
      <div className="correlation-timeline">
        <strong>Correlation timeline</strong>
        {correlation?.map(entry => <div key={entry.id}><span>{new Date(entry.occurredAt).toLocaleTimeString("en-ZA")}</span><b>{entry.eventType}</b><small>{entry.outcome}</small></div>)}
      </div>
    </>}
  </aside>;
}
