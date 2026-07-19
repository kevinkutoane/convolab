import {
  Activity,
  ArrowRight,
  Boxes,
  CheckCircle2,
  CircleDot,
  Clock3,
  Code2,
  Database,
  GitPullRequestArrow,
  Layers3,
  ShieldCheck,
  Sparkles,
  TestTube2,
} from "lucide-react";
import { useNavigate } from "react-router-dom";
import { quickActions } from "../data/platform";
import type { PlatformStatus } from "../types/platform";
import { MetricCard } from "../components/MetricCard";
import { StatusPill } from "../components/StatusPill";

interface DashboardPageProps {
  status: PlatformStatus;
}

const recentActivity = [
  {
    title: "Studio consolidation milestone started",
    detail: "Single React Studio selected; ASP.NET Core remains the platform backend.",
    time: "Current workspace",
    icon: Layers3,
  },
  {
    title: "Intelligence Engine completed",
    detail: "Provider-neutral planning, budget, retry, fallback, streaming, and tools modelled.",
    time: "Platform Core",
    icon: Sparkles,
  },
  {
    title: "Knowledge Engine completed",
    detail: "Governed KnowledgePackage is now the only retrieval artifact consumed by prompts.",
    time: "Platform Core",
    icon: Database,
  },
  {
    title: "Architecture review v1 initiated",
    detail: "Public contracts, dependency rules, versioning, and product readiness are being formalized.",
    time: "Governance",
    icon: GitPullRequestArrow,
  },
];

export function DashboardPage({ status }: DashboardPageProps) {
  const navigate = useNavigate();
  const stableCount = status.capabilities.filter(item => item.status === "stable").length;
  const foundationCount = status.capabilities.filter(item => item.status === "foundation").length;
  const totalEvents = status.capabilities.reduce((sum, item) => sum + item.domainEvents, 0);

  return (
    <div className="page-stack">
      <section className="hero-panel">
        <div className="hero-copy">
          <div className="hero-kicker">
            <span className="live-indicator" /> Platform Core v1
          </div>
          <h2>Engineering conversational intelligence as a platform.</h2>
          <p>
            ConvoLab Studio is the visual workspace for designing, inspecting, testing,
            and operating the provider-neutral capabilities already built into Platform Core.
          </p>
          <div className="hero-actions">
            <button className="primary-button" onClick={() => navigate("/conversations")}>
              Open Conversation Explorer <ArrowRight size={16} />
            </button>
            <button className="secondary-button" onClick={() => navigate("/intelligence")}>
              Inspect Platform Core
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
          label="Architecture health"
          value={status.architectureHealth}
          detail="Clean Architecture boundaries enforced"
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
          label="Domain events"
          value={`${totalEvents}+`}
          detail="Business facts across bounded contexts"
          icon={Activity}
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
              <h3>Platform Core</h3>
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
                Domain remains framework-free. Studio consumes Platform Core through API
                contracts and contains no business orchestration.
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
            <span className="panel-eyebrow">Engineering activity</span>
            <h3>Platform evolution</h3>
          </div>
          <span className="source-chip">
            <Clock3 size={13} /> {status.source}
          </span>
        </div>
        <div className="activity-grid">
          {recentActivity.map(item => {
            const Icon = item.icon;
            return (
              <article className="activity-item" key={item.title}>
                <div className="activity-icon"><Icon size={17} /></div>
                <div>
                  <strong>{item.title}</strong>
                  <p>{item.detail}</p>
                  <span>{item.time}</span>
                </div>
              </article>
            );
          })}
        </div>
      </section>
    </div>
  );
}
