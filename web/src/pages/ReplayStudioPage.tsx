import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import {
  Activity,
  Archive,
  ArrowDownRight,
  ArrowRight,
  ArrowUpRight,
  BookOpen,
  CheckCircle2,
  CircleDollarSign,
  Clock3,
  CopyPlus,
  Cpu,
  Database,
  FlaskConical,
  GitBranch,
  GitCompareArrows,
  Layers3,
  LoaderCircle,
  MessageSquareText,
  Plus,
  RefreshCw,
  RotateCcw,
  Sparkles,
  Target,
  XCircle,
} from "lucide-react";
import { getApiErrorMessage } from "../services/apiClient";
import {
  addReplayCandidate,
  archiveReplayExperiment,
  completeReplayExperiment,
  createReplayExperiment,
  getReplayExperiment,
  getReplayOverview,
} from "../services/replayApi";
import type { SimulationMode } from "../types/simulation";
import type {
  CreateReplayExperimentInput,
  ReplayCandidate,
  ReplayCandidateInput,
  ReplayExperimentDetail,
  ReplayRunSnapshot,
  ReplaySource,
} from "../types/replay";

type ReplayForm = {
  label: string;
  workflow: string;
  promptVersion: string;
  knowledgeCollection: string;
  provider: string;
  model: string;
  temperature: number;
  maxOutputTokens: number;
  mode: SimulationMode;
};

const fallbackForm: ReplayForm = {
  label: "Candidate A",
  workflow: "",
  promptVersion: "",
  knowledgeCollection: "",
  provider: "Deterministic",
  model: "",
  temperature: 0.2,
  maxOutputTokens: 400,
  mode: "Normal",
};

