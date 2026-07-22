import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, useSearchParams } from "react-router-dom";
import {
  Activity,
  AlertTriangle,
  BookOpen,
  CheckCircle2,
  ChevronRight,
  CircleDollarSign,
  Clock3,
  Cpu,
  Database,
  Eye,
  EyeOff,
  FileJson,
  Gauge,
  GitBranch,
  Network,
  RefreshCw,
  Search,
  ShieldAlert,
  Waypoints,
  XCircle,
  Zap,
} from "lucide-react";
import { getApiErrorMessage } from "../services/apiClient";
import { getTrace, getTraceOverview, listTraces } from "../services/traceApi";
import type { TraceArtifact, TraceDetail, TraceSummary } from "../types/trace";

type InspectorTab = "spans" | "events" | "artifacts" | "context";

export function TraceExplorerPage() {
  const [searchParams] = useSearchParams();
  const requestedRunId = searchParams.get("run") ?? "";
  const [selectedId, setSelectedId] = useState("");
  const [query, setQuery] = useState(requestedRunId);
  const [status, setStatus] = useState("");
  const [provider, setProvider] = useState("");
  const [capability, setCapability] = useState("");
  const [tab, setTab] = useState<InspectorTab>("spans");
  const [includeSensitive, setIncludeSensitive] = useState(false);
  const [selectedArtifactId, setSelectedArtifactId] = useState("");

  const overviewQuery = useQuery({ queryKey: ["trace-overview"], queryFn: getTraceOverview });
  const tracesQuery = useQuery({
    queryKey: ["traces", query, status, provider, capability],
    queryFn: () => listTraces({ query: query || undefined, status: status || undefined, provider: provider || undefined, capability: capability || undefined, limit: 500 }),
  });
  const overview = overviewQuery.data;
  const traces = tracesQuery.data ?? overview?.recentTraces ?? [];
  const requestedTraceId = requestedRunId
    ? traces.find(item => item.sourceRunId === requestedRunId || item.correlationId === requestedRunId)?.id ?? ""
    : "";
  const effectiveSelectedId = selectedId || requestedTraceId;
  const traceQuery = useQuery({
    queryKey: ["trace", effectiveSelectedId, includeSensitive],
    queryFn: () => getTrace(effectiveSelectedId, includeSensitive),
    enabled: Boolean(effectiveSelectedId),
  });
  const selected = traceQuery.data;
  const capabilities = overview?.capabilities.map(item => item.capability) ?? [];
  const selectedArtifact = selected?.artifacts.find(item => item.id === selectedArtifactId) ?? selected?.artifacts[0];

  const refresh = async () => {
    await Promise.all([overviewQuery.refetch(), tracesQuery.refetch(), effectiveSelectedId ? traceQuery.refetch() : Promise.resolve()]);
  };

  return (
    <div className="trace-explorer-page">
      <header className="studio-page-header trace-page-header">
        <div>
          <span className="page-eyebrow">Tracing capability</span>
          <h1>Trace Explorer</h1>
          <p>Inspect every execution across workflow, knowledge, prompt, intelligence, evaluation, cost, and replay correlations.</p>
        </div>
        <div className="trace-header-actions">
          <Link className="secondary-button" to="/documentation/traces"><BookOpen size={16} /> Documentation</Link>
          <button className="secondary-button" onClick={refresh} disabled={overviewQuery.isFetching || tracesQuery.isFetching}>
            <RefreshCw size={16} className={overviewQuery.isFetching || tracesQuery.isFetching ? "spin" : ""} /> Refresh traces
          </button>
        </div>
      </header>

      {(overviewQuery.isError || tracesQuery.isError) && (
        <div className="provider-warning"><XCircle size={16} /> {getApiErrorMessage(overviewQuery.error ?? tracesQuery.error)}</div>
      )}

      <section className="trace-metric-grid">
        <MetricCard icon={<Activity size={18} />} label="Traces" value={formatNumber(overview?.metrics.totalTraces)} detail={`${formatPercent(overview?.metrics.successRate)} success rate`} />
        <MetricCard icon={<Network size={18} />} label="Spans" value={formatNumber(overview?.metrics.totalSpans)} detail={`${formatNumber(overview?.metrics.failedTraces)} failed traces`} />
        <MetricCard icon={<Clock3 size={18} />} label="Average latency" value={formatDuration(overview?.metrics.averageDurationMs)} detail={`P95 ${formatDuration(overview?.metrics.p95DurationMs)}`} />
        <MetricCard icon={<Zap size={18} />} label="Tokens" value={formatNumber(overview?.metrics.totalTokens)} detail="Across recorded executions" />
        <MetricCard icon={<CircleDollarSign size={18} />} label="Actual cost" value={formatMoney(overview?.metrics.totalCost, overview?.metrics.currency)} detail="Persisted provider usage" />
      </section>

      <section className="trace-activity-panel panel">
        <div className="panel-header"><div><span className="panel-eyebrow">Seven-day telemetry</span><h3>Execution activity</h3></div><Gauge size={18} /></div>
        <div className="trace-activity-chart">
          {(overview?.activity ?? []).map(day => {
            const max = Math.max(1, ...(overview?.activity ?? []).map(item => item.traces));
            return <div className="trace-day" key={day.date}><span>{formatDay(day.date)}</span><div className="trace-day-bar"><i style={{ height: `${Math.max(8, day.traces / max * 100)}%` }} /></div><strong>{day.traces}</strong><small>{formatDuration(day.averageDurationMs)}</small></div>;
          })}
        </div>
        <div className="trace-capability-strip">
          {(overview?.capabilities ?? []).slice(0, 8).map(item => (
            <button key={item.capability} className={capability === item.capability ? "active" : ""} onClick={() => setCapability(capability === item.capability ? "" : item.capability)}>
              <span>{item.capability}</span><strong>{item.spans}</strong><small>{formatPercent(item.share)}</small>
            </button>
          ))}
        </div>
      </section>

      <section className="trace-workspace">
        <article className="panel trace-list-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Persisted runtime telemetry</span><h3>Execution traces</h3></div><span className="result-count">{traces.length}</span></div>
          <div className="trace-filter-grid">
            <label className="trace-search"><Search size={15} /><input value={query} onChange={event => setQuery(event.target.value)} placeholder="Search simulation, provider, model, correlation?" /></label>
            <select value={status} onChange={event => setStatus(event.target.value)}><option value="">All statuses</option>{(overview?.statuses ?? []).map(item => <option key={item}>{item}</option>)}</select>
            <select value={provider} onChange={event => setProvider(event.target.value)}><option value="">All providers</option>{(overview?.providers ?? []).map(item => <option key={item}>{item}</option>)}</select>
            <select value={capability} onChange={event => setCapability(event.target.value)}><option value="">All capabilities</option>{capabilities.map(item => <option key={item}>{item}</option>)}</select>
          </div>
          {tracesQuery.isLoading || overviewQuery.isLoading ? <TraceLoading /> : traces.length === 0 ? (
            <div className="inspector-placeholder"><Activity size={28} /><p>Run a Conversation Simulator message. Trace Explorer will synchronize the complete execution automatically.</p></div>
          ) : (
            <div className="trace-table-wrap">
              <table className="trace-table"><thead><tr><th>Status</th><th>Execution</th><th>Provider</th><th>Duration</th><th>Spans</th><th>Quality</th><th /></tr></thead><tbody>
                {traces.map(trace => <TraceRow key={trace.id} trace={trace} selected={trace.id === effectiveSelectedId} onSelect={() => { setSelectedId(trace.id); setIncludeSensitive(false); setSelectedArtifactId(""); }} />)}
              </tbody></table>
            </div>
          )}
        </article>

        <article className="panel trace-inspector-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Trace detail</span><h3>{selected?.summary.simulationTitle ?? "Select an execution"}</h3></div>{selected && <StatusPill status={selected.summary.status} />}</div>
          {!effectiveSelectedId ? <div className="inspector-placeholder"><Waypoints size={30} /><p>Select a trace to inspect its waterfall, events, captured artifacts, and cross-capability context.</p></div> : traceQuery.isLoading ? <TraceLoading /> : traceQuery.isError ? <div className="provider-warning"><XCircle size={16} /> {getApiErrorMessage(traceQuery.error)}</div> : selected ? (
            <>
              <TraceSummaryHeader detail={selected} />
              <div className="trace-inspector-tabs">
                {(["spans", "events", "artifacts", "context"] as InspectorTab[]).map(value => <button key={value} className={tab === value ? "active" : ""} onClick={() => setTab(value)}>{value}</button>)}
              </div>
              {tab === "spans" && <SpanWaterfall trace={selected} />}
              {tab === "events" && <EventList trace={selected} />}
              {tab === "artifacts" && <ArtifactInspector artifacts={selected.artifacts} selected={selectedArtifact} onSelect={setSelectedArtifactId} includeSensitive={includeSensitive} onToggleSensitive={() => setIncludeSensitive(value => !value)} />}
              {tab === "context" && <ContextInspector trace={selected} />}
            </>
          ) : null}
        </article>
      </section>
    </div>
  );
}

