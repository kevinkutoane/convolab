import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useSearchParams } from "react-router-dom";
import {
  AlertTriangle,
  BarChart3,
  BookOpenCheck,
  CheckCircle2,
  ClipboardCheck,
  FlaskConical,
  GitCompareArrows,
  LoaderCircle,
  Plus,
  RefreshCcw,
  ShieldCheck,
  Sparkles,
  XCircle,
} from "lucide-react";
import { MetricCard } from "../components/MetricCard";
import { StatusPill } from "../components/StatusPill";
import { getApiErrorMessage } from "../services/apiClient";
import {
  compareEvaluationRuns,
  createEvaluationScorecard,
  createEvaluationTestCase,
  getEvaluationOverview,
  publishEvaluationScorecard,
  reviewEvaluationRun,
  runEvaluationBatch,
} from "../services/evaluationApi";
import type {
  CreateEvaluationScorecardRequest,
  EvaluationRun,
  EvaluationScorecard,
} from "../types/evaluation";
import "../App.css";
import "../functional-workspaces.css";

const defaultScorecard: CreateEvaluationScorecardRequest = {
  name: "Customer Support Quality",
  description: "Quality gate for grounded, relevant, safe and complete customer support responses.",
  version: "1.0",
  qualityGateThreshold: 0.85,
};

