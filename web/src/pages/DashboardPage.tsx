import { useHelp } from "../contexts/HelpContext";
import {
  ArrowRight,
  Boxes,
  CheckCircle2,
  CircleDot,
  Clock3,
  Code2,
  ShieldCheck,
  TestTube2,
} from "lucide-react";
import { useNavigate } from "react-router";
import { quickActions } from "../data/platform";
import type { PlatformStatus } from "../types/platform";
import { MetricCard } from "../components/MetricCard";
import { StatusPill } from "../components/StatusPill";
import { useAuth } from "../contexts/useAuth";

interface DashboardPageProps {
  status: PlatformStatus;
}

export function DashboardPage({ status }: DashboardPageProps) {
  useHelp({
    title: "Platform Dashboard",
    description: "Your starting point in ConvoLab Studio. The Dashboard shows live platform health, a map of all capability boundaries, and quick-launch shortcuts to the most important studios.",
    usageSteps: [
          "Check the 'Platform reliability' and 'Stable capabilities' metric cards at the top to assess system health.",
          "Review the 'Workspace capabilities' list to see which engines (Conversation, Workflow, Prompt, Knowledge, etc.) are stable vs. in development.",
          "Use the 'Start building' quick action buttons to jump directly to any studio.",
          "Click 'View architecture' to inspect the full capability map in the Intelligence Center."
    ],
    examples: [
          "Starting your day by checking that all core capabilities show 'stable' status before running simulations.",
          "After a deployment, confirming that 'Platform reliability' still shows 'Healthy' and the API is online."
    ],
    expectedOutput: "A real-time health overview of the entire platform, showing which capabilities are ready, what version they're on, and shortcuts to start working immediately.",
    aiLayerRole: "The AI layer status is reflected in the 'Intelligence Engine' capability row — a 'stable' status means the AI is ready to process conversations and evaluation requests."
  });

  const navigate = useNavigate();
  const auth = useAuth();
  const workspaceId = auth.session?.activeWorkspaceId;
  const role = auth.session?.workspaces.find(w => w.id === workspaceId)?.role ?? "Viewer";

  // Sort quick actions by role relevance so each persona sees the most useful actions first.
  const roleOrder: Record<string, string[]> = {
    Administrator: ["/operations", "/intelligence", "/policies", "/conversations", "/analytics", "/workflows"],
    Engineer: ["/conversations", "/prompts", "/knowledge", "/workflows", "/intelligence", "/policies"],
    Reviewer: ["/evaluation", "/policies", "/analytics", "/traces", "/conversations", "/prompts"],
    Operator: ["/traces", "/replay", "/analytics", "/intelligence", "/conversations", "/operations"],
    Viewer: ["/conversations", "/analytics", "/knowledge", "/prompts", "/evaluation", "/traces"],
  };
  const priorityPaths = roleOrder[role] ?? roleOrder.Viewer;
  const sortedActions = [...quickActions].sort((a, b) => {
    const ai = priorityPaths.indexOf(a.path);
    const bi = priorityPaths.indexOf(b.path);
    return (ai === -1 ? 99 : ai) - (bi === -1 ? 99 : bi);
  });

  const stableCount = status.capabilities.filter(item => item.status === "stable").length;
  const foundationCount = status.capabilities.filter(item => item.status === "foundation").length;
  const availableCapabilities = status.capabilities
    .filter(item => item.status === "stable" || item.status === "active")
    .slice(0, 4);

  return (
    <div className="page-stack">
      <section className="hero-panel">
        <div className="hero-copy">
          <div className="hero-kicker">
            <span className="live-indicator" /> ConvoLab Studio · v{status.version}
          </div>
          <h2>Design, test, and operate conversational AI.</h2>
          <p>
            Build governed conversations, workflows, prompts, and knowledge, then evaluate,
            trace, replay, and understand every execution from one connected Studio.
          </p>
          <div className="hero-actions">
            <button className="primary-button" onClick={() => navigate("/conversations")}>
              Open Conversation Simulator <ArrowRight size={16} />
            </button>
            <button className="secondary-button" onClick={() => navigate("/intelligence")}>
              Open Intelligence Center
            </button>
          </div>
        </div>
        <div className="hero-visual" aria-label="Platform execution flow">
          <div className="flow-column">
            {[
              "Conversation",
              "Workflow",
              "Knowledge",
              "Prompt",
              "Intelligence",
              "Evaluation",
              "Trace",
            ].map((label, index) => (
              <div key={label} className="flow-step">
                <span>{String(index + 1).padStart(2, "0")}</span>
                <strong>{label}</strong>
                <CheckCircle2 size={15} />
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="metrics-grid dashboard-metrics">
        <MetricCard
          label="Platform reliability"
          value={status.architectureHealth}
          detail="Capability boundaries verified"
          icon={ShieldCheck}
          tone="positive"
        />
        <MetricCard
          label="Stable capabilities"
          value={`${stableCount}/${status.capabilities.length}`}
          detail={`${foundationCount} capability foundations remain`}
          icon={Boxes}
          tone="accent"
        />
        <MetricCard
          label="Runtime environment"
          value={status.environment}
          detail="Current API execution context"
          icon={TestTube2}
        />
        <MetricCard
          label="Studio status"
          value="Active"
          detail={status.source === "api" ? "Connected to Platform API" : "Design-time snapshot"}
          icon={Code2}
          tone="warning"
        />
      </section>

      <section className="dashboard-grid">
        <article className="panel panel-capabilities">
          <div className="panel-header">
            <div>
              <span className="panel-eyebrow">Capability map</span>
              <h3>Workspace capabilities</h3>
            </div>
            <button className="text-button" onClick={() => navigate("/intelligence")}>
              View architecture <ArrowRight size={14} />
            </button>
          </div>
          <div className="capability-list">
            {status.capabilities.map(capability => (
              <div className="capability-row" key={capability.id}>
                <div className="capability-symbol">
                  <CircleDot size={17} />
                </div>
                <div className="capability-copy">
                  <strong>{capability.name}</strong>
                  <span>{capability.description}</span>
                </div>
                <span className="capability-version">v{capability.version}</span>
                <StatusPill status={capability.status} />
              </div>
            ))}
          </div>
        </article>

        <aside className="dashboard-side-stack">
          <article className="panel">
            <div className="panel-header">
              <div>
                <span className="panel-eyebrow">Quick actions</span>
                <h3>Start building</h3>
              </div>
              <small className="panel-role-badge">{role}</small>
            </div>
            <div className="quick-action-grid">
              {sortedActions.slice(0, 6).map(action => {
                const Icon = action.icon;
                return (
                  <button key={action.label} onClick={() => navigate(action.path)}>
                    <Icon size={17} />
                    <span>{action.label}</span>
                    <ArrowRight size={14} />
                  </button>
                );
              })}
            </div>
          </article>

          <article className="panel architecture-card">
            <div className="architecture-card-icon">
              <TestTube2 size={22} />
            </div>
            <div>
              <span className="panel-eyebrow">Architecture fitness</span>
              <h3>Core boundaries protected</h3>
              <p>
                Isolated capabilities, provider independence, and stable API contracts keep
                changes predictable while preserving governed execution.
              </p>
            </div>
            <div className="architecture-checks">
              <span><CheckCircle2 size={14} /> Domain isolation</span>
              <span><CheckCircle2 size={14} /> Provider independence</span>
              <span><CheckCircle2 size={14} /> Single backend topology</span>
            </div>
          </article>
        </aside>
      </section>

      <section className="panel">
        <div className="panel-header">
          <div>
            <span className="panel-eyebrow">Ready to use</span>
            <h3>Available now</h3>
          </div>
          <span className="source-chip">
            <Clock3 size={13} /> {status.source}
          </span>
        </div>
        <div className="activity-grid">
          {availableCapabilities.map(capability => (
              <article className="activity-item" key={capability.id}>
                <div className="activity-icon"><CheckCircle2 size={17} /></div>
                <div>
                  <strong>{capability.name}</strong>
                  <p>{capability.description}</p>
                  <span>{capability.status} · v{capability.version}</span>
                </div>
              </article>
          ))}
        </div>
      </section>
    </div>
  );
}
