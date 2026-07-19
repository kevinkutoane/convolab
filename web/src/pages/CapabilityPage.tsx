import {
  BookOpen,
  CheckCircle2,
  CircleDashed,
  Layers3,
  Route,
  Sparkles,
} from "lucide-react";
import { Link } from "react-router-dom";
import type { StudioPageDefinition } from "../types/platform";
import { MetricCard } from "../components/MetricCard";
import { StatusPill } from "../components/StatusPill";

interface CapabilityPageProps {
  definition: StudioPageDefinition;
  topic: string;
}

export function CapabilityPage({ definition, topic }: CapabilityPageProps) {
  const Icon = definition.icon;

  return (
    <div className="page-stack">
      <section className="page-heading">
        <div className="page-heading-icon"><Icon size={24} /></div>
        <div className="page-heading-copy">
          <div className="page-heading-meta">
            <span>{definition.eyebrow}</span>
            <StatusPill status={definition.status} />
          </div>
          <h2>{definition.title}</h2>
          <p>{definition.description}</p>
        </div>
        <Link className="primary-button" to={`/documentation/${topic}`}><BookOpen size={16} /> View capability guide</Link>
      </section>

      <section className="metrics-grid capability-metrics">
        {definition.metrics.map((metric, index) => (
          <MetricCard
            key={metric.label}
            label={metric.label}
            value={metric.value}
            detail={metric.detail}
            icon={index === 0 ? Layers3 : index === 1 ? Route : Sparkles}
            tone={index === 0 ? "accent" : "default"}
          />
        ))}
      </section>

      <section className="capability-layout">
        <article className="panel capability-workspace">
          <div className="workspace-toolbar">
            <div>
              <span className="panel-eyebrow">Workspace</span>
              <h3>{definition.title}</h3>
            </div>
            <div className="toolbar-actions">
              <Link className="secondary-button" to={`/documentation/${topic}`}><BookOpen size={15} /> Documentation</Link>
            </div>
          </div>
          <div className="empty-state">
            <div className="empty-visual">
              <Icon size={30} />
              <span className="empty-orbit orbit-one" />
              <span className="empty-orbit orbit-two" />
            </div>
            <h3>{definition.emptyTitle}</h3>
            <p>{definition.emptyDescription}</p>
            <Link className="primary-button" to={`/documentation/${topic}`}>View current availability <BookOpen size={15} /></Link>
          </div>
        </article>

        <aside className="panel inspector-panel">
          <div className="panel-header">
            <div>
              <span className="panel-eyebrow">Capability contract</span>
              <h3>Platform guarantees</h3>
            </div>
          </div>
          <div className="contract-list">
            {definition.activities.map(activity => (
              <div key={activity}>
                <CheckCircle2 size={16} />
                <span>{activity}</span>
              </div>
            ))}
          </div>
          <div className="contract-divider" />
          <div className="contract-meta">
            <span><CircleDashed size={14} /> Public contracts</span>
            <strong>Domain + Application</strong>
          </div>
          <div className="contract-meta">
            <span><CircleDashed size={14} /> Infrastructure</span>
            <strong>Adapter only</strong>
          </div>
          <div className="contract-meta">
            <span><CircleDashed size={14} /> Studio responsibility</span>
            <strong>Consume, never orchestrate</strong>
          </div>
        </aside>
      </section>
    </div>
  );
}
