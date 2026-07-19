import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import {
  Activity,
  BadgeCheck,
  BarChart3,
  CheckCircle2,
  CircleAlert,
  ClipboardCheck,
  BookOpen,
  Gauge,
  LoaderCircle,
  Plus,
  RefreshCw,
  ShieldCheck,
  SlidersHorizontal,
  Target,
  XCircle,
} from "lucide-react";
import { MetricCard } from "../components/MetricCard";
import { getApiErrorMessage } from "../services/apiClient";
import {
  createEvaluationScorecard,
  getEvaluationOverview,
  listEvaluationScorecards,
  previewEvaluation,
} from "../services/evaluationApi";
import type {
  CreateEvaluationScorecardRequest,
  EvaluationPreviewRequest,
  EvaluationRun,
} from "../types/evaluation";

const defaultPreview: EvaluationPreviewRequest = {
  groundedness: 0.88,
  relevance: 0.86,
  safety: 0.98,
};

const defaultScorecard: CreateEvaluationScorecardRequest = {
  name: "",
  description: "",
  minimumGroundedness: 0.8,
  minimumRelevance: 0.8,
  minimumSafety: 0.95,
  minimumOverallScore: 0.82,
  failureAction: "Review",
};

const percentage = (value: number) => `${Math.round(value * 100)}%`;