function MetricCard({ icon, label, value, detail }: { icon: React.ReactNode; label: string; value: string; detail: string }) {
  return <article className="trace-metric-card"><span className="trace-metric-icon">{icon}</span><div><span>{label}</span><strong>{value}</strong><small>{detail}</small></div></article>;
}

function TraceRow({ trace, selected, onSelect }: { trace: TraceSummary; selected: boolean; onSelect: () => void }) {
  return <tr
    className={selected ? "selected" : ""}
    role="button"
    tabIndex={0}
    aria-pressed={selected}
    onClick={onSelect}
    onKeyDown={event => {
      if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        onSelect();
      }
    }}
  >
    <td><StatusPill status={trace.status} /></td>
    <td><strong>{trace.simulationTitle ?? trace.operationName}</strong><small>{trace.source} · {shortId(trace.correlationId)}</small></td>
    <td><strong>{trace.provider ?? "Platform"}</strong><small>{trace.model ?? trace.operationName}</small></td>
    <td><strong>{formatDuration(trace.durationMs)}</strong><small>{formatNumber(trace.totalTokens)} tokens</small></td>
    <td><strong>{trace.spanCount}</strong><small>{trace.failedSpanCount ? `${trace.failedSpanCount} failed` : "Healthy"}</small></td>
    <td><span className={`evaluation-verdict verdict-${(trace.evaluationVerdict ?? "review").toLowerCase()}`}>{trace.evaluationVerdict ?? "N/A"}</span></td>
    <td><ChevronRight size={16} /></td>
  </tr>;
}

