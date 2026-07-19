import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import {
  AlertTriangle,
  Bot,
  BrainCircuit,
  CheckCircle2,
  ChevronRight,
  Copy,
  Database,
  FileCode2,
  Gauge,
  LoaderCircle,
  MessageSquarePlus,
  Play,
  Plus,
  RefreshCcw,
  RotateCcw,
  Send,
  Sparkles,
  UserRound,
  Workflow,
  XCircle,
} from "lucide-react";
import {
  createSimulation,
  getSimulation,
  getSimulationOptions,
  listSimulations,
  replaySimulation,
  sendSimulationMessage,
} from "../services/simulationApi";
import type {
  CreateSimulationRequest,
  SimulationConversation,
  SimulationMode,
  SimulationRun,
} from "../types/simulation";

const defaultCreateRequest: CreateSimulationRequest = {
  title: "Claims Assistant Simulation",
  workflow: "Claims Intake",
  promptVersion: "Claims Assistant v1.0",
  knowledgeCollection: "Claims Policies",
};

const starterMessages = [
  "Can I claim for hail damage?",
  "My windscreen cracked while I was driving. Am I covered?",
  "What information do I need after an accident?",
];

type InspectorTab = "overview" | "trace" | "knowledge" | "prompt";

export function ConversationSimulatorPage() {
  const queryClient = useQueryClient();
  const [searchParams] = useSearchParams();
  const linkedSimulationId = searchParams.get("simulation");
  const linkedRunId = searchParams.get("run");
  const [selectedSimulationId, setSelectedSimulationId] = useState<string | null>(linkedSimulationId);
  const [selectedRunId, setSelectedRunId] = useState<string | null>(linkedRunId);
  const [createRequest, setCreateRequest] = useState(defaultCreateRequest);
  const [message, setMessage] = useState(starterMessages[0]);
  const [mode, setMode] = useState<SimulationMode>("Normal");
  const [provider, setProvider] = useState("Deterministic");
  const [model, setModel] = useState("convolab-deterministic-primary");
  const [temperature, setTemperature] = useState(0.2);
  const [maxOutputTokens, setMaxOutputTokens] = useState(400);
  const [inspectorTab, setInspectorTab] = useState<InspectorTab>("overview");
  const [showCreate, setShowCreate] = useState(false);

  const optionsQuery = useQuery({
    queryKey: ["simulation-options"],
    queryFn: getSimulationOptions,
    retry: 1,
  });

  const listQuery = useQuery({
    queryKey: ["simulations"],
    queryFn: listSimulations,
    retry: 1,
  });

  const effectiveSimulationId = selectedSimulationId ?? listQuery.data?.[0]?.id ?? null;

  const simulationQuery = useQuery({
    queryKey: ["simulation", effectiveSimulationId],
    queryFn: () => getSimulation(effectiveSimulationId!),
    enabled: Boolean(effectiveSimulationId),
    retry: 1,
  });

  const selectedSimulation = simulationQuery.data;
  const selectedRun = useMemo(() => {
    if (!selectedSimulation?.runs.length) return null;
    return selectedSimulation.runs.find(run => run.id === selectedRunId)
      ?? selectedSimulation.runs.at(-1)
      ?? null;
  }, [selectedRunId, selectedSimulation]);

  const createMutation = useMutation({
    mutationFn: createSimulation,
    onSuccess: simulation => {
      queryClient.setQueryData(["simulation", simulation.id], simulation);
      void queryClient.invalidateQueries({ queryKey: ["simulations"] });
      setSelectedSimulationId(simulation.id);
      setShowCreate(false);
    },
  });

  const sendMutation = useMutation({
    mutationFn: ({ simulationId, content, runMode }: { simulationId: string; content: string; runMode: SimulationMode }) =>
      sendSimulationMessage(simulationId, { content, mode: runMode, provider, model, temperature, maxOutputTokens }),
    onSuccess: simulation => {
      queryClient.setQueryData(["simulation", simulation.id], simulation);
      void queryClient.invalidateQueries({ queryKey: ["simulations"] });
      setSelectedRunId(simulation.runs.at(-1)?.id ?? null);
      setMessage("");
    },
  });

  const replayMutation = useMutation({
    mutationFn: ({ simulationId, runId, runMode }: { simulationId: string; runId: string; runMode: SimulationMode }) =>
      replaySimulation(simulationId, { runId, mode: runMode, provider, model, temperature, maxOutputTokens }),
    onSuccess: simulation => {
      queryClient.setQueryData(["simulation", simulation.id], simulation);
      void queryClient.invalidateQueries({ queryKey: ["simulations"] });
      setSelectedRunId(simulation.runs.at(-1)?.id ?? null);
    },
  });

  const apiUnavailable = Boolean(optionsQuery.error || listQuery.error);
  const isExecuting = sendMutation.isPending || replayMutation.isPending;

  const handleCreate = () => createMutation.mutate(createRequest);
  const handleSend = () => {
    if (!effectiveSimulationId || !message.trim() || isExecuting) return;
    sendMutation.mutate({ simulationId: effectiveSimulationId, content: message.trim(), runMode: mode });
  };

  const handleReplay = (run: SimulationRun) => {
    if (!effectiveSimulationId || isExecuting) return;
    replayMutation.mutate({ simulationId: effectiveSimulationId, runId: run.id, runMode: mode });
  };

  return (
    <div className="page-stack simulator-page">
      <section className="simulator-heading">
        <div>
          <span className="panel-eyebrow">Functional vertical slice</span>
          <h2>Conversation Simulator</h2>
          <p>
            Exercise the live Conversation → Workflow → Knowledge → Prompt → Intelligence → Evaluation → Trace pipeline.
          </p>
        </div>
        <div className="simulator-heading-actions">
          <span className={`runtime-chip ${apiUnavailable ? "runtime-offline" : ""}`}>
            <span /> {apiUnavailable ? "Platform API offline" : "Deterministic runtime connected"}
          </span>
          <button className="secondary-button" onClick={() => setShowCreate(value => !value)}>
            <Plus size={16} /> New simulation
          </button>
        </div>
      </section>

      {apiUnavailable && <ApiOfflinePanel onRetry={() => {
        void optionsQuery.refetch();
        void listQuery.refetch();
      }} />}

      {showCreate && !apiUnavailable && (
        <CreateSimulationPanel
          request={createRequest}
          onChange={setCreateRequest}
          workflows={optionsQuery.data?.workflows ?? [defaultCreateRequest.workflow]}
          promptVersions={optionsQuery.data?.promptVersions ?? [defaultCreateRequest.promptVersion]}
          knowledgeCollections={optionsQuery.data?.knowledgeCollections ?? [defaultCreateRequest.knowledgeCollection]}
          isCreating={createMutation.isPending}
          error={createMutation.error ? "The simulation could not be created." : null}
          onCreate={handleCreate}
        />
      )}

      {!apiUnavailable && (
        <section className="panel provider-controls-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Intelligence runtime</span><h3>Provider configuration</h3></div><span className={`runtime-chip ${provider === "Gemini" && !optionsQuery.data?.providers.find(item => item.key === "Gemini")?.isConfigured ? "runtime-offline" : ""}`}><span />{provider === "Gemini" ? "Live provider" : "Safe deterministic mode"}</span></div>
          <div className="provider-control-grid">
            <label>Provider<select value={provider} onChange={event => { const next = event.target.value; setProvider(next); const option = optionsQuery.data?.providers.find(item => item.key === next); if (option) setModel(option.defaultModel); }}>{(optionsQuery.data?.providers ?? []).map(item => <option key={item.key} value={item.key} disabled={!item.isConfigured}>{item.displayName}{!item.isConfigured ? " — configure API key" : ""}</option>)}</select></label>
            <label>Model<input value={model} onChange={event => setModel(event.target.value)} /></label>
            <label>Temperature<input type="number" min="0" max="2" step="0.1" value={temperature} onChange={event => setTemperature(Number(event.target.value))} /></label>
            <label>Max output tokens<input type="number" min="32" max="8192" step="32" value={maxOutputTokens} onChange={event => setMaxOutputTokens(Number(event.target.value))} /></label>
          </div>
          {provider === "Gemini" && !optionsQuery.data?.providers.find(item => item.key === "Gemini")?.isConfigured && <p className="provider-warning">Gemini is disabled until <code>GEMINI_API_KEY</code> is set on the API host. Keys are never sent to or stored by the browser.</p>}
        </section>
      )}

      {!apiUnavailable && (
        <section className="simulator-workspace">
          <SimulationList
            simulations={listQuery.data ?? []}
            selectedId={effectiveSimulationId}
            isLoading={listQuery.isLoading}
            onSelect={setSelectedSimulationId}
            onCreate={() => setShowCreate(true)}
          />

          <ConversationWorkspace
            simulation={selectedSimulation}
            isLoading={simulationQuery.isLoading}
            isExecuting={isExecuting}
            message={message}
            mode={mode}
            modes={optionsQuery.data?.modes ?? ["Normal", "RetryOnce", "Fallback"]}
            error={sendMutation.error ? "The message execution failed before a result was returned." : null}
            onMessageChange={setMessage}
            onModeChange={setMode}
            onSend={handleSend}
            onStarterMessage={setMessage}
            onSelectRun={runId => {
              setSelectedRunId(runId);
              setInspectorTab("overview");
            }}
          />

          <RunInspector
            run={selectedRun}
            tab={inspectorTab}
            mode={mode}
            isReplaying={replayMutation.isPending}
            onTabChange={setInspectorTab}
            onReplay={() => selectedRun && handleReplay(selectedRun)}
          />
        </section>
      )}
    </div>
  );
}

