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

interface DashboardPageProps {
  status: PlatformStatus;
}

export function DashboardPage({ status }: DashboardPageProps) {
  const navigate = useNavigate();
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
            </div>
            <div className="quick-action-grid">
              {quickActions.slice(0, 6).map(action => {
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