export function EvaluationStudioPage() {
  const queryClient = useQueryClient();
  const overviewQuery = useQuery({
    queryKey: ["evaluation-overview"],
    queryFn: getEvaluationOverview,
    refetchInterval: 30_000,
  });
  const [selectedRun, setSelectedRun] = useState<EvaluationRun | null>(null);
  const [previewRequest, setPreviewRequest] = useState<EvaluationPreviewRequest>(defaultPreview);
  const [selectedScorecardId, setSelectedScorecardId] = useState<string | null>(null);
  const [showScorecardForm, setShowScorecardForm] = useState(false);
  const [scorecardDraft, setScorecardDraft] = useState<CreateEvaluationScorecardRequest>(defaultScorecard);
  const previewMutation = useMutation({ mutationFn: previewEvaluation });
  const scorecardsQuery = useQuery({
    queryKey: ["evaluation-scorecards"],
    queryFn: listEvaluationScorecards,
  });
  const createScorecardMutation = useMutation({
    mutationFn: createEvaluationScorecard,
    onSuccess: async scorecard => {
      setSelectedScorecardId(scorecard.id);
      setShowScorecardForm(false);
      setScorecardDraft(defaultScorecard);
      await queryClient.invalidateQueries({ queryKey: ["evaluation-scorecards"] });
    },
  });
  const overview = overviewQuery.data;
  const maxDailyRuns = useMemo(
    () => Math.max(1, ...(overview?.dailyTrend.map(item => item.runs) ?? [1])),
    [overview],
  );

  if (overviewQuery.isLoading) {
    return <div className="page-loading"><LoaderCircle className="spin" /> Loading Evaluation Studio</div>;
  }

  if (overviewQuery.isError || !overview) {
    return (
      <section className="panel evaluation-error-state">
        <CircleAlert size={28} />
        <h2>Evaluation Studio is unavailable</h2>
        <p>{getApiErrorMessage(overviewQuery.error)}</p>
        <button className="primary-button" onClick={() => void overviewQuery.refetch()}>
          <RefreshCw size={15} /> Retry
        </button>
      </section>
    );
  }

  return (
    <div className="page-stack evaluation-studio">
      <section className="page-heading">
        <div className="page-heading-icon"><ClipboardCheck size={25} /></div>
        <div className="page-heading-copy">
          <div className="page-heading-meta">
            <span>Evaluation Engine</span>
            <span className="source-chip"><Activity size={13} /> Persisted quality telemetry</span>
          </div>
          <h2>Evaluation Studio</h2>
          <p>Measure groundedness, relevance, safety, and overall response quality. Inspect failed gates before a conversational AI change reaches production.</p>
        </div>
        <div className="page-heading-actions">
          <Link className="secondary-button" to="/documentation/evaluation"><BookOpen size={15} /> Documentation</Link>
          <button className="secondary-button" onClick={() => void overviewQuery.refetch()} disabled={overviewQuery.isFetching}>
            <RefreshCw className={overviewQuery.isFetching ? "spin" : ""} size={15} /> Refresh
          </button>
          <button className="primary-button" onClick={() => setShowScorecardForm(value => !value)}>
            <Plus size={15} /> New scorecard
          </button>
        </div>
      </section>

      <section className="metrics-grid evaluation-metrics">
        <MetricCard label="Evaluated runs" value={overview.evaluatedRuns.toLocaleString()} detail={`${overview.passingRuns} passed · ${overview.failingRuns} require review`} icon={ClipboardCheck} tone="accent" />
        <MetricCard label="Pass rate" value={percentage(overview.passRate)} detail={`Failure action: ${overview.policy.failureAction}`} icon={BadgeCheck} tone={overview.passRate >= .8 ? "positive" : "warning"} />
        <MetricCard label="Average quality" value={percentage(overview.averageOverallScore)} detail={`Gate: ${percentage(overview.policy.minimumOverallScore)}`} icon={Gauge} />
        <MetricCard label="Safety gate" value={percentage(overview.policy.minimumSafety)} detail="Minimum permitted safety score" icon={ShieldCheck} tone="positive" />
      </section>

      <section className="evaluation-overview-grid">
        <article className="panel evaluation-metrics-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Quality dimensions</span><h3>Metric performance</h3></div><Target size={20} /></div>
          <div className="evaluation-metric-list">
            {overview.metrics.map(metric => (
              <div key={metric.name} className="evaluation-metric-row">
                <div className="evaluation-metric-title"><strong>{metric.name}</strong><span>{metric.passing} passed · {metric.failing} failed</span></div>
                <div className="evaluation-progress"><span style={{ width: `${Math.max(2, metric.average * 100)}%` }} /><i style={{ left: `${metric.threshold * 100}%` }} /></div>
                <div className="evaluation-metric-values"><span>Avg {percentage(metric.average)}</span><span>Gate {percentage(metric.threshold)}</span><span>Range {percentage(metric.minimum)}–{percentage(metric.maximum)}</span></div>
              </div>
            ))}
          </div>
        </article>

        <aside className="panel evaluation-trend-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Last seven days</span><h3>Quality trend</h3></div><BarChart3 size={20} /></div>
          <div className="evaluation-trend-bars">
            {overview.dailyTrend.map(item => (
              <div key={item.date} className="evaluation-trend-item" title={`${item.runs} runs · ${percentage(item.averageScore)} average`}>
                <div className="evaluation-trend-track"><span style={{ height: `${Math.max(4, item.runs / maxDailyRuns * 100)}%` }} /></div>
                <strong>{percentage(item.passRate)}</strong>
                <small>{new Date(`${item.date}T00:00:00`).toLocaleDateString(undefined, { weekday: "short" })}</small>
              </div>
            ))}
          </div>
        </aside>
      </section>

      <section className="evaluation-main-grid">
        <article className="panel evaluation-runs-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Persisted simulator runs</span><h3>Recent evaluations</h3></div><span className="source-chip">{overview.recentRuns.length} recent</span></div>
          {overview.recentRuns.length === 0 ? (
            <div className="empty-state compact-empty"><ClipboardCheck size={28} /><h3>No evaluations yet</h3><p>Run a conversation simulation to generate groundedness, relevance, and safety telemetry.</p></div>
          ) : (
            <div className="execution-table-wrap">
              <table className="execution-table evaluation-table">
                <thead><tr><th>Simulation</th><th>Provider</th><th>Grounded</th><th>Relevant</th><th>Safe</th><th>Overall</th><th>Verdict</th></tr></thead>
                <tbody>
                  {overview.recentRuns.map(run => (
                    <tr key={run.runId} onClick={() => setSelectedRun(run)} className={selectedRun?.runId === run.runId ? "selected" : ""}>
                      <td><strong>{run.simulationTitle}</strong><span>{new Date(run.createdAt).toLocaleString()}</span></td>
                      <td><strong>{run.provider}</strong><span>{run.model}</span></td>
                      <td>{percentage(run.groundedness)}</td>
                      <td>{percentage(run.relevance)}</td>
                      <td>{percentage(run.safety)}</td>
                      <td>{percentage(run.overallScore)}</td>
                      <td><span className={`runtime-badge ${run.passed ? "runtime-ready" : "runtime-blocked"}`}>{run.verdict}</span></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </article>

        <aside className="panel evaluation-inspector-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Run inspector</span><h3>Gate decisions</h3></div>{selectedRun ? (selectedRun.passed ? <CheckCircle2 size={20} /> : <XCircle size={20} />) : <Target size={20} />}</div>
          {selectedRun ? (
            <div className="evaluation-inspector-content">
              <div className={`evaluation-verdict ${selectedRun.passed ? "evaluation-pass" : "evaluation-fail"}`}>
                {selectedRun.passed ? <CheckCircle2 size={18} /> : <CircleAlert size={18} />}
                <div><strong>{selectedRun.verdict}</strong><span>{selectedRun.passed ? "Every quality gate passed." : `Failed: ${selectedRun.failedGates.join(", ")}`}</span></div>
              </div>
              <QualityGate label="Groundedness" score={selectedRun.groundedness} threshold={overview.policy.minimumGroundedness} />
              <QualityGate label="Relevance" score={selectedRun.relevance} threshold={overview.policy.minimumRelevance} />
              <QualityGate label="Safety" score={selectedRun.safety} threshold={overview.policy.minimumSafety} />
              <QualityGate label="Overall" score={selectedRun.overallScore} threshold={overview.policy.minimumOverallScore} />
              <a className="secondary-button execution-link" href={`/conversations?simulation=${selectedRun.simulationId}&run=${selectedRun.runId}`}>Open simulator run</a>
            </div>
          ) : <div className="inspector-placeholder"><Target size={28} /><p>Select a run to inspect its quality gates and failure reasons.</p></div>}
        </aside>
      </section>

      <section className="panel evaluation-scorecards-panel">
        <div className="panel-header">
          <div><span className="panel-eyebrow">Reusable evaluation policy</span><h3>Scorecards</h3></div>
          <span className="source-chip">{scorecardsQuery.data?.length ?? 0} saved</span>
        </div>
        {showScorecardForm && (
          <form className="evaluation-scorecard-form" onSubmit={event => {
            event.preventDefault();
            createScorecardMutation.mutate(scorecardDraft);
          }}>
            <div className="evaluation-scorecard-fields">
              <label><span>Name</span><input required maxLength={120} value={scorecardDraft.name} onChange={event => setScorecardDraft(current => ({ ...current, name: event.target.value }))} placeholder="Production quality gate" /></label>
              <label><span>Failure action</span><input required maxLength={80} value={scorecardDraft.failureAction} onChange={event => setScorecardDraft(current => ({ ...current, failureAction: event.target.value }))} placeholder="Review" /></label>
              <label className="evaluation-scorecard-description"><span>Description</span><textarea maxLength={500} value={scorecardDraft.description} onChange={event => setScorecardDraft(current => ({ ...current, description: event.target.value }))} placeholder="When and where this scorecard should be used" /></label>
            </div>
            <div className="evaluation-scorecard-thresholds">
              {(["minimumGroundedness", "minimumRelevance", "minimumSafety", "minimumOverallScore"] as const).map(key => (
                <label key={key}><span>{key.replace("minimum", "")}</span><input type="range" min="0" max="1" step="0.01" value={scorecardDraft[key]} onChange={event => setScorecardDraft(current => ({ ...current, [key]: Number(event.target.value) }))} /><strong>{percentage(scorecardDraft[key])}</strong></label>
              ))}
            </div>
            {createScorecardMutation.isError && <p className="form-error">{getApiErrorMessage(createScorecardMutation.error)}</p>}
            <div className="evaluation-scorecard-form-actions">
              <button type="button" className="secondary-button" onClick={() => setShowScorecardForm(false)}>Cancel</button>
              <button type="submit" className="primary-button" disabled={createScorecardMutation.isPending}>
                {createScorecardMutation.isPending ? <LoaderCircle className="spin" size={15} /> : <Plus size={15} />} Save scorecard
              </button>
            </div>
          </form>
        )}
        {scorecardsQuery.isError ? (
          <div className="evaluation-inline-error"><CircleAlert size={18} /><span>{getApiErrorMessage(scorecardsQuery.error)}</span><button onClick={() => void scorecardsQuery.refetch()}>Retry</button></div>
        ) : scorecardsQuery.data?.length ? (
          <div className="evaluation-scorecard-grid">
            <button className={`evaluation-scorecard-card ${selectedScorecardId === null ? "selected" : ""}`} onClick={() => setSelectedScorecardId(null)}>
              <span className="scorecard-card-heading"><strong>Environment default</strong><i>Configured</i></span>
              <p>Uses the deployed Evaluation settings.</p>
              <span>{percentage(overview.policy.minimumGroundedness)} grounded · {percentage(overview.policy.minimumSafety)} safe · {percentage(overview.policy.minimumOverallScore)} overall</span>
            </button>
            {scorecardsQuery.data.map(scorecard => (
              <button key={scorecard.id} className={`evaluation-scorecard-card ${selectedScorecardId === scorecard.id ? "selected" : ""}`} onClick={() => setSelectedScorecardId(scorecard.id)}>
                <span className="scorecard-card-heading"><strong>{scorecard.name}</strong><i>Saved</i></span>
                <p>{scorecard.description || `Failure action: ${scorecard.failureAction}`}</p>
                <span>{percentage(scorecard.minimumGroundedness)} grounded · {percentage(scorecard.minimumSafety)} safe · {percentage(scorecard.minimumOverallScore)} overall</span>
              </button>
            ))}
          </div>
        ) : !showScorecardForm ? (
          <div className="empty-state compact-empty"><ClipboardCheck size={28} /><h3>No saved scorecards</h3><p>Create a reusable set of quality gates, then select it in the policy sandbox.</p><button className="primary-button" onClick={() => setShowScorecardForm(true)}><Plus size={15} /> Create scorecard</button></div>
        ) : null}
      </section>

      <section className="panel evaluation-preview-panel">
        <div className="panel-header"><div><span className="panel-eyebrow">Policy sandbox</span><h3>Quality-gate preview</h3></div><SlidersHorizontal size={20} /></div>
        <div className="evaluation-preview-layout">
          <div className="evaluation-preview-form">
            {(["groundedness", "relevance", "safety"] as const).map(key => (
              <label key={key}><span>{key}</span><input type="range" min="0" max="1" step="0.01" value={previewRequest[key]} onChange={event => setPreviewRequest(current => ({ ...current, [key]: Number(event.target.value) }))} /><strong>{percentage(previewRequest[key])}</strong></label>
            ))}
            <div className="evaluation-preview-profile"><span>Scorecard</span><strong>{scorecardsQuery.data?.find(item => item.id === selectedScorecardId)?.name ?? "Environment default"}</strong></div>
            <button className="primary-button" onClick={() => previewMutation.mutate({ ...previewRequest, scorecardId: selectedScorecardId ?? undefined })} disabled={previewMutation.isPending}>
              {previewMutation.isPending ? <LoaderCircle className="spin" size={15} /> : <Target size={15} />} Evaluate sample
            </button>
          </div>
          <div className="evaluation-preview-result">
            {previewMutation.data ? (
              <>
                <div className={`evaluation-verdict ${previewMutation.data.passed ? "evaluation-pass" : "evaluation-fail"}`}>
                  {previewMutation.data.passed ? <CheckCircle2 size={18} /> : <XCircle size={18} />}
                  <div><strong>{previewMutation.data.verdict}</strong><span>Overall score {percentage(previewMutation.data.overallScore)}</span></div>
                </div>
                <div className="evaluation-decision-list">{previewMutation.data.decisions.map(decision => <QualityGate key={decision.name} label={decision.name} score={decision.score} threshold={decision.threshold} />)}</div>
              </>
            ) : <div className="inspector-placeholder"><SlidersHorizontal size={27} /><p>Adjust sample scores to see how the current evaluation policy behaves.</p></div>}
          </div>
        </div>
      </section>
    </div>
  );
}

function QualityGate({ label, score, threshold }: { label: string; score: number; threshold: number }) {
  const passed = score >= threshold;
  return (
    <div className="quality-gate-row">
      <span>{passed ? <CheckCircle2 size={15} /> : <XCircle size={15} />}{label}</span>
      <div><i style={{ width: `${score * 100}%` }} /></div>
      <strong>{percentage(score)}</strong>
      <small>Gate {percentage(threshold)}</small>
    </div>
  );
}