export function EvaluationStudioPage() {
  const queryClient = useQueryClient();
  const [searchParams] = useSearchParams();
  const [selectedRunId, setSelectedRunId] = useState<string | null>(() => searchParams.get("run"));
  const [showCreateScorecard, setShowCreateScorecard] = useState(false);
  const [scorecardRequest, setScorecardRequest] = useState(defaultScorecard);
  const [testCaseName, setTestCaseName] = useState("Regression case");
  const [expectedVerdict, setExpectedVerdict] = useState("Passed");
  const [baselineId, setBaselineId] = useState("");
  const [candidateId, setCandidateId] = useState("");

  const overviewQuery = useQuery({
    queryKey: ["evaluation-overview"],
    queryFn: getEvaluationOverview,
    retry: 1,
  });

  const overview = overviewQuery.data;
  const runs = useMemo(() => overview?.recentRuns ?? [], [overview?.recentRuns]);
  const selectedRun = useMemo(
    () => runs.find(run => run.id === selectedRunId) ?? runs[0] ?? null,
    [runs, selectedRunId],
  );
  const publishedScorecard = overview?.scorecards.find(item => item.status === "Published") ?? overview?.scorecards[0];

  const refresh = () => queryClient.invalidateQueries({ queryKey: ["evaluation-overview"] });

  const createScorecardMutation = useMutation({
    mutationFn: createEvaluationScorecard,
    onSuccess: () => {
      setShowCreateScorecard(false);
      setScorecardRequest(defaultScorecard);
      void refresh();
    },
  });

  const publishMutation = useMutation({
    mutationFn: ({ id, revision }: { id: string; revision: number }) => publishEvaluationScorecard(id, revision),
    onSuccess: () => void refresh(),
  });

  const reviewMutation = useMutation({
    mutationFn: ({ id, status }: { id: string; status: string }) => reviewEvaluationRun(id, status, "ConvoLab Studio reviewer"),
    onSuccess: () => void refresh(),
  });

  const testCaseMutation = useMutation({
    mutationFn: async () => {
      if (!selectedRun) throw new Error("Select an evaluation run first.");
      return createEvaluationTestCase({
        name: testCaseName,
        description: `Regression protection for ${selectedRun.simulationTitle}.`,
        simulationId: selectedRun.simulationId,
        sourceRunId: selectedRun.sourceRunId,
        scorecardId: selectedRun.scorecardId,
        expectedVerdict,
        tags: ["studio", "regression"],
      });
    },
    onSuccess: () => void refresh(),
  });

  const batchMutation = useMutation({
    mutationFn: async () => {
      if (!publishedScorecard) throw new Error("Publish a scorecard before running a suite.");
      const testCaseIds = overview?.testCases.map(item => item.id) ?? [];
      if (testCaseIds.length === 0) throw new Error("Create at least one test case before running a suite.");
      return runEvaluationBatch({
        name: `Evaluation suite ${new Date().toLocaleDateString("en-ZA")}`,
        scorecardId: publishedScorecard.id,
        testCaseIds,
      });
    },
    onSuccess: () => void refresh(),
  });

  const comparisonMutation = useMutation({
    mutationFn: () => compareEvaluationRuns(baselineId, candidateId),
  });

  if (overviewQuery.isLoading) {
    return <div className="page-loading"><LoaderCircle className="spin" size={28} /> Loading Evaluation Studio…</div>;
  }

  if (overviewQuery.isError || !overview) {
    return (
      <section className="panel api-offline-panel">
        <div className="empty-state-icon"><AlertTriangle size={22} /></div>
        <div><span className="panel-eyebrow">Evaluation API unavailable</span><h3>Evaluation Studio could not load</h3><p>{getApiErrorMessage(overviewQuery.error)}</p></div>
        <button className="secondary-button" onClick={() => void overviewQuery.refetch()}><RefreshCcw size={15} /> Retry</button>
      </section>
    );
  }

  return (
    <div className="page-stack evaluation-page">
      <section className="page-heading evaluation-heading">
        <div className="page-heading-icon"><BookOpenCheck size={24} /></div>
        <div className="page-heading-copy">
          <div className="page-heading-meta"><span>Functional vertical slice</span><StatusPill status="stable" /></div>
          <h2>Evaluation Studio</h2>
          <p>Govern quality with versioned scorecards, persisted run evaluations, regression suites, human review and side-by-side comparison.</p>
        </div>
        <div className="evaluation-heading-actions">
          <Link className="secondary-button" to="/documentation/evaluation"><BookOpenCheck size={15} /> Documentation</Link>
          <button className="secondary-button" onClick={() => void overviewQuery.refetch()}><RefreshCcw size={15} /> Sync runs</button>
          <button className="primary-button" onClick={() => setShowCreateScorecard(value => !value)}><Plus size={16} /> New scorecard</button>
        </div>
      </section>

      <section className="metrics-grid evaluation-metrics">
        <MetricCard label="Evaluated runs" value={String(overview.metrics.totalRuns)} detail={`${overview.metrics.passedRuns} passed · ${overview.metrics.reviewRuns} review`} icon={ClipboardCheck} tone="accent" />
        <MetricCard label="Pass rate" value={formatPercent(overview.metrics.passRate)} detail={`${overview.metrics.failedRuns} failed quality gates`} icon={CheckCircle2} />
        <MetricCard label="Average quality" value={formatPercent(overview.metrics.averageScore)} detail={`${overview.metrics.publishedScorecards} published scorecard(s)`} icon={BarChart3} />
        <MetricCard label="Regression suite" value={String(overview.metrics.testCases)} detail={`${overview.metrics.regressionCount} mismatched expectation(s)`} icon={FlaskConical} />
      </section>

      {showCreateScorecard && (
        <section className="panel evaluation-create-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Governed quality profile</span><h3>Create scorecard</h3></div></div>
          <div className="evaluation-form-grid">
            <label><span>Name</span><input value={scorecardRequest.name} onChange={event => setScorecardRequest(current => ({ ...current, name: event.target.value }))} /></label>
            <label><span>Version</span><input value={scorecardRequest.version} onChange={event => setScorecardRequest(current => ({ ...current, version: event.target.value }))} /></label>
            <label><span>Quality gate</span><input type="number" min="0" max="1" step="0.01" value={scorecardRequest.qualityGateThreshold} onChange={event => setScorecardRequest(current => ({ ...current, qualityGateThreshold: Number(event.target.value) }))} /></label>
            <label className="evaluation-description-field"><span>Description</span><textarea value={scorecardRequest.description} onChange={event => setScorecardRequest(current => ({ ...current, description: event.target.value }))} /></label>
          </div>
          <div className="evaluation-form-footer">
            <p>New scorecards start with ConvoLab’s groundedness, relevance, safety and completeness metrics. Publish to use them for evaluation.</p>
            <button className="primary-button" disabled={createScorecardMutation.isPending} onClick={() => createScorecardMutation.mutate(scorecardRequest)}>
              {createScorecardMutation.isPending ? <LoaderCircle className="spin" size={15} /> : <Plus size={15} />} Create draft
            </button>
          </div>
          {createScorecardMutation.isError && <div className="provider-warning"><XCircle size={15} /> {getApiErrorMessage(createScorecardMutation.error)}</div>}
        </section>
      )}

      <section className="evaluation-top-grid">
        <article className="panel evaluation-trend-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Seven-day signal</span><h3>Quality trend</h3></div><span className="evaluation-generated">Synced {new Date(overview.generatedAt).toLocaleTimeString("en-ZA")}</span></div>
          <div className="quality-trend-chart">
            {overview.qualityTrend.map(day => (
              <div key={day.date} className="quality-trend-column">
                <div className="quality-trend-track"><span style={{ height: `${Math.max(day.averageScore * 100, day.runs > 0 ? 4 : 0)}%` }} /></div>
                <strong>{day.runs ? formatPercent(day.averageScore) : "—"}</strong>
                <small>{formatDay(day.date)}</small>
              </div>
            ))}
          </div>
        </article>

        <article className="panel scorecard-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Versioned policy</span><h3>Scorecards</h3></div><ShieldCheck size={18} /></div>
          <div className="scorecard-list">
            {overview.scorecards.map(scorecard => <ScorecardRow key={scorecard.id} scorecard={scorecard} onPublish={() => publishMutation.mutate({ id: scorecard.id, revision: scorecard.revision })} publishing={publishMutation.isPending} />)}
          </div>
        </article>
      </section>

      <section className="evaluation-workspace-grid">
        <article className="panel evaluation-runs-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Persisted execution quality</span><h3>Evaluation runs</h3></div><span>{runs.length} recent</span></div>
          {runs.length === 0 ? (
            <div className="inspector-placeholder"><Sparkles size={24} /><p>Run a conversation simulation. Its evaluation will be persisted here automatically.</p></div>
          ) : (
            <div className="evaluation-run-table-wrap">
              <table className="evaluation-run-table">
                <thead><tr><th>Simulation</th><th>Scorecard</th><th>Quality</th><th>Verdict</th><th>Review</th></tr></thead>
                <tbody>{runs.map(run => (
                  <tr
                    key={run.id}
                    className={selectedRun?.id === run.id ? "selected" : ""}
                    role="button"
                    tabIndex={0}
                    aria-pressed={selectedRun?.id === run.id}
                    onClick={() => setSelectedRunId(run.id)}
                    onKeyDown={event => {
                      if (event.key === "Enter" || event.key === " ") {
                        event.preventDefault();
                        setSelectedRunId(run.id);
                      }
                    }}
                  >
                    <td><strong>{run.simulationTitle}</strong><span>{new Date(run.createdAt).toLocaleString("en-ZA")}</span></td>
                    <td>{run.scorecardName}<span>v{run.scorecardVersion}</span></td>
                    <td><strong>{formatPercent(run.overallScore)}</strong></td>
                    <td><span className={`evaluation-verdict verdict-${run.verdict.toLowerCase()}`}>{run.verdict}</span></td>
                    <td>{run.reviewStatus}</td>
                  </tr>
                ))}</tbody>
              </table>
            </div>
          )}
        </article>

        <aside className="panel evaluation-inspector">
          <div className="panel-header"><div><span className="panel-eyebrow">Quality gate inspector</span><h3>{selectedRun?.simulationTitle ?? "Select a run"}</h3></div></div>
          {selectedRun ? <RunInspector run={selectedRun} onReview={status => reviewMutation.mutate({ id: selectedRun.id, status })} reviewing={reviewMutation.isPending} /> : <div className="inspector-placeholder"><ClipboardCheck size={24} /><p>Select an evaluation run to inspect metric thresholds and review status.</p></div>}
        </aside>
      </section>

      <section className="evaluation-bottom-grid">
        <article className="panel test-suite-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Repeatable regression protection</span><h3>Test cases and batch suites</h3></div><FlaskConical size={18} /></div>
          <div className="test-case-create-row">
            <input value={testCaseName} onChange={event => setTestCaseName(event.target.value)} placeholder="Test case name" />
            <select value={expectedVerdict} onChange={event => setExpectedVerdict(event.target.value)}><option>Passed</option><option>Review</option><option>Failed</option></select>
            <button className="secondary-button" disabled={!selectedRun || testCaseMutation.isPending} onClick={() => testCaseMutation.mutate()}><Plus size={14} /> Add selected run</button>
          </div>
          <div className="test-case-list">
            {overview.testCases.length === 0 ? <p className="muted-copy">No regression cases yet. Select a run and preserve its expected verdict.</p> : overview.testCases.map(testCase => (
              <div key={testCase.id}><span><strong>{testCase.name}</strong><small>{testCase.description}</small></span><span className="evaluation-verdict">Expect {testCase.expectedVerdict}</span></div>
            ))}
          </div>
          <button className="primary-button suite-run-button" disabled={!overview.testCases.length || batchMutation.isPending} onClick={() => batchMutation.mutate()}>
            {batchMutation.isPending ? <LoaderCircle className="spin" size={15} /> : <FlaskConical size={15} />} Run all test cases
          </button>
          {batchMutation.data && <div className={`suite-result ${batchMutation.data.passRate === 1 ? "suite-passed" : "suite-failed"}`}><strong>{formatPercent(batchMutation.data.passRate)} suite pass rate</strong><span>{batchMutation.data.passedCases} of {batchMutation.data.totalCases} expectations matched.</span></div>}
          {(testCaseMutation.isError || batchMutation.isError) && <div className="provider-warning"><XCircle size={15} /> {getApiErrorMessage(testCaseMutation.error ?? batchMutation.error)}</div>}
        </article>

        <article className="panel comparison-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Replay-ready analysis</span><h3>Compare evaluation runs</h3></div><GitCompareArrows size={18} /></div>
          <div className="comparison-selectors">
            <label><span>Baseline</span><select value={baselineId} onChange={event => setBaselineId(event.target.value)}><option value="">Select baseline</option>{runs.map(run => <option key={run.id} value={run.id}>{run.simulationTitle} · {formatPercent(run.overallScore)}</option>)}</select></label>
            <label><span>Candidate</span><select value={candidateId} onChange={event => setCandidateId(event.target.value)}><option value="">Select candidate</option>{runs.map(run => <option key={run.id} value={run.id}>{run.simulationTitle} · {formatPercent(run.overallScore)}</option>)}</select></label>
            <button className="secondary-button" disabled={!baselineId || !candidateId || baselineId === candidateId || comparisonMutation.isPending} onClick={() => comparisonMutation.mutate()}><GitCompareArrows size={15} /> Compare</button>
          </div>
          {comparisonMutation.data ? (
            <div className="comparison-result">
              <div className={`comparison-outcome outcome-${comparisonMutation.data.outcome.toLowerCase()}`}><strong>{comparisonMutation.data.outcome}</strong><span>{formatSignedPercent(comparisonMutation.data.overallDelta)} overall quality</span></div>
              {comparisonMutation.data.metrics.map(metric => (
                <div key={metric.key} className="comparison-metric"><span>{metric.displayName}</span><strong>{formatPercent(metric.baselineScore)} → {formatPercent(metric.candidateScore)}</strong><small className={metric.delta < 0 ? "negative-delta" : "positive-delta"}>{formatSignedPercent(metric.delta)}</small></div>
              ))}
              <div className="comparison-findings">{comparisonMutation.data.findings.map(finding => <p key={finding}>{finding}</p>)}</div>
            </div>
          ) : <div className="inspector-placeholder"><GitCompareArrows size={24} /><p>Compare two persisted evaluations to expose quality improvement or regression.</p></div>}
          {comparisonMutation.isError && <div className="provider-warning"><XCircle size={15} /> {getApiErrorMessage(comparisonMutation.error)}</div>}
        </article>
      </section>
    </div>
  );
}

