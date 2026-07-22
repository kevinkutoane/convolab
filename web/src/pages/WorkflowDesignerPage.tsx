import { useEffect, useMemo, useRef, useState } from "react";
import {
  AlertTriangle,
  CheckCircle2,
  GitBranch,
  GripVertical,
  Plus,
  Save,
  Send,
  ShieldCheck,
  Split,
  Trash2,
  Workflow,
} from "lucide-react";
import { getApiErrorMessage } from "../services/apiClient";
import { CreateResourceDialog, type CreateResourceField } from "../components/CreateResourceDialog";
import { ErrorState, LoadingState } from "../components/AsyncStates";
import * as api from "../services/workflowApi";
import type {
  WorkflowDetail,
  WorkflowNode,
  WorkflowNodeKind,
  WorkflowSummary,
  WorkflowTransition,
} from "../types/workflow";
import "../App.css";
import "../functional-workspaces.css";

const kinds: WorkflowNodeKind[] = ["Start", "Knowledge", "Prompt", "Decision", "Intelligence", "Response", "End"];
const nodeWidth = 180;
const nodeHeight = 70;

const workflowFields: CreateResourceField[] = [
  { name: "name", label: "Workflow name", placeholder: "Claims Intake" },
  { name: "owner", label: "Owner", placeholder: "Conversation team" },
  { name: "tags", label: "Tags", placeholder: "conversation, enterprise", required: false },
  { name: "description", label: "Description", type: "textarea", placeholder: "What journey this workflow governs" },
];

const initialWorkflowDraft = { name: "Claims Intake", description: "Governed conversational workflow", owner: "Kevin", tags: "conversation, enterprise" };

function newId() {
  return crypto.randomUUID();
}

function starterGraph() {
  const start = newId(), knowledge = newId(), prompt = newId(), intelligence = newId(), response = newId(), end = newId();
  const nodes: WorkflowNode[] = [
    { id: start, name: "Start", kind: "Start", positionX: 50, positionY: 170, configuration: {} },
    { id: knowledge, name: "Retrieve knowledge", kind: "Knowledge", positionX: 280, positionY: 170, configuration: {} },
    { id: prompt, name: "Render prompt", kind: "Prompt", positionX: 510, positionY: 170, configuration: {} },
    { id: intelligence, name: "Generate response", kind: "Intelligence", positionX: 740, positionY: 170, configuration: {} },
    { id: response, name: "Return response", kind: "Response", positionX: 970, positionY: 170, configuration: {} },
    { id: end, name: "End", kind: "End", positionX: 1200, positionY: 170, configuration: {} },
  ];
  const transitions: WorkflowTransition[] = nodes.slice(0, -1).map((node, index) => ({
    id: newId(), fromNodeId: node.id, toNodeId: nodes[index + 1].id, label: "", condition: null,
  }));
  return { nodes, transitions };
}