export function ReplayStudioPage() {
  const queryClient = useQueryClient();
  const [selectedExperimentId, setSelectedExperimentId] = useState("");
  const [selectedCandidateId, setSelectedCandidateId] = useState("");
  const [showCreate, setShowCreate] = useState(false);
  const [sourceRunId, setSourceRunId] = useState("");
  const [experimentName, setExperimentName] = useState("Controlled replay experiment");
  const [form, setForm] = useState<ReplayForm | null>(null);

  const overviewQuery = useQuery({ queryKey: ["replay-overview"], queryFn: getReplayOverview });
  const detailQuery = useQuery({
    queryKey: ["replay-experiment", selectedExperimentId || overviewQuery.data?.recentExperiments[0]?.id || ""],
    queryFn: () => getReplayExperiment(selectedExperimentId || overviewQuery.data?.recentExperiments[0]?.id || ""),
    enabled: Boolean(selectedExperimentId || overviewQuery.data?.recentExperiments[0]?.id),
  });

  const overview = overviewQuery.data;
  const effectiveExperimentId = selectedExperimentId || overview?.recentExperiments[0]?.id || "";
  const effectiveSourceRunId = sourceRunId || overview?.recentSources[0]?.runId || "";
  const selectedSource = overview?.recentSources.find(item => item.runId === effectiveSourceRunId) ?? overview?.recentSources[0];
  const detail = detailQuery.data;
  const selectedCandidate = detail?.candidates.find(item => item.id === selectedCandidateId) ?? detail?.candidates[0];
  const activeForm = form ?? createFormFromSnapshot(detail?.baseline.snapshot ?? selectedSource?.snapshot, overview);

  const invalidateReplay = async (experimentId?: string) => {
    await queryClient.invalidateQueries({ queryKey: ["replay-overview"] });
    if (experimentId) await queryClient.invalidateQueries({ queryKey: ["replay-experiment", experimentId] });
    await queryClient.invalidateQueries({ queryKey: ["simulations"] });
    await queryClient.invalidateQueries({ queryKey: ["evaluation-overview"] });
    await queryClient.invalidateQueries({ queryKey: ["trace-overview"] });
  };

  const createMutation = useMutation({
    mutationFn: (input: CreateReplayExperimentInput) => createReplayExperiment(input),
    onSuccess: async created => {
      setSelectedExperimentId(created.summary.id);
      setSelectedCandidateId(created.candidates[0]?.id ?? "");
      setShowCreate(false);
      await invalidateReplay(created.summary.id);
    },
  });

  const candidateMutation = useMutation({
    mutationFn: ({ experimentId, input }: { experimentId: string; input: ReplayCandidateInput }) => addReplayCandidate(experimentId, input),
    onSuccess: async updated => {
      setSelectedCandidateId(updated.candidates[0]?.id ?? "");
      setForm(current => ({ ...(current ?? createFormFromSnapshot(updated.baseline.snapshot, overview)), label: `Candidate ${String.fromCharCode(65 + updated.candidates.length)}` }));
      await invalidateReplay(updated.summary.id);
    },
  });

  const completeMutation = useMutation({
    mutationFn: completeReplayExperiment,
    onSuccess: async updated => invalidateReplay(updated.summary.id),
  });

  const archiveMutation = useMutation({
    mutationFn: archiveReplayExperiment,
    onSuccess: async updated => invalidateReplay(updated.summary.id),
  });

  const submitCreate = () => {
    if (!selectedSource || createMutation.isPending) return;
    createMutation.mutate({
      name: experimentName,
      simulationId: selectedSource.simulationId,
      sourceRunId: selectedSource.runId,
      candidateLabel: activeForm.label,
      ...toExecutionInput(activeForm),
    });
  };

  const submitCandidate = () => {
    if (!detail || candidateMutation.isPending) return;
    candidateMutation.mutate({ experimentId: detail.summary.id, input: { label: activeForm.label, ...toExecutionInput(activeForm) } });
  };

  const refresh = async () => {
    await overviewQuery.refetch();
    if (effectiveExperimentId) await detailQuery.refetch();
  };

  const error = overviewQuery.error ?? detailQuery.error ?? createMutation.error ?? candidateMutation.error ?? completeMutation.error ?? archiveMutation.error;

  return (
    <div className="replay-studio-page">
      <header className="studio-page-header replay-page-header">
        <div>
          <span className="page-eyebrow">Signature product capability</span>
          <h1>Replay Studio</h1>
          <p>Time-travel through an immutable conversation run, change one or more governed dimensions, and compare quality, latency, cost, trace, and evaluation outcomes.</p>
        </div>
        <div className="replay-header-actions">
          <Link className="secondary-button" to="/documentation/replay"><BookOpen size={16} /> Documentation</Link>
          <button className="secondary-button" onClick={refresh} disabled={overviewQuery.isFetching || detailQuery.isFetching}>
            <RefreshCw size={16} className={overviewQuery.isFetching || detailQuery.isFetching ? "spin" : ""} /> Refresh
          </button>
          <button
            className="primary-button"
            onClick={() => {
              setShowCreate(value => !value);
              setForm(createFormFromSnapshot(selectedSource?.snapshot, overview));
            }}
          >
            <Plus size={16} /> New experiment
          </button>
        </div>
      </header>

      {error && <div className="provider-warning"><XCircle size={16} /> {getApiErrorMessage(error)}</div>}

      <section className="replay-metric-grid">
        <ReplayMetric icon={<FlaskConical size={18} />} label="Experiments" value={formatNumber(overview?.metrics.totalExperiments)} detail={`${formatNumber(overview?.metrics.activeExperiments)} active`} />
        <ReplayMetric icon={<Layers3 size={18} />} label="Candidates" value={formatNumber(overview?.metrics.totalCandidates)} detail={`${formatNumber(overview?.metrics.improvedCandidates)} improved`} />
        <ReplayMetric icon={<Target size={18} />} label="Quality delta" value={formatSignedPercent(overview?.metrics.averageQualityDelta)} detail={`${formatNumber(overview?.metrics.regressionCandidates)} regressions`} trend={overview?.metrics.averageQualityDelta} />
        <ReplayMetric icon={<Clock3 size={18} />} label="Latency delta" value={formatSignedDuration(overview?.metrics.averageLatencyDeltaMs)} detail="Candidate minus baseline" inverse trend={overview?.metrics.averageLatencyDeltaMs} />
        <ReplayMetric icon={<CircleDollarSign size={18} />} label="Cost delta" value={formatSignedMoney(overview?.metrics.averageCostDelta, overview?.metrics.currency)} detail="Average per candidate" inverse trend={overview?.metrics.averageCostDelta} />
      </section>

      {showCreate && overview && (
        <section className="panel replay-create-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Controlled experiment</span><h3>Create from an immutable baseline</h3></div><RotateCcw size={18} /></div>
          {overview.recentSources.length === 0 ? (
            <div className="empty-state compact-empty"><MessageSquareText size={28} /><h3>No source runs available</h3><p>Execute a Conversation Simulator message first, then return to preserve it as a replay baseline.</p><Link className="primary-button" to="/conversations">Open Simulator</Link></div>
          ) : (
            <div className="replay-create-grid">
              <div className="replay-source-column">
                <label>Experiment name<input value={experimentName} onChange={event => setExperimentName(event.target.value)} /></label>
                <label>Baseline run<select value={selectedSource?.runId ?? ""} onChange={event => { const source = overview.recentSources.find(item => item.runId === event.target.value); setSourceRunId(event.target.value); setForm(createFormFromSnapshot(source?.snapshot, overview)); }}>{overview.recentSources.map(source => <option key={source.runId} value={source.runId}>{source.simulationTitle} · {formatDate(source.createdAt)} · {source.snapshot.verdict}</option>)}</select></label>
                {selectedSource && <SourcePreview source={selectedSource} />}
              </div>
              <ReplayConfigurationForm form={activeForm} onChange={setForm} overview={overview} />
              <div className="replay-create-actions">
                <button className="secondary-button" onClick={() => setShowCreate(false)}>Cancel</button>
                <button className="primary-button" onClick={submitCreate} disabled={!experimentName.trim() || !activeForm.label.trim() || createMutation.isPending}>
                  {createMutation.isPending ? <LoaderCircle className="spin" size={16} /> : <Sparkles size={16} />} Execute first candidate
                </button>
              </div>
            </div>
          )}
        </section>
      )}

      <section className="replay-workspace">
        <aside className="panel replay-experiment-list">
          <div className="panel-header"><div><span className="panel-eyebrow">Persisted experiments</span><h3>Replay history</h3></div><span className="result-count">{overview?.recentExperiments.length ?? 0}</span></div>
          {overviewQuery.isLoading ? <ReplayLoading /> : overview?.recentExperiments.length ? (
            <div className="replay-experiment-cards">
              {overview.recentExperiments.map(experiment => (
                <button key={experiment.id} className={experiment.id === effectiveExperimentId ? "selected" : ""} onClick={() => { setSelectedExperimentId(experiment.id); setSelectedCandidateId(""); setForm(null); }}>
                  <div><strong>{experiment.name}</strong><StatusPill status={experiment.status} /></div>
                  <span>{experiment.simulationTitle}</span>
                  <small>{experiment.candidateCount} candidate{experiment.candidateCount === 1 ? "" : "s"} · best {formatSignedPercent(experiment.bestQualityDelta)}</small>
                  <time>{formatDate(experiment.updatedAt)}</time>
                </button>
              ))}
            </div>
          ) : <div className="inspector-placeholder"><RotateCcw size={30} /><p>Create the first controlled replay experiment from a recorded simulator run.</p></div>}
        </aside>

        <main className="panel replay-detail-panel">
          {!effectiveExperimentId ? <div className="inspector-placeholder"><GitCompareArrows size={32} /><p>Select or create a replay experiment to inspect its immutable baseline and candidates.</p></div> : detailQuery.isLoading ? <ReplayLoading /> : detail ? (
            <ReplayExperimentInspector
              detail={detail}
              selectedCandidate={selectedCandidate}
              onSelectCandidate={setSelectedCandidateId}
              form={activeForm}
              onFormChange={setForm}
              overview={overview}
              onAddCandidate={submitCandidate}
              isAdding={candidateMutation.isPending}
              onComplete={() => completeMutation.mutate(detail.summary.id)}
              isCompleting={completeMutation.isPending}
              onArchive={() => archiveMutation.mutate(detail.summary.id)}
              isArchiving={archiveMutation.isPending}
            />
          ) : null}
        </main>
      </section>
    </div>
  );
}

