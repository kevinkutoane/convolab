import { BookOpen, CheckCircle2, Code2, ExternalLink, Layers3 } from "lucide-react";
import { Link, useParams } from "react-router-dom";

const topics = {
  evaluation: {
    title: "Evaluation Studio",
    summary: "Create reusable scorecards, inspect persisted simulation telemetry, and preview quality-gate decisions before release.",
    capabilities: [
      "Persist named scorecards with groundedness, relevance, safety, and overall thresholds.",
      "Select a saved scorecard in the policy sandbox and evaluate sample scores against it.",
      "Inspect pass rates, failed gates, seven-day trends, and individual simulator runs.",
      "Use environment settings as the default policy when no saved scorecard is selected.",
    ],
    workflow: [
      "Choose New scorecard in Evaluation Studio.",
      "Name the scorecard, describe its intended use, and set its four quality gates.",
      "Save it, select its card, then adjust the sandbox sample scores.",
      "Choose Evaluate sample to run the server-side scoring logic with that scorecard.",
    ],
    endpoints: [
      "GET /api/evaluation/overview",
      "GET /api/evaluation/runs?limit=100",
      "GET /api/evaluation/scorecards",
      "POST /api/evaluation/scorecards",
      "POST /api/evaluation/preview",
    ],
  },
  policies: {
    title: "Policy Center",
    summary: "Policy contracts centralize model restrictions, budgets, approvals, compliance rules, and runtime governance decisions.",
    capabilities: ["Policy remains a foundation capability.", "Runtime governance belongs outside execution engines.", "Published policy authoring is not enabled yet."],
    workflow: ["Use Evaluation scorecards for currently supported quality thresholds.", "Configure execution budgets in Intelligence Center.", "Treat Policy Center authoring controls as unavailable until its persistence milestone lands."],
    endpoints: [],
  },
  traces: {
    title: "Trace Explorer",
    summary: "Tracing contracts define cross-capability spans, events, correlations, and artifacts.",
    capabilities: ["Simulation run traces are available from Conversation Simulator.", "A standalone persisted trace explorer remains a foundation capability."],
    workflow: ["Run a conversation simulation.", "Open its run inspector to review the generated trace timeline."],
    endpoints: [],
  },
  replay: {
    title: "Replay Studio",
    summary: "Replay will compare immutable execution snapshots across prompt, knowledge, workflow, model, and policy changes.",
    capabilities: ["Replay contracts are planned.", "Production replay authoring is not currently enabled."],
    workflow: ["Use Conversation Simulator retry and fallback controls for the currently supported recovery workflow."],
    endpoints: [],
  },
  plugins: {
    title: "Plugin Registry",
    summary: "Plugin contracts will support providers, tools, connectors, channels, and evaluators without coupling them to Platform Core.",
    capabilities: ["Provider adapters are isolated in Infrastructure.", "Plugin installation and lifecycle management are not currently enabled."],
    workflow: ["Configure supported providers through environment settings and Intelligence Center."],
    endpoints: [],
  },
  analytics: {
    title: "Platform Analytics",
    summary: "Analytics will consume normalized execution, cost, latency, and quality telemetry.",
    capabilities: ["Intelligence Center exposes current execution and cost telemetry.", "Evaluation Studio exposes current quality telemetry."],
    workflow: ["Use Intelligence Center for provider operations.", "Use Evaluation Studio for quality trends."],
    endpoints: [],
  },
  settings: {
    title: "Studio Settings",
    summary: "Studio behavior is currently controlled by theme and deployment configuration.",
    capabilities: ["Theme selection persists in the browser.", "API, database, provider, and Evaluation configuration are supplied by deployment settings."],
    workflow: ["Use the top-bar theme control for appearance.", "Use appsettings or environment variables for server configuration."],
    endpoints: [],
  },
} as const;

export function DocumentationPage() {
  const { topic = "evaluation" } = useParams();
  const definition = topics[topic as keyof typeof topics] ?? topics.evaluation;

  return (
    <div className="page-stack documentation-page">
      <section className="page-heading">
        <div className="page-heading-icon"><BookOpen size={24} /></div>
        <div className="page-heading-copy"><div className="page-heading-meta"><span>ConvoLab documentation</span></div><h2>{definition.title}</h2><p>{definition.summary}</p></div>
        <Link className="secondary-button" to={topic === "evaluation" ? "/evaluation" : `/${topic}`}><ExternalLink size={15} /> Open workspace</Link>
      </section>
      <div className="documentation-layout">
        <nav className="panel documentation-nav" aria-label="Documentation topics">
          <span className="panel-eyebrow">Topics</span>
          {Object.entries(topics).map(([key, item]) => <Link key={key} className={key === topic ? "active" : ""} to={`/documentation/${key}`}>{item.title}</Link>)}
        </nav>
        <main className="documentation-content">
          <section className="panel documentation-section"><div className="panel-header"><div><span className="panel-eyebrow">Available now</span><h3>Capabilities</h3></div><Layers3 size={19} /></div><div className="documentation-list">{definition.capabilities.map(item => <p key={item}><CheckCircle2 size={16} /><span>{item}</span></p>)}</div></section>
          <section className="panel documentation-section"><div className="panel-header"><div><span className="panel-eyebrow">How to use it</span><h3>Workflow</h3></div><BookOpen size={19} /></div><ol>{definition.workflow.map(item => <li key={item}>{item}</li>)}</ol></section>
          {definition.endpoints.length > 0 && <section className="panel documentation-section"><div className="panel-header"><div><span className="panel-eyebrow">HTTP contract</span><h3>API endpoints</h3></div><Code2 size={19} /></div><div className="documentation-endpoints">{definition.endpoints.map(item => <code key={item}>{item}</code>)}</div></section>}
        </main>
      </div>
    </div>
  );
}
