import { BookOpen, CheckCircle2, Code2, ExternalLink, Layers3 } from "lucide-react";
import { Link, useParams } from "react-router-dom";

const topics = {
  evaluation: {
    title: "Evaluation Studio",
    summary: "Govern quality with versioned scorecards, persisted evaluation runs, human review, regression cases, batches, and deterministic comparisons.",
    capabilities: [
      "Create draft scorecard versions with weighted groundedness, relevance, safety, and completeness metrics.",
      "Publish immutable scorecard versions and preserve existing scorecards through idempotent backfill.",
      "Persist simulation evaluations, review outcomes, regression cases, batch suites, and run comparisons.",
      "Continue using the singular compatibility API while consumers migrate to the canonical plural API.",
    ],
    workflow: [
      "Choose New scorecard in Evaluation Studio.",
      "Name and version the scorecard, set its quality gate, then create the draft.",
      "Publish the draft to make it available for automatic simulation evaluation.",
      "Preserve important runs as test cases, execute a batch, and compare baseline and candidate evaluations.",
      "Use the run inspector to approve an outcome or request changes.",
    ],
    endpoints: [
      "GET /api/evaluations/overview",
      "GET|POST /api/evaluations/scorecards",
      "GET /api/evaluations/scorecards/{id}",
      "POST /api/evaluations/scorecards/{id}/publish",
      "GET /api/evaluations/runs",
      "POST /api/evaluations/runs/evaluate",
      "POST /api/evaluations/runs/{id}/review",
      "GET /api/evaluations/compare",
      "GET|POST /api/evaluations/test-cases",
      "POST /api/evaluations/batches",
      "GET /api/evaluation/overview",
      "POST /api/evaluation/preview",
    ],
  },
  policies: {
    title: "Policy Center",
    summary: "Author immutable policy versions, approve and activate them atomically, and audit every runtime allow, constraint, and denial.",
    capabilities: ["Scope ordered rules globally, by environment, or by tenant.", "Enforce provider, model, ZAR budget, token, fallback, streaming, and safety decisions before provider invocation.", "Persist simulation, run, and correlation references for every decision.", "Retire the previous active version only when successor activation succeeds."],
    workflow: ["Create a draft policy and add exact, case-insensitive match attributes and constraints.", "Submit it for approval, then activate it.", "Use the decision simulator to verify rule matching.", "Run a conversation or replay and inspect its persisted decision history."],
    endpoints: ["GET|POST /api/policies", "GET|PUT /api/policies/{id}", "POST /api/policies/{id}/versions", "POST /api/policies/{id}/{submit|activate|suspend|retire}", "POST /api/policies/evaluate", "GET /api/policies/decisions"],
  },
  traces: {
    title: "Trace Explorer",
    summary: "Inspect persisted traces, span waterfalls, events, usage, ZAR cost, correlations, and safely redacted execution artifacts.",
    capabilities: ["Synchronize simulation and replay runs idempotently.", "Filter by text, status, provider, capability, and date range.", "Inspect nested spans, events, and cross-capability context.", "Keep prompts, responses, and failure details redacted until explicitly revealed."],
    workflow: ["Run a Conversation Simulator message or Replay candidate.", "Open Trace Explorer and select the execution.", "Inspect its waterfall and events, then open Artifacts.", "Reveal sensitive artifacts only when needed; redaction is restored on the next selection."],
    endpoints: ["GET /api/traces/overview", "GET /api/traces", "GET /api/traces/{id}?includeSensitive=false"],
  },
  replay: {
    title: "Replay Studio",
    summary: "Create controlled experiments from immutable baseline runs and compare governed candidates across configuration, quality, latency, tokens, and ZAR cost.",
    capabilities: ["Preserve the source run as an immutable baseline.", "Override workflow, prompt, knowledge, provider, model, temperature, tokens, and recovery mode per candidate.", "Record every candidate in Evaluation Studio and Trace Explorer.", "Complete and archive experiments without mutating prior candidates."],
    workflow: ["Run a Conversation Simulator message.", "Choose New experiment and select its baseline.", "Configure and execute one or more candidates, then inspect deterministic deltas and findings.", "Complete the experiment and archive it when review is finished."],
    endpoints: ["GET /api/replay/overview", "GET /api/replay/sources", "GET|POST /api/replay/experiments", "GET /api/replay/experiments/{id}", "POST /api/replay/experiments/{id}/candidates", "POST /api/replay/experiments/{id}/complete", "POST /api/replay/experiments/{id}/archive"],
  },
  plugins: {
    title: "Plugin Center",
    summary: "Govern persistent extension registrations, immutable versions, compatibility, capability and permission contracts, lifecycle, and operational health evidence.",
    capabilities: ["Register providers, tools, knowledge connectors, channels, evaluators, trace exporters, workflow nodes, and enterprise connectors.", "Maintain one atomic active version per logical plugin and reject incompatible or unhealthy activation.", "Persist capabilities, permissions, configuration schemas, metadata, and health history.", "Discover only active plugins with healthy or degraded evidence at runtime without loading arbitrary assemblies."],
    workflow: ["Choose Register plugin and declare its registry key, semantic version, manifest, Platform API version, capabilities, and permissions.", "Run a health check and inspect the persisted evidence.", "Activate a compatible healthy version, or create an immutable successor version and activate it atomically.", "Deactivate before editing metadata, and deprecate versions that must no longer be used."],
    endpoints: ["GET /api/plugins/overview", "GET|POST /api/plugins", "GET|PUT /api/plugins/{id}", "POST /api/plugins/{id}/versions", "POST /api/plugins/{id}/health", "POST /api/plugins/{id}/{activate|deactivate|deprecate}"],
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

const workspacePaths: Record<keyof typeof topics, string> = {
  evaluation: "/evaluation",
  policies: "/policies",
  traces: "/traces",
  replay: "/replay",
  plugins: "/plugins",
  analytics: "/analytics",
  settings: "/settings",
};

export function DocumentationPage() {
  const { topic = "evaluation" } = useParams();
  const activeTopic = topic in topics ? topic as keyof typeof topics : "evaluation";
  const definition = topics[activeTopic];

  return (
    <div className="page-stack documentation-page">
      <section className="page-heading">
        <div className="page-heading-icon"><BookOpen size={24} /></div>
        <div className="page-heading-copy"><div className="page-heading-meta"><span>ConvoLab documentation</span></div><h2>{definition.title}</h2><p>{definition.summary}</p></div>
        <Link className="secondary-button" to={workspacePaths[activeTopic]}><ExternalLink size={15} /> Open workspace</Link>
      </section>
      <div className="documentation-layout">
        <nav className="panel documentation-nav" aria-label="Documentation topics">
          <span className="panel-eyebrow">Topics</span>
          {Object.entries(topics).map(([key, item]) => <Link key={key} className={key === activeTopic ? "active" : ""} to={`/documentation/${key}`}>{item.title}</Link>)}
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