function ReplayExperimentInspector({
  detail,
  selectedCandidate,
  onSelectCandidate,
  form,
  onFormChange,
  overview,
  onAddCandidate,
  isAdding,
  onComplete,
  isCompleting,
  onArchive,
  isArchiving,
}: {
  detail: ReplayExperimentDetail;
  selectedCandidate?: ReplayCandidate;
  onSelectCandidate: (id: string) => void;
  form: ReplayForm;
  onFormChange: (value: ReplayForm) => void;
  overview?: Awaited<ReturnType<typeof getReplayOverview>>;
  onAddCandidate: () => void;
  isAdding: boolean;
  onComplete: () => void;
  isCompleting: boolean;
  onArchive: () => void;
  isArchiving: boolean;
}) {
  return (
    <>
      <div className="replay-detail-heading">
        <div><span className="panel-eyebrow">Experiment workspace</span><h2>{detail.summary.name}</h2><p>{detail.summary.simulationTitle} · baseline {shortId(detail.summary.sourceRunId)}</p></div>
        <div className="replay-detail-actions">
          <StatusPill status={detail.summary.status} />
          {detail.summary.status === "Active" && <button className="secondary-button" onClick={onComplete} disabled={isCompleting}>{isCompleting ? <LoaderCircle className="spin" size={15} /> : <CheckCircle2 size={15} />} Complete</button>}
          {detail.summary.status === "Completed" && <button className="secondary-button" onClick={onArchive} disabled={isArchiving}>{isArchiving ? <LoaderCircle className="spin" size={15} /> : <Archive size={15} />} Archive</button>}
        </div>
      </div>

      <div className="replay-candidate-tabs">
        {detail.candidates.map(candidate => <button key={candidate.id} className={selectedCandidate?.id === candidate.id ? "active" : ""} onClick={() => onSelectCandidate(candidate.id)}><span>{candidate.label}</span><OutcomePill outcome={candidate.comparison.outcome} /></button>)}
      </div>

      {selectedCandidate ? (
        <>
          <section className="replay-comparison-grid">
            <RunComparisonCard title="Immutable baseline" badge="Baseline" snapshot={detail.baseline.snapshot} />
            <div className="replay-delta-column"><GitCompareArrows size={22} /><strong>{formatSignedPercent(selectedCandidate.comparison.qualityDelta)}</strong><span>quality delta</span><OutcomePill outcome={selectedCandidate.comparison.outcome} /></div>
            <RunComparisonCard title={selectedCandidate.label} badge="Candidate" snapshot={selectedCandidate.snapshot} />
          </section>

          <section className="replay-delta-grid">
            <DeltaCard label="Groundedness" value={selectedCandidate.comparison.groundednessDelta} format="percent" />
            <DeltaCard label="Relevance" value={selectedCandidate.comparison.relevanceDelta} format="percent" />
            <DeltaCard label="Safety" value={selectedCandidate.comparison.safetyDelta} format="percent" />
            <DeltaCard label="Latency" value={selectedCandidate.comparison.durationDeltaMs} format="duration" inverse />
            <DeltaCard label="Tokens" value={selectedCandidate.comparison.tokenDelta} format="number" inverse />
            <DeltaCard label="Cost" value={selectedCandidate.comparison.costDelta} format="money" currency={selectedCandidate.snapshot.currency} inverse />
          </section>

          <section className="replay-analysis-grid">
            <article className="replay-response-panel">
              <div className="panel-header"><div><span className="panel-eyebrow">Output comparison</span><h3>Assistant responses</h3></div><MessageSquareText size={18} /></div>
              <div className="replay-responses"><ResponseBlock label="Baseline" content={detail.baseline.snapshot.response} /><ResponseBlock label={selectedCandidate.label} content={selectedCandidate.snapshot.response} /></div>
            </article>
            <aside className="replay-findings-panel">
              <div className="panel-header"><div><span className="panel-eyebrow">Automated analysis</span><h3>Findings</h3></div><Target size={18} /></div>
              <div className="replay-change-tags">{selectedCandidate.comparison.changedDimensions.length ? selectedCandidate.comparison.changedDimensions.map(item => <span key={item}>{item}</span>) : <span>No configuration changes</span>}</div>
              <ul>{selectedCandidate.comparison.findings.map(item => <li key={item}>{item}</li>)}</ul>
              <div className="replay-context-links">
                <Link to={`/conversations?simulation=${detail.summary.simulationId}`}><MessageSquareText size={14} /> Simulator</Link>
                <Link to={`/evaluations?run=${selectedCandidate.runId}`}><Target size={14} /> Evaluation</Link>
                <Link to={`/traces?run=${selectedCandidate.runId}`}><Activity size={14} /> Trace</Link>
              </div>
            </aside>
          </section>
        </>
      ) : <div className="inspector-placeholder"><GitCompareArrows size={30} /><p>No replay candidate is available for this experiment.</p></div>}

      {detail.summary.status === "Active" && overview && (
        <section className="replay-add-candidate">
          <div className="panel-header"><div><span className="panel-eyebrow">Continue experiment</span><h3>Execute another candidate</h3></div><CopyPlus size={18} /></div>
          <ReplayConfigurationForm form={form} onChange={onFormChange} overview={overview} compact />
          <div className="replay-create-actions"><button className="primary-button" onClick={onAddCandidate} disabled={!form.label.trim() || isAdding}>{isAdding ? <LoaderCircle className="spin" size={16} /> : <RotateCcw size={16} />} Run candidate</button></div>
        </section>
      )}
    </>
  );
}