function ApiOfflinePanel({ onRetry }: { onRetry: () => void }) {
  return (
    <section className="panel api-offline-panel">
      <div className="empty-state-icon"><AlertTriangle size={22} /></div>
      <div>
        <span className="panel-eyebrow">Connection required</span>
        <h3>Start the ASP.NET Core API to use the simulator</h3>
        <p>
          Run <code>dotnet run --project src/Api/ConvoLab.Api/ConvoLab.Api.csproj</code> from the repository root.
          The Studio proxy expects the API at <code>http://localhost:5000</code>.
        </p>
      </div>
      <button className="secondary-button" onClick={onRetry}><RefreshCcw size={15} /> Retry connection</button>
    </section>
  );
}

interface CreateSimulationPanelProps {
  request: CreateSimulationRequest;
  onChange: (request: CreateSimulationRequest) => void;
  workflows: string[];
  promptVersions: string[];
  knowledgeCollections: string[];
  isCreating: boolean;
  error: string | null;
  onCreate: () => void;
}

function CreateSimulationPanel({
  request,
  onChange,
  workflows,
  promptVersions,
  knowledgeCollections,
  isCreating,
  error,
  onCreate,
}: CreateSimulationPanelProps) {
  return (
    <section className="panel create-simulation-panel">
      <div className="panel-header">
        <div>
          <span className="panel-eyebrow">Runtime configuration</span>
          <h3>Create a governed simulation</h3>
        </div>
        <span className="source-chip"><Sparkles size={13} /> No API key required</span>
      </div>
      <div className="configuration-grid">
        <label>
          <span>Simulation name</span>
          <input value={request.title} onChange={event => onChange({ ...request, title: event.target.value })} />
        </label>
        <label>
          <span>Workflow</span>
          <select value={request.workflow} onChange={event => onChange({ ...request, workflow: event.target.value })}>
            {workflows.map(value => <option key={value}>{value}</option>)}
          </select>
        </label>
        <label>
          <span>Prompt version</span>
          <select value={request.promptVersion} onChange={event => onChange({ ...request, promptVersion: event.target.value })}>
            {promptVersions.map(value => <option key={value}>{value}</option>)}
          </select>
        </label>
        <label>
          <span>Knowledge collection</span>
          <select value={request.knowledgeCollection} onChange={event => onChange({ ...request, knowledgeCollection: event.target.value })}>
            {knowledgeCollections.map(value => <option key={value}>{value}</option>)}
          </select>
        </label>
      </div>
      <div className="form-action-row">
        {error && <span className="form-error"><XCircle size={14} /> {error}</span>}
        <button className="primary-button" onClick={onCreate} disabled={isCreating}>
          {isCreating ? <LoaderCircle className="spin" size={16} /> : <Play size={16} />}
          Create simulation
        </button>
      </div>
    </section>
  );
}