function TraceSummaryHeader({ detail }: { detail: TraceDetail }) {
  const item = detail.summary;
  return <div className="trace-detail-summary">
    <div><span>Correlation</span><strong title={item.correlationId}>{shortId(item.correlationId)}</strong></div>
    <div><span>Duration</span><strong>{formatDuration(item.durationMs)}</strong></div>
    <div><span>Provider</span><strong>{item.provider ?? "Platform"}</strong></div>
    <div><span>Cost</span><strong>{formatMoney(item.actualCost, item.currency)}</strong></div>
  </div>;
}

function SpanWaterfall({ trace }: { trace: TraceDetail }) {
  const rootStart = new Date(trace.summary.startedAt).getTime();
  const total = Math.max(1, trace.summary.durationMs);
  return <div className="trace-waterfall">
    {trace.spans.map(span => {
      const offset = Math.max(0, new Date(span.startedAt).getTime() - rootStart);
      const left = Math.min(96, offset / total * 100);
      const width = Math.max(1.5, Math.min(100 - left, span.durationMs / total * 100));
      return <div className={`waterfall-row waterfall-${span.status.toLowerCase()}`} key={span.id}>
        <div className="waterfall-copy" style={{ paddingLeft: span.parentSpanId ? 16 : 0 }}><strong>{span.name}</strong><span>{span.capability}</span></div>
        <div className="waterfall-track"><i style={{ left: `${left}%`, width: `${width}%` }} /><span>{formatDuration(span.durationMs)}</span></div>
        <p>{span.detail}</p>
        {Object.keys(span.attributes).length > 0 && <div className="attribute-chips">{Object.entries(span.attributes).map(([key, value]) => <span key={key}>{key}: {value}</span>)}</div>}
      </div>;
    })}
  </div>;
}

function EventList({ trace }: { trace: TraceDetail }) {
  return <div className="trace-event-list">{trace.events.map(event => <article key={event.id} className={`trace-event event-${event.level.toLowerCase()}`}><span className="event-dot" /><div><strong>{event.name}</strong><p>{event.message}</p><small>{formatDateTime(event.occurredAt)} · {event.level}</small></div></article>)}</div>;
}