function ReplayConfigurationForm({ form, onChange, overview, compact = false }: { form: ReplayForm; onChange: (value: ReplayForm) => void; overview: Awaited<ReturnType<typeof getReplayOverview>>; compact?: boolean }) {
  const provider = overview.options.providers.find(item => item.key === form.provider);
  const field = <K extends keyof ReplayForm>(key: K, value: ReplayForm[K]) => onChange({ ...form, [key]: value });
  return <div className={`replay-configuration-form ${compact ? "compact" : ""}`}>
    <label>Candidate label<input value={form.label} onChange={event => field("label", event.target.value)} /></label>
    <label>Workflow<select value={form.workflow} onChange={event => field("workflow", event.target.value)}>{unique([form.workflow, ...overview.options.workflows]).map(item => <option key={item}>{item}</option>)}</select></label>
    <label>Prompt version<select value={form.promptVersion} onChange={event => field("promptVersion", event.target.value)}>{unique([form.promptVersion, ...overview.options.promptVersions]).map(item => <option key={item}>{item}</option>)}</select></label>
    <label>Knowledge collection<select value={form.knowledgeCollection} onChange={event => field("knowledgeCollection", event.target.value)}>{unique([form.knowledgeCollection, ...overview.options.knowledgeCollections]).map(item => <option key={item}>{item}</option>)}</select></label>
    <label>Provider<select value={form.provider} onChange={event => { const next = overview.options.providers.find(item => item.key === event.target.value); onChange({ ...form, provider: event.target.value, model: next?.defaultModel ?? form.model }); }}>{overview.options.providers.map(item => <option key={item.key} value={item.key} disabled={!item.isConfigured}>{item.displayName} · {item.status}</option>)}</select></label>
    <label>Model<input value={form.model} onChange={event => field("model", event.target.value)} placeholder={provider?.defaultModel ?? "Model"} /></label>
    <label>Recovery mode<select value={form.mode} onChange={event => field("mode", event.target.value as SimulationMode)}>{overview.options.modes.map(item => <option key={item}>{item}</option>)}</select></label>
    <label>Temperature<div className="replay-range-field"><input type="range" min="0" max="2" step="0.1" value={form.temperature} onChange={event => field("temperature", Number(event.target.value))} /><strong>{form.temperature.toFixed(1)}</strong></div></label>
    <label>Maximum output tokens<input type="number" min="32" max="8192" value={form.maxOutputTokens} onChange={event => field("maxOutputTokens", Number(event.target.value))} /></label>
  </div>;
}