export function WorkflowDesignerPage() {
  const [items, setItems] = useState<WorkflowSummary[]>([]);
  const [selectedId, setSelectedId] = useState<string>();
  const [detail, setDetail] = useState<WorkflowDetail>();
  const [selectedVersionId, setSelectedVersionId] = useState<string>();
  const [nodes, setNodes] = useState<WorkflowNode[]>([]);
  const [transitions, setTransitions] = useState<WorkflowTransition[]>([]);
  const [selectedNodeId, setSelectedNodeId] = useState<string>();
  const [version, setVersion] = useState("1.0.0");
  const [message, setMessage] = useState("");
  const [changeSummary, setChangeSummary] = useState("Designed in Workflow Studio");
  const [linkFrom, setLinkFrom] = useState("");
  const [linkTo, setLinkTo] = useState("");
  const [createOpen, setCreateOpen] = useState(false);
  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState("");
  const [workflowDraft, setWorkflowDraft] = useState<Record<string, string>>(initialWorkflowDraft);
  const [initialLoading, setInitialLoading] = useState(true);
  const [initialError, setInitialError] = useState("");
  const canvasRef = useRef<HTMLDivElement>(null);
  const drag = useRef<{ id: string; dx: number; dy: number } | undefined>(undefined);

  const current = selectedVersionId ? detail?.versions.find(item => item.id === selectedVersionId) : undefined;
  const selectedNode = nodes.find(item => item.id === selectedNodeId);
  const editable = !current || current.status === "Draft";
  const canAddTransition = editable && Boolean(linkFrom) && Boolean(linkTo) && linkFrom !== linkTo
    && !transitions.some(item => item.fromNodeId === linkFrom && item.toNodeId === linkTo);

  const orderedIssues = useMemo(() => current?.validationIssues ?? [], [current]);

  async function refresh(preferredId?: string, preferredVersionId?: string) {
    const list = await api.listWorkflows();
    setItems(list);
    const id = preferredId ?? selectedId ?? list[0]?.id;
    if (!id) return;
    setSelectedId(id);
    const next = await api.getWorkflow(id);
    setDetail(next);
    const target = next.versions.find(item => item.id === preferredVersionId) ?? next.versions[0];
    setSelectedVersionId(target?.id);
    if (target) {
      setNodes(target.nodes);
      setTransitions(target.transitions);
    } else {
      const graph = starterGraph();
      setNodes(graph.nodes);
      setTransitions(graph.transitions);
    }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void refresh().catch(error => setInitialError(getApiErrorMessage(error))).finally(() => setInitialLoading(false));
    }, 0);
    return () => window.clearTimeout(timer);
    // Initial API synchronization only.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function create() {
    setCreating(true);
    setCreateError("");
    try {
      const created = await api.createWorkflow({
        name: workflowDraft.name.trim(),
        description: workflowDraft.description.trim(),
        owner: workflowDraft.owner.trim(),
        tags: workflowDraft.tags.split(",").map(value => value.trim()).filter(Boolean),
      });
      setMessage("Workflow created. Create its first version.");
      await refresh(created.id);
      setCreateOpen(false);
      setWorkflowDraft(initialWorkflowDraft);
    } catch (error) {
      setCreateError(getApiErrorMessage(error));
    } finally {
      setCreating(false);
    }
  }

  async function createVersion() {
    if (!detail) return;
    try {
      const created = await api.createWorkflowVersion(detail.id, {
        version,
        changeSummary,
        nodes,
        transitions,
        expectedWorkflowRevision: detail.revision,
      });
      setMessage(`Workflow v${created.version} created.`);
      await refresh(detail.id, created.id);
    } catch (error) {
      setMessage(getApiErrorMessage(error));
    }
  }

  async function saveGraph() {
    if (!current) return;
    try {
      const saved = await api.updateWorkflowGraph(current.id, {
        nodes,
        transitions,
        changeSummary,
        expectedRevision: current.revision,
      });
      setMessage(`Workflow graph saved. ${saved.validationIssues.length ? `${saved.validationIssues.length} validation issue(s) remain.` : "Graph is valid."}`);
      await refresh(detail?.id, saved.id);
    } catch (error) {
      setMessage(getApiErrorMessage(error));
    }
  }

  async function validate() {
    if (!current) return;
    try {
      const validated = await api.validateWorkflow(current.id);
      setMessage(validated.isValid ? "Workflow graph is valid." : `${validated.validationIssues.length} validation issue(s) found.`);
      await refresh(detail?.id, validated.id);
    } catch (error) {
      setMessage(getApiErrorMessage(error));
    }
  }

  async function transition(action: string) {
    if (!current) return;
    try {
      const updated = await api.transitionWorkflowVersion(current.id, action, current.revision);
      setMessage(`Workflow ${action} completed.`);
      await refresh(detail?.id, updated.id);
    } catch (error) {
      setMessage(getApiErrorMessage(error));
    }
  }

  function selectWorkflow(id: string) {
    setSelectedId(id);
    void api.getWorkflow(id).then(next => {
      setDetail(next);
      const target = next.versions[0];
      setSelectedVersionId(target?.id);
      if (target) {
        setNodes(target.nodes);
        setTransitions(target.transitions);
      } else {
        const graph = starterGraph();
        setNodes(graph.nodes);
        setTransitions(graph.transitions);
      }
    }).catch(error => setMessage(getApiErrorMessage(error)));
  }

  function selectVersion(id: string) {
    const target = detail?.versions.find(item => item.id === id);
    setSelectedVersionId(id || undefined);
    if (target) {
      setNodes(target.nodes);
      setTransitions(target.transitions);
    } else {
      const graph = starterGraph();
      setNodes(graph.nodes);
      setTransitions(graph.transitions);
    }
    setSelectedNodeId(undefined);
  }

  function addNode(kind: WorkflowNodeKind) {
    if (!editable) return;
    const id = newId();
    setNodes(currentNodes => [...currentNodes, {
      id,
      name: kind === "Decision" ? "Decision" : kind,
      kind,
      positionX: 120 + (currentNodes.length % 4) * 220,
      positionY: 80 + Math.floor(currentNodes.length / 4) * 140,
      configuration: {},
    }]);
    setSelectedNodeId(id);
  }

  function updateNode(patch: Partial<WorkflowNode>) {
    if (!selectedNodeId || !editable) return;
    setNodes(currentNodes => currentNodes.map(node => node.id === selectedNodeId ? { ...node, ...patch } : node));
  }

  function removeNode(id: string) {
    if (!editable) return;
    setNodes(currentNodes => currentNodes.filter(node => node.id !== id));
    setTransitions(currentTransitions => currentTransitions.filter(item => item.fromNodeId !== id && item.toNodeId !== id));
    setSelectedNodeId(undefined);
  }

  function addTransition() {
    if (!editable || !linkFrom || !linkTo || linkFrom === linkTo) return;
    if (transitions.some(item => item.fromNodeId === linkFrom && item.toNodeId === linkTo)) return;
    setTransitions(currentTransitions => [...currentTransitions, {
      id: newId(), fromNodeId: linkFrom, toNodeId: linkTo, label: "", condition: null,
    }]);
  }

  function updateTransition(id: string, patch: Partial<WorkflowTransition>) {
    if (!editable) return;
    setTransitions(current => current.map(item => item.id === id ? { ...item, ...patch } : item));
  }

  function removeTransition(id: string) {
    if (!editable) return;
    setTransitions(current => current.filter(item => item.id !== id));
  }

  function onPointerDown(event: React.PointerEvent, node: WorkflowNode) {
    if (!editable || !canvasRef.current) return;
    const rect = canvasRef.current.getBoundingClientRect();
    drag.current = { id: node.id, dx: event.clientX - rect.left + canvasRef.current.scrollLeft - node.positionX, dy: event.clientY - rect.top + canvasRef.current.scrollTop - node.positionY };
    (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
  }

  function onPointerMove(event: React.PointerEvent) {
    if (!drag.current || !canvasRef.current) return;
    const rect = canvasRef.current.getBoundingClientRect();
    const x = Math.max(10, event.clientX - rect.left + canvasRef.current.scrollLeft - drag.current.dx);
    const y = Math.max(10, event.clientY - rect.top + canvasRef.current.scrollTop - drag.current.dy);
    setNodes(currentNodes => currentNodes.map(node => node.id === drag.current?.id ? { ...node, positionX: x, positionY: y } : node));
  }

  function onPointerUp() {
    drag.current = undefined;
  }

  if (initialLoading) return <LoadingState label="Loading Workflow Designer…" />;
  if (initialError && !items.length) return <ErrorState message={initialError} onRetry={() => { setInitialLoading(true); setInitialError(""); void refresh().catch(error => setInitialError(getApiErrorMessage(error))).finally(() => setInitialLoading(false)); }} />;
  return <div className="page-stack workflow-designer-page">
    <section className="page-heading">
      <div className="page-heading-icon"><Workflow size={24} /></div>
      <div className="page-heading-copy">
        <div className="page-heading-meta"><span>Workflow Engine</span><span>Versioned · validated · executable</span></div>
        <h2>Workflow Designer</h2>
        <p>Compose governed conversational workflows and publish them for use in Conversation Simulator.</p>
      </div>
      <button className="primary-button" onClick={() => setCreateOpen(true)}><Plus size={16} /> New workflow</button>
    </section>

    <CreateResourceDialog open={createOpen} title="New workflow" description="Create the governed workflow definition before designing and versioning its execution graph." submitLabel="Create workflow" fields={workflowFields} values={workflowDraft} busy={creating} error={createError} onChange={(name, value) => setWorkflowDraft(current => ({ ...current, [name]: value }))} onClose={() => !creating && setCreateOpen(false)} onSubmit={create} />

    {message && <div className="panel workflow-notice" role="status" aria-live="polite">{message}</div>}

    <section className="workflow-studio-layout">
      <aside className="panel workflow-library">
        <div className="panel-header"><div><span className="panel-eyebrow">Definitions</span><h3>{items.length} workflows</h3></div></div>
        {items.map(item => <button key={item.id} className={`workflow-list-item ${selectedId === item.id ? "active" : ""}`} onClick={() => selectWorkflow(item.id)}>
          <strong>{item.name}</strong><span>v{item.latestVersion} · {item.versionCount} versions</span><small>{item.status} · {item.owner}</small>
        </button>)}
        {!items.length && <div className="empty-state compact"><GitBranch size={28} /><h3>Create your first workflow</h3></div>}
      </aside>

      <main className="panel workflow-workspace">
        <div className="workspace-toolbar">
          <div><span className="panel-eyebrow">Definition</span><h3>{detail?.name ?? "No workflow selected"}</h3></div>
          {detail && <div className="workflow-version-controls">
            <select value={selectedVersionId ?? ""} onChange={event => selectVersion(event.target.value)}>
              <option value="">New version</option>
              {detail.versions.map(item => <option key={item.id} value={item.id}>v{item.version} · {item.status}</option>)}
            </select>
            <input className="workflow-summary-input" value={changeSummary} onChange={event => setChangeSummary(event.target.value)} aria-label="Change summary" placeholder="Change summary" />
            {!current && <><input value={version} onChange={event => setVersion(event.target.value)} aria-label="Version" /><button className="primary-button" onClick={createVersion}><Plus size={15} /> Create version</button></>}
            {current?.status === "Draft" && <button className="primary-button" onClick={saveGraph}><Save size={15} /> Save graph</button>}
          </div>}
        </div>

        {detail && <>
          <div className="workflow-palette">
            {kinds.map(kind => <button key={kind} onClick={() => addNode(kind)} disabled={!editable}><Plus size={13} /> {kind}</button>)}
          </div>
          <div className="workflow-canvas" ref={canvasRef} onPointerMove={onPointerMove} onPointerUp={onPointerUp} onPointerLeave={onPointerUp}>
            <div className="workflow-canvas-inner">
              <svg className="workflow-edges" width="1600" height="750" aria-hidden="true">
                {transitions.map(edge => {
                  const from = nodes.find(node => node.id === edge.fromNodeId);
                  const to = nodes.find(node => node.id === edge.toNodeId);
                  if (!from || !to) return null;
                  const x1 = from.positionX + nodeWidth, y1 = from.positionY + nodeHeight / 2, x2 = to.positionX, y2 = to.positionY + nodeHeight / 2;
                  const mid = Math.max(40, Math.abs(x2 - x1) / 2);
                  return <g key={edge.id}><path d={`M ${x1} ${y1} C ${x1 + mid} ${y1}, ${x2 - mid} ${y2}, ${x2} ${y2}`} /><circle cx={x2} cy={y2} r="4" />{edge.label && <text x={(x1 + x2) / 2} y={(y1 + y2) / 2 - 8}>{edge.label}</text>}</g>;
                })}
              </svg>
              {nodes.map(node => <button
                type="button"
                key={node.id}
                className={`workflow-node node-${node.kind.toLowerCase()} ${selectedNodeId === node.id ? "selected" : ""}`}
                style={{ left: node.positionX, top: node.positionY, width: nodeWidth, height: nodeHeight }}
                onClick={() => setSelectedNodeId(node.id)}
                onPointerDown={event => onPointerDown(event, node)}
              >
                <GripVertical size={15} /><span><small>{node.kind}</small><strong>{node.name}</strong></span>
              </button>)}
            </div>
          </div>
        </>}
      </main>

      <aside className="panel workflow-inspector">
        <div className="panel-header"><div><span className="panel-eyebrow">Inspector</span><h3>{current ? `v${current.version}` : "Draft graph"}</h3></div>{current && <span className={`status-pill status-${current.status.toLowerCase()}`}>{current.status}</span>}</div>
        {current && <div className="workflow-health"><div><span>Nodes</span><strong>{nodes.length}</strong></div><div><span>Edges</span><strong>{transitions.length}</strong></div><div><span>Valid</span><strong>{current.isValid ? "Yes" : "No"}</strong></div></div>}

        {selectedNode && <div className="workflow-node-editor">
          <label>Name<input value={selectedNode.name} onChange={event => updateNode({ name: event.target.value })} disabled={!editable} /></label>
          <label>Kind<select value={selectedNode.kind} onChange={event => updateNode({ kind: event.target.value as WorkflowNodeKind })} disabled={!editable}>{kinds.map(kind => <option key={kind}>{kind}</option>)}</select></label>
          <label>Reference<input value={selectedNode.configuration.reference ?? ""} onChange={event => updateNode({ configuration: { ...selectedNode.configuration, reference: event.target.value } })} placeholder="Prompt or collection reference" disabled={!editable} /></label>
          {editable && <button className="danger-button" onClick={() => removeNode(selectedNode.id)}><Trash2 size={14} /> Remove node</button>}
        </div>}

        {detail && <div className="workflow-link-editor">
          <span className="panel-eyebrow">Connect nodes</span>
          <select value={linkFrom} onChange={event => setLinkFrom(event.target.value)}><option value="">From</option>{nodes.map(node => <option key={node.id} value={node.id}>{node.name}</option>)}</select>
          <select value={linkTo} onChange={event => setLinkTo(event.target.value)}><option value="">To</option>{nodes.map(node => <option key={node.id} value={node.id}>{node.name}</option>)}</select>
          <button onClick={addTransition} disabled={!canAddTransition}><Split size={14} /> Add transition</button>
          <div className="workflow-transition-list">
            {transitions.map(edge => <div key={edge.id}>
              <span>{nodes.find(node => node.id === edge.fromNodeId)?.name ?? "?"} → {nodes.find(node => node.id === edge.toNodeId)?.name ?? "?"}</span>
              <input value={edge.label} onChange={event => updateTransition(edge.id, { label: event.target.value })} placeholder="Label" disabled={!editable} />
              <input value={edge.condition ?? ""} onChange={event => updateTransition(edge.id, { condition: event.target.value || null })} placeholder="contains:hail" disabled={!editable} />
              {editable && <button className="edge-delete" onClick={() => removeTransition(edge.id)} aria-label="Remove transition"><Trash2 size={13} /></button>}
            </div>)}
          </div>
        </div>}

        {current && <>
          <button className="secondary-button workflow-validate" onClick={validate}><CheckCircle2 size={15} /> Validate graph</button>
          <div className="workflow-issues">
            {orderedIssues.map(issue => <div key={`${issue.code}-${issue.nodeId}`}><AlertTriangle size={14} /><span>{issue.message}</span></div>)}
            {!orderedIssues.length && <div className="workflow-valid"><CheckCircle2 size={14} /><span>Graph satisfies platform rules.</span></div>}
          </div>
          <div className="lifecycle-actions">
            {current.status === "Draft" && <button onClick={() => transition("submit")}><Send size={14} /> Submit</button>}
            {current.status === "PendingApproval" && <button onClick={() => transition("approve")}><ShieldCheck size={14} /> Approve</button>}
            {current.status === "Approved" && <button onClick={() => transition("publish")}><CheckCircle2 size={14} /> Publish</button>}
            {current.status === "Published" && <button onClick={() => transition("deprecate")}>Deprecate</button>}
            {(current.status === "Draft" || current.status === "Deprecated") && <button onClick={() => transition("archive")}>Archive</button>}
            {current.status === "Archived" && <button onClick={() => transition("restore")}>Restore</button>}
          </div>
        </>}
      </aside>
    </section>
  </div>;
}