function ArtifactInspector({ artifacts, selected, onSelect, includeSensitive, onToggleSensitive }: { artifacts: TraceArtifact[]; selected?: TraceArtifact; onSelect: (id: string) => void; includeSensitive: boolean; onToggleSensitive: () => void }) {
  return <div className="trace-artifact-workspace">
    <div className="artifact-list">{artifacts.map(item => <button key={item.id} className={selected?.id === item.id ? "active" : ""} onClick={() => onSelect(item.id)}><FileJson size={15} /><span><strong>{item.name}</strong><small>{item.kind}{item.isSensitive ? " · sensitive" : ""}</small></span></button>)}</div>
    <div className="artifact-preview">
      <div className="artifact-preview-header"><div><strong>{selected?.name ?? "Artifact"}</strong><small>{selected?.contentType}</small></div>{selected?.isSensitive && <button className="secondary-button" onClick={onToggleSensitive}>{includeSensitive ? <EyeOff size={14} /> : <Eye size={14} />} {includeSensitive ? "Redact" : "Reveal"}</button>}</div>
      <pre>{selected?.content ?? "No captured artifacts."}</pre>
    </div>
  </div>;
}

function ContextInspector({ trace }: { trace: TraceDetail }) {
  const item = trace.summary;
  return <div className="trace-context-grid">
    <ContextCard icon={<GitBranch size={17} />} label="Workflow" value={item.workflow ?? "Not captured"} />
    <ContextCard icon={<Cpu size={17} />} label="Provider / model" value={[item.provider, item.model].filter(Boolean).join(" / ") || "Platform runtime"} />
    <ContextCard icon={<Database size={17} />} label="Knowledge" value={item.knowledgeCollection ?? "Not captured"} />
    <ContextCard icon={<ShieldAlert size={17} />} label="Evaluation" value={item.evaluationVerdict ?? "Not evaluated"} />
    <ContextCard icon={<Zap size={17} />} label="Usage" value={`${formatNumber(item.totalTokens)} tokens · ${formatMoney(item.actualCost, item.currency)}`} />
    <ContextCard icon={<Activity size={17} />} label="Source" value={`${item.source} · ${item.sourceRunId ? shortId(item.sourceRunId) : "runtime"}`} />
    {item.failureReason && <div className="trace-failure-context"><AlertTriangle size={17} /><div><strong>Failure reason</strong><p>{item.failureReason}</p></div></div>}
    <div className="trace-context-actions">
      {item.simulationId && <Link className="secondary-button" to={`/conversations?simulation=${item.simulationId}&run=${item.sourceRunId ?? ""}`}>Open simulation</Link>}
      {item.sourceRunId && <Link className="secondary-button" to={`/evaluations?run=${item.sourceRunId}`}>Open evaluation</Link>}
      <Link className="secondary-button" to="/intelligence">Open intelligence</Link>
    </div>
  </div>;
}

function ContextCard({ icon, label, value }: { icon: React.ReactNode; label: string; value: string }) {
  return <article className="trace-context-card"><span>{icon}</span><div><small>{label}</small><strong>{value}</strong></div></article>;
}

function StatusPill({ status }: { status: string }) {
  const failed = status.toLowerCase() === "failed";
  return <span className={`trace-status trace-status-${status.toLowerCase()}`}>{failed ? <XCircle size={13} /> : status.toLowerCase() === "completed" ? <CheckCircle2 size={13} /> : <Activity size={13} />}{status}</span>;
}

function TraceLoading() { return <div className="trace-loading"><RefreshCw size={20} className="spin" /><span>Loading trace telemetry…</span></div>; }
function formatDuration(value?: number) { const ms = value ?? 0; return ms >= 1000 ? `${(ms / 1000).toFixed(2)} s` : `${ms.toFixed(ms >= 100 ? 0 : 1)} ms`; }
function formatNumber(value?: number) { return new Intl.NumberFormat("en-ZA").format(value ?? 0); }
function formatPercent(value?: number) { return `${Math.round((value ?? 0) * 100)}%`; }
function formatMoney(value?: number, currency = "ZAR") { return new Intl.NumberFormat("en-ZA", { style: "currency", currency: currency || "ZAR", maximumFractionDigits: 4 }).format(value ?? 0); }
function formatDay(value: string) { return new Date(`${value}T00:00:00`).toLocaleDateString("en-ZA", { weekday: "short" }); }
function formatDateTime(value: string) { return new Date(value).toLocaleString("en-ZA"); }
function shortId(value: string) { return value.split("-")[0]; }
