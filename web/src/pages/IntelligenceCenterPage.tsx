import { useMemo, useState } from "react";
import type { CSSProperties } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import {
  Activity,
  BadgeDollarSign,
  BrainCircuit,
  CheckCircle2,
  CircleAlert,
  Clock3,
  Cpu,
  Gauge,
  LoaderCircle,
  Play,
  RefreshCw,
  Route,
  ShieldCheck,
  Sparkles,
  TestTube2,
  TimerReset,
  WalletCards,
  XCircle,
  Zap,
} from "lucide-react";
import { MetricCard } from "../components/MetricCard";
import { getApiErrorMessage } from "../services/apiClient";
import {
  getIntelligenceOverview,
  previewExecutionPlan,
  testIntelligenceProvider,
} from "../services/intelligenceApi";
import type {
  ExecutionPlanPreviewRequest,
  IntelligenceExecution,
  IntelligenceModelDefinition,
} from "../types/intelligence";
import "../App.css";
import "../functional-workspaces.css";

const defaultPlan: ExecutionPlanPreviewRequest = {
  provider: "Deterministic",
  model: "convolab-deterministic-primary",
  estimatedInputTokens: 1200,
  maxOutputTokens: 400,
  streaming: true,
  allowFallback: true,
  maxAttempts: 3,
  requiredCapabilities: ["Chat", "TextGeneration"],
};

function formatNumber(value: number) {
  return new Intl.NumberFormat().format(Math.round(value));
}

function formatCost(value: number, currency = "ZAR") {
  return new Intl.NumberFormat("en-ZA", {
    style: "currency",
    currency,
    minimumFractionDigits: value < 0.01 ? 4 : 2,
    maximumFractionDigits: value < 0.01 ? 6 : 2,
  }).format(value);
}

function formatLatency(value: number) {
  if (value >= 1000) return `${(value / 1000).toFixed(2)}s`;
  return `${Math.round(value)}ms`;
}