interface SimulationListProps {
  simulations: Awaited<ReturnType<typeof listSimulations>>;
  selectedId: string | null;
  isLoading: boolean;
  onSelect: (id: string) => void;
  onCreate: () => void;
}

function SimulationList({ simulations, selectedId, isLoading, onSelect, onCreate }: SimulationListProps) {
  return (
    <aside className="simulator-column simulation-list-panel">
      <div className="simulator-column-header">
        <div>
          <span className="panel-eyebrow">Workspace</span>
          <h3>Simulations</h3>
        </div>
        <button className="icon-button" onClick={onCreate} aria-label="Create simulation"><Plus size={15} /></button>
      </div>
      <div className="simulation-list">
        {isLoading && <div className="compact-loading"><LoaderCircle className="spin" size={17} /> Loading simulations</div>}
        {!isLoading && simulations.length === 0 && (
          <button className="simulation-empty" onClick={onCreate}>
            <MessageSquarePlus size={22} />
            <strong>Create the first simulation</strong>
            <span>Configure a workflow, prompt, and knowledge collection.</span>
          </button>
        )}
        {simulations.map(simulation => (
          <button
            key={simulation.id}
            className={`simulation-list-item ${selectedId === simulation.id ? "simulation-list-item-active" : ""}`}
            onClick={() => onSelect(simulation.id)}
          >
            <div className="simulation-list-icon"><Bot size={16} /></div>
            <div>
              <strong>{simulation.title}</strong>
              <span>{simulation.lastMessage || "No messages yet"}</span>
              <small>{simulation.runCount} run{simulation.runCount === 1 ? "" : "s"} · {simulation.workflow}</small>
            </div>
            <ChevronRight size={14} />
          </button>
        ))}
      </div>
    </aside>
  );
}