function SourcePreview({ source }: { source: ReplaySource }) {
  return <article className="replay-source-preview"><div><span>Customer message</span><p>{source.userMessage}</p></div><div className="replay-source-facts"><span><GitBranch size={13} /> {source.snapshot.workflow}</span><span><Cpu size={13} /> {source.snapshot.provider}</span><span><Database size={13} /> {source.snapshot.knowledgeCollection}</span><span><Target size={13} /> {formatPercent(source.snapshot.qualityScore)}</span></div></article>;
}

function RunComparisonCard({ title, badge, snapshot }: { title: string; badge: string; snapshot: ReplayRunSnapshot }) {
  return <article className="replay-run-card"><div className="replay-run-card-header"><div><span>{badge}</span><h3>{title}</h3></div><StatusPill status={snapshot.status} /></div><div className="replay-score"><strong>{formatPercent(snapshot.qualityScore)}</strong><span>{snapshot.verdict}</span></div><dl><div><dt>Workflow</dt><dd>{snapshot.workflow}</dd></div><div><dt>Prompt</dt><dd>{snapshot.promptVersion}</dd></div><div><dt>Knowledge</dt><dd>{snapshot.knowledgeCollection}</dd></div><div><dt>Provider</dt><dd>{snapshot.provider}</dd></div><div><dt>Model</dt><dd>{snapshot.model}</dd></div><div><dt>Mode</dt><dd>{snapshot.mode}</dd></div><div><dt>Latency</dt><dd>{formatDuration(snapshot.durationMs)}</dd></div><div><dt>Tokens</dt><dd>{formatNumber(snapshot.totalTokens)}</dd></div><div><dt>Cost</dt><dd>{formatMoney(snapshot.actualCost, snapshot.currency)}</dd></div><div><dt>Citations</dt><dd>{snapshot.citationCount}</dd></div></dl></article>;
}