export function IntelligenceCenterPage() {
  const queryClient = useQueryClient();
  const overviewQuery = useQuery({
    queryKey: ["intelligence-overview"],
    queryFn: getIntelligenceOverview,
    refetchInterval: 30_000,
  });
  const overview = overviewQuery.data;
  const [planRequest, setPlanRequest] = useState<ExecutionPlanPreviewRequest>(defaultPlan);
  const [selectedExecution, setSelectedExecution] = useState<IntelligenceExecution | null>(null);
  const [providerMessage, setProviderMessage] = useState<string | null>(null);

  const selectedProvider = overview?.providers.find(provider => provider.key === planRequest.provider);
  const availableCapabilities = useMemo(
    () => Array.from(new Set(selectedProvider?.models.flatMap(model => model.capabilities) ?? [])).sort(),
    [selectedProvider],
  );
  const planMutation = useMutation({
    mutationFn: previewExecutionPlan,
  });

  const providerTestMutation = useMutation({
    mutationFn: testIntelligenceProvider,
    onSuccess: result => {
      setProviderMessage(`${result.provider} is ${result.status.toLowerCase()} (${formatLatency(result.latencyMs)}).`);
      void queryClient.invalidateQueries({ queryKey: ["intelligence-overview"] });
    },
    onError: error => setProviderMessage(getApiErrorMessage(error)),
  });

  const maxDailyExecutions = useMemo(
    () => Math.max(1, ...(overview?.dailyUsage.map(item => item.executions) ?? [1])),
    [overview],
  );

  if (overviewQuery.isLoading) {
    return <div className="page-loading"><LoaderCircle className="spin" /> Loading Intelligence Center</div>;
  }

  if (overviewQuery.isError || !overview) {
    return (
      <section className="panel intelligence-error-state">
        <CircleAlert size={28} />
        <h2>Intelligence Center is unavailable</h2>
        <p>{getApiErrorMessage(overviewQuery.error)}</p>
        <button className="primary-button" onClick={() => void overviewQuery.refetch()}><RefreshCw size={15} /> Retry</button>
      </section>
    );
  }

  const metrics = overview.metrics;
  const budgetPercent = Math.round(overview.budget.utilisation * 100);

  return (
    <div className="page-stack intelligence-center">
      <section className="page-heading">
        <div className="page-heading-icon"><BrainCircuit size={25} /></div>
        <div className="page-heading-copy">
          <div className="page-heading-meta"><span>Intelligence Engine</span><span className="source-chip"><Activity size={13} /> Live execution telemetry</span></div>
          <h2>Intelligence Center</h2>
          <p>Inspect provider readiness, model capabilities, execution decisions, recovery behaviour, token consumption, latency, and cost across ConvoLab Studio.</p>
        </div>
        <button className="secondary-button" onClick={() => void overviewQuery.refetch()} disabled={overviewQuery.isFetching}>
          <RefreshCw className={overviewQuery.isFetching ? "spin" : ""} size={15} /> Refresh
        </button>
      </section>

      <section className="metrics-grid intelligence-metrics">
        <MetricCard label="Executions" value={formatNumber(metrics.totalExecutions)} detail={`${Math.round(metrics.successRate * 100)}% successful`} icon={Zap} tone="accent" />
        <MetricCard label="Average latency" value={formatLatency(metrics.averageLatencyMs)} detail="End-to-end execution" icon={Clock3} />
        <MetricCard label="Tokens" value={formatNumber(metrics.totalTokens)} detail={`${metrics.retryExecutions} retries · ${metrics.fallbackExecutions} fallbacks`} icon={Gauge} />
        <MetricCard label="Actual cost" value={formatCost(metrics.totalCost, metrics.currency)} detail="Across persisted simulator runs" icon={BadgeDollarSign} tone={overview.budget.status === "Warning" ? "warning" : "positive"} />
      </section>

      <section className="intelligence-overview-grid">
        <article className="panel provider-catalogue-panel">
          <div className="panel-header">
            <div><span className="panel-eyebrow">Runtime adapters</span><h3>Provider & model catalogue</h3></div>
            <span className="source-chip">{overview.providers.length} provider{overview.providers.length === 1 ? "" : "s"}</span>
          </div>
          {providerMessage && <div className="intelligence-inline-message"><TestTube2 size={15} /> {providerMessage}</div>}
          <div className="provider-card-grid">
            {overview.providers.map(provider => (
              <article key={provider.key} className={`provider-card ${provider.isConfigured ? "provider-ready" : "provider-unconfigured"}`}>
                <div className="provider-card-header">
                  <div className="provider-icon"><Cpu size={18} /></div>
                  <div><strong>{provider.displayName}</strong><span>{provider.isLive ? "Live external provider" : "Local deterministic adapter"}</span></div>
                  <span className={`runtime-badge ${provider.isConfigured ? "runtime-ready" : "runtime-blocked"}`}>{provider.status}</span>
                </div>
                {!provider.isConfigured && <p className="provider-warning"><CircleAlert size={14} /> {provider.configurationHint}</p>}
                <div className="provider-model-list">
                  {provider.models.map(model => <ModelSummary key={model.key} model={model} />)}
                </div>
                <button
                  className="secondary-button provider-test-button"
                  onClick={() => providerTestMutation.mutate(provider.key)}
                  disabled={providerTestMutation.isPending}
                >
                  {providerTestMutation.isPending ? <LoaderCircle className="spin" size={14} /> : <TestTube2 size={14} />} Test connection
                </button>
              </article>
            ))}
          </div>
        </article>

        <aside className="panel intelligence-budget-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Cost governance</span><h3>Monthly AI budget</h3></div><WalletCards size={20} /></div>
          <div className="budget-ring" style={{ "--budget-progress": `${Math.min(100, budgetPercent)}%` } as CSSProperties}>
            <div><strong>{budgetPercent}%</strong><span>consumed</span></div>
          </div>
          <div className="budget-numbers">
            <div><span>Limit</span><strong>{formatCost(overview.budget.limit)}</strong></div>
            <div><span>Consumed</span><strong>{formatCost(overview.budget.consumed)}</strong></div>
            <div><span>Remaining</span><strong>{formatCost(overview.budget.remaining)}</strong></div>
          </div>
          <p className="muted intelligence-budget-period">Renews {new Date(overview.budget.periodEnd).toLocaleDateString()} · Status: {overview.budget.status}</p>
          <div className="intelligence-usage-bars">
            {overview.dailyUsage.map(item => (
              <div key={item.date} className="usage-bar-item" title={`${item.executions} executions · ${formatNumber(item.tokens)} tokens`}>
                <div className="usage-bar-track"><span style={{ height: `${Math.max(5, item.executions / maxDailyExecutions * 100)}%` }} /></div>
                <small>{new Date(`${item.date}T00:00:00`).toLocaleDateString(undefined, { weekday: "short" })}</small>
              </div>
            ))}
          </div>
        </aside>
      </section>

      <section className="intelligence-main-grid">
        <article className="panel execution-history-panel">
          <div className="panel-header">
            <div><span className="panel-eyebrow">Persisted runtime history</span><h3>Recent intelligent executions</h3></div>
            <span className="source-chip">{overview.recentExecutions.length} recent</span>
          </div>
          {overview.recentExecutions.length === 0 ? (
            <div className="empty-state compact-empty"><Sparkles size={27} /><h3>No executions recorded</h3><p>Run a conversation simulation to populate provider, model, token, cost, and latency telemetry.</p></div>
          ) : (
            <div className="execution-table-wrap">
              <table className="execution-table">
                <thead><tr><th>Execution</th><th>Provider</th><th>Tokens</th><th>Latency</th><th>Cost</th><th>Recovery</th><th>Status</th></tr></thead>
                <tbody>
                  {overview.recentExecutions.map(execution => (
                    <tr key={execution.runId} onClick={() => setSelectedExecution(execution)} className={selectedExecution?.runId === execution.runId ? "selected" : ""}>
                      <td><strong>{execution.simulationTitle}</strong><span>{new Date(execution.createdAt).toLocaleString()}</span></td>
                      <td><strong>{execution.provider}</strong><span>{execution.model}</span></td>
                      <td>{formatNumber(execution.totalTokens)}</td>
                      <td>{formatLatency(execution.durationMs)}</td>
                      <td>{formatCost(execution.cost, execution.currency)}</td>
                      <td>{execution.attempts} attempt{execution.attempts === 1 ? "" : "s"} · {execution.fallbacksUsed} fallback</td>
                      <td><span className={`runtime-badge ${execution.status === "Completed" ? "runtime-ready" : "runtime-blocked"}`}>{execution.status}</span></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </article>

        <aside className="panel execution-inspector-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Execution inspector</span><h3>{selectedExecution ? selectedExecution.simulationTitle : "Select a run"}</h3></div><Route size={19} /></div>
          {selectedExecution ? <ExecutionInspector execution={selectedExecution} /> : <div className="inspector-placeholder"><Activity size={24} /><p>Select an execution to inspect model selection, recovery, quality, and cost.</p></div>}
        </aside>
      </section>

      <section className="panel plan-preview-panel">
        <div className="panel-header"><div><span className="panel-eyebrow">Admission control</span><h3>Execution plan preview</h3></div><ShieldCheck size={20} /></div>
        <div className="plan-preview-layout">
          <div className="plan-form-grid">
            <label><span>Provider</span><select value={planRequest.provider} onChange={event => { const provider = overview.providers.find(item => item.key === event.target.value); setPlanRequest(current => ({ ...current, provider: event.target.value, model: provider?.models[0]?.key ?? current.model })); }}>{overview.providers.map(provider => <option key={provider.key} value={provider.key}>{provider.displayName}</option>)}</select></label>
            <label><span>Model</span><select value={planRequest.model} onChange={event => setPlanRequest(current => ({ ...current, model: event.target.value }))}>{selectedProvider?.models.map(model => <option key={model.key} value={model.key}>{model.displayName}</option>)}</select></label>
            <label><span>Estimated input tokens</span><input type="number" min="1" value={planRequest.estimatedInputTokens} onChange={event => setPlanRequest(current => ({ ...current, estimatedInputTokens: Number(event.target.value) }))} /></label>
            <label><span>Maximum output tokens</span><input type="number" min="1" value={planRequest.maxOutputTokens} onChange={event => setPlanRequest(current => ({ ...current, maxOutputTokens: Number(event.target.value) }))} /></label>
            <label><span>Maximum attempts</span><input type="number" min="1" max="10" value={planRequest.maxAttempts} onChange={event => setPlanRequest(current => ({ ...current, maxAttempts: Number(event.target.value) }))} /></label>
            <label className="plan-check"><input type="checkbox" checked={planRequest.streaming} onChange={event => setPlanRequest(current => ({ ...current, streaming: event.target.checked }))} /><span>Require streaming</span></label>
            <label className="plan-check"><input type="checkbox" checked={planRequest.allowFallback} onChange={event => setPlanRequest(current => ({ ...current, allowFallback: event.target.checked }))} /><span>Allow fallback</span></label>
            <fieldset className="plan-capabilities">
              <legend>Required capabilities</legend>
              {availableCapabilities.map(capability => (
                <label key={capability}>
                  <input
                    type="checkbox"
                    checked={planRequest.requiredCapabilities.includes(capability)}
                    onChange={event => setPlanRequest(current => ({
                      ...current,
                      requiredCapabilities: event.target.checked
                        ? [...current.requiredCapabilities, capability]
                        : current.requiredCapabilities.filter(value => value !== capability),
                    }))}
                  />
                  <span>{capability}</span>
                </label>
              ))}
            </fieldset>
            <button className="primary-button plan-preview-button" onClick={() => planMutation.mutate(planRequest)} disabled={planMutation.isPending}>
              {planMutation.isPending ? <LoaderCircle className="spin" size={15} /> : <Play size={15} />} Preview plan
            </button>
          </div>
          <div className="plan-result">
            {planMutation.isError && <div className="provider-warning"><XCircle size={15} /> {getApiErrorMessage(planMutation.error)}</div>}
            {!planMutation.data && !planMutation.isPending && <div className="inspector-placeholder"><TimerReset size={24} /><p>Preview how ConvoLab will admit and price the requested execution before sending it to a provider.</p></div>}
            {planMutation.data && (
              <>
                <div className="plan-result-summary">
                  <div><span>Model</span><strong>{planMutation.data.model}</strong></div>
                  <div><span>Estimated tokens</span><strong>{formatNumber(planMutation.data.estimatedTotalTokens)}</strong></div>
                  <div><span>Estimated latency</span><strong>{formatLatency(planMutation.data.estimatedLatencyMs)}</strong></div>
                  <div><span>Estimated cost</span><strong>{planMutation.data.estimatedCost == null ? "Not configured" : formatCost(planMutation.data.estimatedCost, planMutation.data.currency)}</strong></div>
                </div>
                <div className="plan-decision-list">
                  {planMutation.data.decisions.map(decision => (
                    <div key={decision.name} className={decision.status === "Blocked" ? "decision-blocked" : "decision-approved"}>
                      {decision.status === "Blocked" ? <XCircle size={16} /> : <CheckCircle2 size={16} />}
                      <span><strong>{decision.name}</strong><small>{decision.detail}</small></span>
                    </div>
                  ))}
                </div>
                {planMutation.data.warnings.length > 0 && <div className="plan-warnings">{planMutation.data.warnings.map(warning => <p key={warning}><CircleAlert size={14} /> {warning}</p>)}</div>}
              </>
            )}
          </div>
        </div>
      </section>
    </div>
  );
}

function ModelSummary({ model }: { model: IntelligenceModelDefinition }) {
  return (
    <div className="provider-model-row">
      <div><strong>{model.displayName}</strong><span>{formatNumber(model.maxContextTokens)} context · {formatNumber(model.maxOutputTokens)} output</span></div>
      <div className="capability-cloud">{model.capabilities.slice(0, 5).map(capability => <span key={capability}>{capability}</span>)}</div>
      <small>{model.inputPricePer1K == null ? "Pricing not configured" : `${formatCost(model.inputPricePer1K, model.currency)} / 1K input`} · {formatLatency(model.typicalLatencyMs)} typical</small>
    </div>
  );
}

function ExecutionInspector({ execution }: { execution: IntelligenceExecution }) {
  return (
    <div className="execution-inspector-content">
      <div className="inspector-kv"><span>Provider</span><strong>{execution.provider}</strong></div>
      <div className="inspector-kv"><span>Model</span><strong>{execution.model}</strong></div>
      <div className="inspector-kv"><span>Mode</span><strong>{execution.mode}</strong></div>
      <div className="inspector-kv"><span>Attempts</span><strong>{execution.attempts}</strong></div>
      <div className="inspector-kv"><span>Fallbacks</span><strong>{execution.fallbacksUsed}</strong></div>
      <div className="inspector-kv"><span>Provider latency</span><strong>{formatLatency(execution.providerLatencyMs)}</strong></div>
      <div className="inspector-section-title">Usage</div>
      <div className="token-breakdown"><div><span>Input</span><strong>{formatNumber(execution.inputTokens)}</strong></div><div><span>Output</span><strong>{formatNumber(execution.outputTokens)}</strong></div><div><span>Total</span><strong>{formatNumber(execution.totalTokens)}</strong></div></div>
      <div className="inspector-section-title">Evaluation</div>
      <div className="quality-row"><span>Groundedness</span><progress max="1" value={execution.groundedness} /><strong>{Math.round(execution.groundedness * 100)}%</strong></div>
      <div className="quality-row"><span>Relevance</span><progress max="1" value={execution.relevance} /><strong>{Math.round(execution.relevance * 100)}%</strong></div>
      <div className="execution-verdict"><Sparkles size={14} /> {execution.verdict}</div>
      {execution.failureReason && <div className="provider-warning"><XCircle size={14} /> {execution.failureReason}</div>}
      <Link className="secondary-button execution-link" to={"/conversations?simulation=" + execution.simulationId + "&run=" + execution.runId}>Open in Conversation Simulator</Link>
    </div>
  );
}