interface ConversationWorkspaceProps {
  simulation?: SimulationConversation;
  isLoading: boolean;
  isExecuting: boolean;
  message: string;
  mode: SimulationMode;
  modes: SimulationMode[];
  error: string | null;
  onMessageChange: (value: string) => void;
  onModeChange: (mode: SimulationMode) => void;
  onSend: () => void;
  onStarterMessage: (message: string) => void;
  onSelectRun: (runId: string) => void;
}

function ConversationWorkspace({
  simulation,
  isLoading,
  isExecuting,
  message,
  mode,
  modes,
  error,
  onMessageChange,
  onModeChange,
  onSend,
  onStarterMessage,
  onSelectRun,
}: ConversationWorkspaceProps) {
  if (isLoading) {
    return <main className="simulator-column conversation-panel compact-loading"><LoaderCircle className="spin" size={20} /> Loading conversation</main>;
  }

  if (!simulation) {
    return (
      <main className="simulator-column conversation-panel conversation-empty">
        <Bot size={34} />
        <h3>Select or create a simulation</h3>
        <p>The functional conversation canvas will appear here.</p>
      </main>
    );
  }

  const runsByAssistant = new Map(simulation.runs.filter(run => run.assistantMessageId).map(run => [run.assistantMessageId, run]));

  return (
    <main className="simulator-column conversation-panel">
      <header className="conversation-header">
        <div>
          <span className="panel-eyebrow">{simulation.workflow}</span>
          <h3>{simulation.title}</h3>
        </div>
        <div className="conversation-contracts">
          <span><Workflow size={13} /> {simulation.workflow}</span>
          <span><FileCode2 size={13} /> {simulation.promptVersion}</span>
          <span><Database size={13} /> {simulation.knowledgeCollection}</span>
        </div>
      </header>

      <div className="message-canvas">
        {simulation.messages.length === 0 && (
          <div className="conversation-welcome">
            <div className="welcome-mark"><BrainCircuit size={28} /></div>
            <h3>Run the complete platform pipeline</h3>
            <p>Select a sample question or enter your own customer message.</p>
            <div className="starter-message-grid">
              {starterMessages.map(value => (
                <button key={value} onClick={() => onStarterMessage(value)}>{value}</button>
              ))}
            </div>
          </div>
        )}

        {simulation.messages.map(item => {
          const run = runsByAssistant.get(item.id);
          return (
            <article key={item.id} className={`chat-message chat-message-${item.role}`}>
              <div className="chat-avatar">{item.role === "assistant" ? <Bot size={16} /> : <UserRound size={16} />}</div>
              <div className="chat-bubble">
                <div className="chat-message-meta">
                  <strong>{item.role === "assistant" ? "ConvoLab Assistant" : "Customer"}</strong>
                  <span>{new Date(item.createdAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</span>
                  {item.isReplay && <span className="replay-badge"><RotateCcw size={11} /> replay</span>}
                </div>
                <p>{item.content}</p>
                {run && (
                  <button className="inspect-run-button" onClick={() => onSelectRun(run.id)}>
                    Inspect run <ChevronRight size={13} />
                  </button>
                )}
              </div>
            </article>
          );
        })}

        {isExecuting && (
          <article className="chat-message chat-message-assistant">
            <div className="chat-avatar"><Bot size={16} /></div>
            <div className="chat-bubble execution-bubble">
              <LoaderCircle className="spin" size={16} /> Executing platform pipeline…
            </div>
          </article>
        )}
      </div>

      <footer className="message-composer">
        <div className="composer-controls">
          <label>
            <span>Scenario</span>
            <select value={mode} onChange={event => onModeChange(event.target.value as SimulationMode)} disabled={isExecuting}>
              {modes.map(value => <option value={value} key={value}>{formatMode(value)}</option>)}
            </select>
          </label>
          <span className="scenario-hint">{getModeHint(mode)}</span>
        </div>
        <div className="composer-input-row">
          <textarea
            rows={3}
            value={message}
            onChange={event => onMessageChange(event.target.value)}
            placeholder="Ask a customer question…"
            onKeyDown={event => {
              if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();
                onSend();
              }
            }}
          />
          <button className="send-button" onClick={onSend} disabled={!message.trim() || isExecuting} aria-label="Send message">
            {isExecuting ? <LoaderCircle className="spin" size={18} /> : <Send size={18} />}
          </button>
        </div>
        {error && <span className="form-error"><XCircle size={14} /> {error}</span>}
      </footer>
    </main>
  );
}

interface RunInspectorProps {
  run: SimulationRun | null;
  tab: InspectorTab;
  mode: SimulationMode;
  isReplaying: boolean;
  onTabChange: (tab: InspectorTab) => void;
  onReplay: () => void;
}

function RunInspector({ run, tab, mode, isReplaying, onTabChange, onReplay }: RunInspectorProps) {
  return (
    <aside className="simulator-column inspector-panel">
      <div className="simulator-column-header inspector-heading">
        <div>
          <span className="panel-eyebrow">Engineering inspector</span>
          <h3>{run ? `Run ${run.id.slice(0, 8)}` : "No run selected"}</h3>
        </div>
        {run && (
          <button className="icon-button" onClick={onReplay} disabled={isReplaying} title={`Replay using ${formatMode(mode)}`}>
            {isReplaying ? <LoaderCircle className="spin" size={15} /> : <RotateCcw size={15} />}
          </button>
        )}
      </div>

      <div className="inspector-tabs">
        {(["overview", "trace", "knowledge", "prompt"] as InspectorTab[]).map(value => (
          <button key={value} className={tab === value ? "active" : ""} onClick={() => onTabChange(value)}>{value}</button>
        ))}
      </div>

      <div className="inspector-content">
        {!run && (
          <div className="inspector-empty">
            <Gauge size={28} />
            <strong>Send a message to create a run</strong>
            <span>Plans, knowledge, traces, evaluations, and cost will appear here.</span>
          </div>
        )}
        {run && tab === "overview" && <RunOverview run={run} />}
        {run && tab === "trace" && <TraceTimeline run={run} />}
        {run && tab === "knowledge" && <KnowledgeInspector run={run} />}
        {run && tab === "prompt" && <PromptInspector run={run} />}
      </div>
    </aside>
  );
}

function RunOverview({ run }: { run: SimulationRun }) {
  const plan = run.executionPlan;
  const metrics = run.metrics;
  return (
    <div className="inspector-stack">
      <div className={`run-status-card ${run.status === "Completed" ? "run-success" : "run-failed"}`}>
        {run.status === "Completed" ? <CheckCircle2 size={18} /> : <XCircle size={18} />}
        <div><strong>{run.status}</strong><span>{formatMode(run.mode)} scenario</span></div>
      </div>

      <section className="inspector-section">
        <span className="panel-eyebrow">Workflow snapshot</span>
        <dl className="property-grid">
          <div><dt>Definition</dt><dd>{run.workflow?.name ?? "Legacy workflow"}</dd></div>
          <div><dt>Version</dt><dd>{run.workflow ? `v${run.workflow.version}` : "Not recorded"}</dd></div>
          <div><dt>Source</dt><dd>{run.workflow?.source ?? "Legacy run"}</dd></div>
          <div><dt>Nodes</dt><dd>{run.workflow?.nodes.length ?? 0}</dd></div>
        </dl>
      </section>

      {plan && (
        <section className="inspector-section">
          <span className="panel-eyebrow">Execution plan</span>
          <dl className="property-grid">
            <div><dt>Provider</dt><dd>{plan.provider}</dd></div>
            <div><dt>Model</dt><dd>{plan.model}</dd></div>
            <div><dt>Streaming</dt><dd>{plan.streaming ? "Enabled" : "Disabled"}</dd></div>
            <div><dt>Attempts</dt><dd>{plan.attempts} / {plan.maxAttempts}</dd></div>
            <div><dt>Fallbacks</dt><dd>{plan.fallbacksUsed}</dd></div>
            <div><dt>Estimate</dt><dd>{formatCurrency(plan.estimatedCost, plan.currency)}</dd></div>
          </dl>
        </section>
      )}

      {metrics && (
        <section className="inspector-section">
          <span className="panel-eyebrow">Actual telemetry</span>
          <div className="mini-metrics-grid">
            <div><strong>{metrics.totalTokens}</strong><span>tokens</span></div>
            <div><strong>{metrics.totalDurationMs.toFixed(0)} ms</strong><span>duration</span></div>
            <div><strong>{formatCurrency(metrics.actualCost, metrics.currency)}</strong><span>cost</span></div>
          </div>
        </section>
      )}

      <section className="inspector-section">
        <span className="panel-eyebrow">Evaluation</span>
        <div className="score-row"><span>Groundedness</span><strong>{Math.round(run.evaluation.groundedness * 100)}%</strong></div>
        <div className="score-track"><span style={{ width: `${run.evaluation.groundedness * 100}%` }} /></div>
        <div className="score-row"><span>Relevance</span><strong>{Math.round(run.evaluation.relevance * 100)}%</strong></div>
        <div className="score-track"><span style={{ width: `${run.evaluation.relevance * 100}%` }} /></div>
        <div className="score-row"><span>Safety</span><strong>{Math.round(run.evaluation.safety * 100)}%</strong></div>
        <div className="score-track"><span style={{ width: `${run.evaluation.safety * 100}%` }} /></div>
      </section>

      {run.failureReason && <div className="failure-detail"><AlertTriangle size={15} /> {run.failureReason}</div>}
    </div>
  );
}

function TraceTimeline({ run }: { run: SimulationRun }) {
  return (
    <div className="trace-timeline">
      {run.timeline.map((step, index) => (
        <article key={step.id} className={`trace-step trace-step-${step.status.toLowerCase()}`}>
          <div className="trace-rail">
            <span>{step.status === "Completed" ? <CheckCircle2 size={14} /> : <XCircle size={14} />}</span>
            {index < run.timeline.length - 1 && <i />}
          </div>
          <div className="trace-step-copy">
            <div><strong>{step.name}</strong><span>{step.durationMs.toFixed(1)} ms</span></div>
            <small>{step.capability}</small>
            <p>{step.detail}</p>
          </div>
        </article>
      ))}
    </div>
  );
}

function KnowledgeInspector({ run }: { run: SimulationRun }) {
  const knowledge = run.knowledgePackage;
  return (
    <div className="inspector-stack">
      <section className="knowledge-summary-card">
        <Database size={19} />
        <div><strong>{knowledge.collection}</strong><span>{knowledge.retrievalStrategy} retrieval</span></div>
        <b>{Math.round(knowledge.confidence * 100)}%</b>
      </section>
      <div className="knowledge-meta-row">
        <span>{knowledge.citations.length} citations</span>
        <span>{knowledge.tokenEstimate} tokens</span>
        <span>Sealed package</span>
      </div>
      {knowledge.citations.map((citation, index) => (
        <article className="citation-card" key={`${citation.source}-${citation.section}`}>
          <span>{String(index + 1).padStart(2, "0")}</span>
          <div><strong>{citation.source}</strong><small>{citation.section}</small><p>{citation.snippet}</p></div>
        </article>
      ))}
    </div>
  );
}

function PromptInspector({ run }: { run: SimulationRun }) {
  const [copied, setCopied] = useState(false);
  const copyPrompt = async () => {
    await navigator.clipboard.writeText(run.renderedPrompt);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 1400);
  };

  return (
    <div className="prompt-inspector">
      <div className="prompt-toolbar">
        <span>Rendered prompt</span>
        <button onClick={copyPrompt}><Copy size={13} /> {copied ? "Copied" : "Copy"}</button>
      </div>
      <pre>{run.renderedPrompt || "Prompt rendering failed before an artifact was produced."}</pre>
    </div>
  );
}

function formatMode(mode: SimulationMode) {
  if (mode === "RetryOnce") return "Retry once";
  return mode;
}

function getModeHint(mode: SimulationMode) {
  if (mode === "RetryOnce") return "First provider attempt times out, then retry succeeds.";
  if (mode === "Fallback") return "Primary model fails and the planned fallback completes the run.";
  return "Primary deterministic model completes normally.";
}

function formatCurrency(value: number, currency: string) {
  return new Intl.NumberFormat("en-ZA", {
    style: "currency",
    currency,
    minimumFractionDigits: value < 0.01 ? 4 : 2,
    maximumFractionDigits: value < 0.01 ? 4 : 2,
  }).format(value);
}