function ScorecardRow({ scorecard, onPublish, publishing }: { scorecard: EvaluationScorecard; onPublish: () => void; publishing: boolean }) {
  return (
    <div className="scorecard-row">
      <div className="scorecard-row-heading"><span><strong>{scorecard.name}</strong><small>v{scorecard.version} · gate {formatPercent(scorecard.qualityGateThreshold)}</small></span><span className={`evaluation-verdict ${scorecard.status === "Published" ? "verdict-passed" : "verdict-review"}`}>{scorecard.status}</span></div>
      <p>{scorecard.description}</p>
      <div className="scorecard-metrics">{scorecard.metrics.map(metric => <span key={metric.id}>{metric.displayName} ≥ {formatPercent(metric.threshold)}</span>)}</div>
      {scorecard.status !== "Published" && <button className="secondary-button" disabled={publishing} onClick={onPublish}><CheckCircle2 size={14} /> Publish</button>}
    </div>
  );
}

function RunInspector({ run, onReview, reviewing }: { run: EvaluationRun; onReview: (status: string) => void; reviewing: boolean }) {
  return (
    <div className="evaluation-inspector-content">
      <div className="evaluation-score-hero"><div><span>Overall quality</span><strong>{formatPercent(run.overallScore)}</strong></div><span className={`evaluation-verdict verdict-${run.verdict.toLowerCase()}`}>{run.verdict}</span></div>
      <div className="evaluation-metric-list">
        {run.metrics.map(metric => (
          <div key={metric.id} className="evaluation-metric-row">
            <div><span>{metric.displayName}</span><small>{metric.detail}</small></div>
            <progress max="1" value={metric.score} />
            <strong className={metric.passed ? "metric-passed" : "metric-failed"}>{formatPercent(metric.score)}</strong>
          </div>
        ))}
      </div>
      <div className="inspector-section-title">Human review</div>
      <div className="review-status-row"><span>Status</span><strong>{run.reviewStatus}</strong></div>
      {run.reviewer && <div className="review-status-row"><span>Reviewer</span><strong>{run.reviewer}</strong></div>}
      <div className="review-actions">
        <button className="secondary-button" disabled={reviewing} onClick={() => onReview("Approved")}><CheckCircle2 size={14} /> Approve</button>
        <button className="secondary-button" disabled={reviewing} onClick={() => onReview("NeedsChanges")}><AlertTriangle size={14} /> Needs changes</button>
      </div>
      <a className="secondary-button execution-link" href={`/conversations?simulation=${run.simulationId}&run=${run.sourceRunId}`}>Open source simulation</a>
    </div>
  );
}

function formatPercent(value: number): string { return `${Math.round(value * 100)}%`; }
function formatSignedPercent(value: number): string { return `${value >= 0 ? "+" : ""}${(value * 100).toFixed(1)}%`; }
function formatDay(value: string): string { return new Date(`${value}T00:00:00`).toLocaleDateString("en-ZA", { weekday: "short" }); }