function DeltaCard({ label, value, format, currency = "ZAR", inverse = false }: { label: string; value: number; format: "percent" | "duration" | "number" | "money"; currency?: string; inverse?: boolean }) {
  const positive = inverse ? value < 0 : value > 0;
  const negative = inverse ? value > 0 : value < 0;
  const rendered = format === "percent" ? formatSignedPercent(value) : format === "duration" ? formatSignedDuration(value) : format === "money" ? formatSignedMoney(value, currency) : formatSignedNumber(value);
  return <article className={`replay-delta-card ${positive ? "positive" : negative ? "negative" : "neutral"}`}><span>{label}</span><strong>{positive ? <ArrowUpRight size={16} /> : negative ? <ArrowDownRight size={16} /> : <ArrowRight size={16} />}{rendered}</strong></article>;
}

function ResponseBlock({ label, content }: { label: string; content?: string | null }) {
  return <div><span>{label}</span><p>{content || "No response was produced for this run."}</p></div>;
}

function ReplayMetric({ icon, label, value, detail, trend, inverse = false }: { icon: React.ReactNode; label: string; value: string; detail: string; trend?: number; inverse?: boolean }) {
  const tone = trend === undefined || Math.abs(trend) < .0001 ? "neutral" : (inverse ? trend < 0 : trend > 0) ? "positive" : "negative";
  return <article className={`replay-metric-card ${tone}`}><span className="replay-metric-icon">{icon}</span><div><span>{label}</span><strong>{value}</strong><small>{detail}</small></div></article>;
}

function StatusPill({ status }: { status: string }) {
  return <span className={`runtime-badge ${status === "Completed" || status === "Active" ? "runtime-ready" : status === "Failed" ? "runtime-blocked" : ""}`}>{status}</span>;
}

function OutcomePill({ outcome }: { outcome: string }) {
  return <span className={`replay-outcome outcome-${outcome.toLowerCase()}`}>{outcome}</span>;
}

function ReplayLoading() {
  return <div className="inspector-placeholder"><LoaderCircle className="spin" size={28} /><p>Loading replay experiments…</p></div>;
}

function createFormFromSnapshot(snapshot?: ReplayRunSnapshot, overview?: Awaited<ReturnType<typeof getReplayOverview>>): ReplayForm {
  if (!snapshot) {
    const provider = overview?.options.providers[0];
    return { ...fallbackForm, provider: provider?.key ?? fallbackForm.provider, model: provider?.defaultModel ?? fallbackForm.model };
  }
  const provider = resolveProviderOption(snapshot.provider, overview);
  return {
    label: "Candidate A",
    workflow: snapshot.workflow,
    promptVersion: snapshot.promptVersion,
    knowledgeCollection: snapshot.knowledgeCollection,
    provider: provider?.key ?? overview?.options.providers.find(item => item.isConfigured)?.key ?? "Deterministic",
    model: snapshot.model || provider?.defaultModel || "",
    temperature: snapshot.temperature,
    maxOutputTokens: snapshot.maxOutputTokens,
    mode: snapshot.mode,
  };
}

function toExecutionInput(form: ReplayForm) {
  return {
    workflow: form.workflow,
    promptVersion: form.promptVersion,
    knowledgeCollection: form.knowledgeCollection,
    provider: form.provider,
    model: form.model || undefined,
    temperature: form.temperature,
    maxOutputTokens: form.maxOutputTokens,
    mode: form.mode,
  };
}

function resolveProviderOption(providerName: string, overview?: Awaited<ReturnType<typeof getReplayOverview>>) {
  const normalize = (value: string) => value.toLowerCase().replace(/[^a-z0-9]/g, "");
  const requested = normalize(providerName);
  return overview?.options.providers.find(item => {
    const key = normalize(item.key);
    const display = normalize(item.displayName);
    return requested === key || requested === display || requested.includes(key) || requested.includes(display);
  });
}

function unique(values: string[]) { return [...new Set(values.filter(Boolean))]; }
function shortId(value: string) { return value.slice(0, 8); }
function formatDate(value: string) { return new Date(value).toLocaleString("en-ZA"); }
function formatNumber(value?: number) { return new Intl.NumberFormat("en-ZA").format(value ?? 0); }
function formatPercent(value?: number) { return `${((value ?? 0) * 100).toFixed(1)}%`; }
function formatSignedPercent(value?: number) { const actual = value ?? 0; return `${actual > 0 ? "+" : ""}${(actual * 100).toFixed(1)}%`; }
function formatDuration(value?: number) { return `${(value ?? 0).toFixed(0)} ms`; }
function formatSignedDuration(value?: number) { const actual = value ?? 0; return `${actual > 0 ? "+" : ""}${actual.toFixed(0)} ms`; }
function formatSignedNumber(value?: number) { const actual = value ?? 0; return `${actual > 0 ? "+" : ""}${formatNumber(actual)}`; }
function formatMoney(value?: number, currency = "ZAR") { return new Intl.NumberFormat("en-ZA", { style: "currency", currency: currency || "ZAR", maximumFractionDigits: 6 }).format(value ?? 0); }
function formatSignedMoney(value?: number, currency = "ZAR") { const actual = value ?? 0; return `${actual > 0 ? "+" : actual < 0 ? "−" : ""}${formatMoney(Math.abs(actual), currency)}`; }
