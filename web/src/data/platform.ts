import {
  Activity,
  BarChart3,
  BookOpenCheck,
  Bot,
  BrainCircuit,
  Braces,
  Boxes,
  Database,
  FileKey2,
  GitBranch,
  LayoutDashboard,
  MessageSquareText,
  PlugZap,
  RotateCcw,
  Settings,
  UsersRound,
  ShieldCheck,
  Sparkles,
  Workflow,
} from "lucide-react";
import type {
  NavigationItem,
  PlatformStatus,
  StudioPageDefinition,
} from "../types/platform";

export const designTimePlatformStatus: PlatformStatus = {
  platformName: "ConvoLab Platform",
  productName: "ConvoLab Studio",
  version: "1.0.0-alpha.12",
  environment: "Development",
  architectureHealth: "Healthy",
  apiHealth: "Unknown",
  generatedAt: new Date().toISOString(),
  source: "design-time snapshot",
  capabilities: [
    {
      id: "conversation",
      name: "Conversation Engine",
      description: "Lifecycle, sessions, participants, memory, and timeline.",
      status: "stable",
      version: "1.0",
      domainEvents: 16,
    },
    {
      id: "workflow",
      name: "Workflow Engine",
      description: "Versioned workflow definitions and governed executions.",
      status: "stable",
      version: "1.0",
      domainEvents: 12,
    },
    {
      id: "prompt",
      name: "Prompt Engine",
      description: "Governed prompt assets, versions, rendering, and experiments.",
      status: "stable",
      version: "1.0",
      domainEvents: 10,
    },
    {
      id: "knowledge",
      name: "Knowledge Engine",
      description: "Governed retrieval, packages, citations, and connectors.",
      status: "stable",
      version: "1.0",
      domainEvents: 13,
    },
    {
      id: "intelligence",
      name: "Intelligence Engine",
      description: "Provider-neutral planning, budgets, tools, and fallback.",
      status: "stable",
      version: "1.0",
      domainEvents: 14,
    },
    {
      id: "policy",
      name: "Policy",
      description: "Central governance and runtime decision constraints.",
      status: "stable",
      version: "1.0",
      domainEvents: 8,
    },
    {
      id: "evaluation",
      name: "Evaluation",
      description: "Persisted scorecards, quality gates, safety, relevance, and groundedness telemetry.",
      status: "stable",
      version: "1.0",
      domainEvents: 5,
    },
    {
      id: "tracing",
      name: "Tracing",
      description: "Distributed trace model, spans, events, and artifacts.",
      status: "stable",
      version: "1.0",
      domainEvents: 7,
    },
    {
      id: "replay",
      name: "Replay Studio",
      description: "Controlled re-execution, immutable baselines, candidate comparisons, and findings.",
      status: "stable",
      version: "1.0",
      domainEvents: 3,
    },
    {
      id: "plugins",
      name: "Plugin Engine",
      description: "Persistent extension registry, immutable versions, compatibility, lifecycle, health, and capability contracts.",
      status: "stable",
      version: "1.0",
      domainEvents: 4,
    },
    {
      id: "workspace-identity",
      name: "Workspace, Identity and Access",
      description: "Secure authentication, workspace isolation, fixed RBAC, service identities, and audit.",
      status: "active",
      version: "1.0",
      domainEvents: 8,
    },
    {
      id: "studio",
      name: "ConvoLab Studio",
      description: "Functional conversation simulation, governance, evaluation, tracing, replay, and plugin workspace.",
      status: "active",
      version: "0.12",
      domainEvents: 0,
    },
  ],
};

export const navigationItems: NavigationItem[] = [
  {
    label: "Dashboard",
    path: "/",
    icon: LayoutDashboard,
    description: "Platform health and engineering overview",
  },
  {
    label: "Conversation Simulator",
    path: "/conversations",
    icon: MessageSquareText,
    description: "Run and inspect end-to-end conversation executions",
    status: "stable",
  },
  {
    label: "Workflow Designer",
    path: "/workflows",
    icon: Workflow,
    description: "Compose reusable workflow definitions",
    status: "stable",
  },
  {
    label: "Prompt Studio",
    path: "/prompts",
    icon: Braces,
    description: "Govern prompt versions and experiments",
    status: "stable",
  },
  {
    label: "Knowledge Studio",
    path: "/knowledge",
    icon: Database,
    description: "Manage governed enterprise knowledge",
    status: "stable",
  },
  {
    label: "Intelligence Center",
    path: "/intelligence",
    icon: BrainCircuit,
    description: "Inspect execution plans and provider decisions",
    status: "stable",
  },
  {
    label: "Policy Center",
    path: "/policies",
    icon: ShieldCheck,
    description: "Manage platform governance policies",
    status: "stable",
  },
  {
    label: "Evaluation Studio",
    path: "/evaluation",
    icon: BookOpenCheck,
    description: "Inspect quality gates and safety evaluations",
    status: "stable",
  },
  {
    label: "Trace Explorer",
    path: "/traces",
    icon: Activity,
    description: "Debug cross-capability execution traces",
    status: "stable",
  },
  {
    label: "Replay Studio",
    path: "/replay",
    icon: RotateCcw,
    description: "Re-run conversations with controlled changes",
    status: "stable",
  },
  {
    label: "Plugin Center",
    path: "/plugins",
    icon: PlugZap,
    description: "Extend providers, tools, channels, and connectors",
    status: "stable",
  },
  {
    label: "Workspace & Access",
    path: "/workspace",
    icon: UsersRound,
    description: "Manage members, roles, service identities, and audit",
    status: "active",
  },
  {
    label: "Analytics",
    path: "/analytics",
    icon: BarChart3,
    description: "Monitor quality, cost, latency, and adoption",
    status: "planned",
  },
  {
    label: "Settings",
    path: "/settings",
    icon: Settings,
    description: "Workspace and environment configuration",
  },
];

export const studioPages: Record<string, StudioPageDefinition> = {
  conversations: {
    title: "Conversation Explorer",
    eyebrow: "Conversation Engine",
    description:
      "Inspect lifecycle transitions, participants, sessions, memory snapshots, and the business timeline for every conversation.",
    icon: MessageSquareText,
    status: "stable",
    metrics: [
      { label: "Active", value: "0", detail: "No live conversations" },
      { label: "Sessions", value: "0", detail: "Awaiting a runtime adapter" },
      { label: "Memory", value: "Ready", detail: "Provider-neutral contracts" },
    ],
    activities: [
      "Conversation aggregate owns all lifecycle transitions",
      "Messages remain immutable after creation",
      "Timeline is separated from engineering traces",
    ],
    emptyTitle: "No conversations recorded yet",
    emptyDescription:
      "Connect a channel or launch a simulation to create the first governed conversation.",
    primaryAction: "Create simulation",
  },
  workflows: {
    title: "Workflow Designer",
    eyebrow: "Workflow Engine",
    description:
      "Design reusable, versioned business workflows without coupling them to a channel, provider, or model.",
    icon: GitBranch,
    status: "stable",
    metrics: [
      { label: "Definitions", value: "0", detail: "No workflows published" },
      { label: "Executions", value: "0", detail: "No active executions" },
      { label: "State model", value: "Valid", detail: "Transitions enforced" },
    ],
    activities: [
      "Workflow definitions are separated from runtime executions",
      "Execution state transitions are domain-enforced",
      "Intelligence orchestration remains outside Workflow",
    ],
    emptyTitle: "No workflow definitions",
    emptyDescription:
      "Create the first workflow definition to model a customer or engineering journey.",
    primaryAction: "New workflow",
  },
  prompts: {
    title: "Prompt Studio",
    eyebrow: "Prompt Engine",
    description:
      "Create governed prompt assets with composition, versioning, approval, comparison, and rollback.",
    icon: Braces,
    status: "stable",
    metrics: [
      { label: "Prompts", value: "0", detail: "No governed assets" },
      { label: "Versions", value: "0", detail: "Immutable version history" },
      { label: "Experiments", value: "Ready", detail: "Replay-compatible model" },
    ],
    activities: [
      "Prompt edits create immutable versions",
      "Composition remains provider independent",
      "Experiments can reference conversations and evaluations",
    ],
    emptyTitle: "No prompts created",
    emptyDescription:
      "Create a governed prompt asset and prepare it for future replay experiments.",
    primaryAction: "Create prompt",
  },
  knowledge: {
    title: "Knowledge Studio",
    eyebrow: "Knowledge Engine",
    description:
      "Govern enterprise sources, collections, documents, retrieval strategies, and sealed knowledge packages.",
    icon: Database,
    status: "stable",
    metrics: [
      { label: "Sources", value: "0", detail: "No connectors registered" },
      { label: "Collections", value: "0", detail: "No business domains" },
      { label: "Packages", value: "Ready", detail: "Cited and token-aware" },
    ],
    activities: [
      "Prompt Engine consumes only sealed KnowledgePackage artifacts",
      "Published documents are immutable",
      "Classification and retention rules are enforced in-domain",
    ],
    emptyTitle: "No knowledge sources",
    emptyDescription:
      "Register SharePoint, document, API, database, or custom connector abstractions.",
    primaryAction: "Register source",
  },
  intelligence: {
    title: "Intelligence Center",
    eyebrow: "Intelligence Engine",
    description:
      "Understand how ConvoLab plans intelligent execution across providers, models, budgets, capabilities, tools, and fallback paths.",
    icon: BrainCircuit,
    status: "stable",
    metrics: [
      { label: "Providers", value: "0", detail: "No adapters installed" },
      { label: "Models", value: "0", detail: "Capability catalogue ready" },
      { label: "Planner", value: "Ready", detail: "Provider-neutral contracts" },
    ],
    activities: [
      "Conversation never selects a provider or model",
      "Execution plans are immutable",
      "Cost, token, latency, retry, and fallback are first-class concepts",
    ],
    emptyTitle: "No intelligence providers",
    emptyDescription:
      "Install a provider adapter after Platform Core integration contracts are finalized.",
    primaryAction: "View provider contract",
  },
  policies: {
    title: "Policy Center",
    eyebrow: "Policy Capability",
    description:
      "Centralize model restrictions, budgets, approvals, compliance rules, and runtime governance decisions.",
    icon: ShieldCheck,
    status: "stable",
    metrics: [
      { label: "Policies", value: "0", detail: "No policies published" },
      { label: "Decisions", value: "0", detail: "No runtime evaluations" },
      { label: "Coverage", value: "Active", detail: "Runtime enforcement enabled" },
    ],
    activities: [
      "Governance is separated from execution",
      "Policies can evolve independently of engines",
      "Tenant and environment scopes are represented in policy data",
    ],
    emptyTitle: "No platform policies",
    emptyDescription:
      "Define the first governance policy for model access, spend, safety, or retrieval.",
    primaryAction: "Create policy",
  },
  evaluations: {
    title: "Evaluation Studio",
    eyebrow: "Evaluation Capability",
    description:
      "Measure quality, groundedness, safety, completeness, relevance, and policy compliance.",
    icon: BookOpenCheck,
    status: "stable",
    metrics: [
      { label: "Evaluations", value: "0", detail: "No runs recorded" },
      { label: "Scorecards", value: "0", detail: "No evaluation profiles" },
      { label: "Thresholds", value: "Policy", detail: "Governed externally" },
    ],
    activities: [
      "Evaluation references remain outside Conversation ownership",
      "Thresholds can be supplied by Policy",
      "Results can feed replay comparisons",
    ],
    emptyTitle: "No evaluation results",
    emptyDescription:
      "Evaluation results will appear after a simulated or provider-backed execution.",
    primaryAction: "Create scorecard",
  },
  traces: {
    title: "Trace Explorer",
    eyebrow: "Tracing Capability",
    description:
      "Debug conversational execution using traces, nested spans, events, metrics, correlations, and artifacts.",
    icon: Activity,
    status: "stable",
    metrics: [
      { label: "Traces", value: "0", detail: "No runtime data" },
      { label: "Spans", value: "0", detail: "OpenTelemetry-aligned model" },
      { label: "Correlations", value: "Ready", detail: "Cross-capability references" },
    ],
    activities: [
      "Trace history is separate from the business timeline",
      "Nested spans and parent relationships are supported",
      "Artifacts can preserve prompts, packages, and outputs",
    ],
    emptyTitle: "No traces recorded",
    emptyDescription:
      "Run a simulation to create the first persisted trace.",
    primaryAction: "Open Trace Explorer",
  },
  replay: {
    title: "Replay Studio",
    eyebrow: "Signature Product",
    description:
      "Re-run recorded conversations with controlled prompt, knowledge, workflow, model, provider, and policy changes.",
    icon: RotateCcw,
    status: "stable",
    metrics: [
      { label: "Replays", value: "0", detail: "Awaiting a baseline run" },
      { label: "Comparisons", value: "0", detail: "Side-by-side evaluation" },
      { label: "Time travel", value: "Designed", detail: "Platform contracts aligned" },
    ],
    activities: [
      "Replay consumes immutable simulation snapshots",
      "Results compare quality, latency, tokens, and ZAR cost",
      "Every candidate is evaluated, traced, and policy governed",
    ],
    emptyTitle: "No replay sessions",
    emptyDescription:
      "Run a simulation to create the first immutable replay baseline.",
    primaryAction: "Create experiment",
  },
  plugins: {
    title: "Plugin Center",
    eyebrow: "Plugin Engine",
    description:
      "Extend ConvoLab with providers, tools, knowledge connectors, channels, evaluators, and enterprise integrations.",
    icon: PlugZap,
    status: "stable",
    metrics: [
      { label: "Registered", value: "4", detail: "Built-in adapter contracts" },
      { label: "Healthy", value: "4", detail: "Runtime evidence available" },
      { label: "API", value: "1.0", detail: "Compatibility boundary" },
    ],
    activities: [
      "Plugins expose capabilities rather than concrete types",
      "Health and lifecycle are first-class concepts",
      "Provider implementations remain outside Platform Core",
    ],
    emptyTitle: "No plugins match",
    emptyDescription:
      "Adjust the registry filters or register a compatible extension contract.",
    primaryAction: "Register plugin",
  },
  analytics: {
    title: "Platform Analytics",
    eyebrow: "Operations",
    description:
      "Monitor adoption, quality, containment, latency, cost, reliability, and capability health.",
    icon: BarChart3,
    status: "planned",
    metrics: [
      { label: "Events", value: "0", detail: "No telemetry connected" },
      { label: "Cost", value: "R0.00", detail: "No provider usage" },
      { label: "Quality", value: "—", detail: "No evaluated responses" },
    ],
    activities: [
      "Analytics will consume normalized platform telemetry",
      "Cost will be tracked by conversation and workflow",
      "Quality trends will integrate Evaluation results",
    ],
    emptyTitle: "No analytics data",
    emptyDescription:
      "Connect runtime telemetry to start monitoring the platform across environments.",
    primaryAction: "View metrics roadmap",
  },
  settings: {
    title: "Studio Settings",
    eyebrow: "Workspace",
    description:
      "Configure Studio appearance, API connectivity, environment context, and developer preferences.",
    icon: Settings,
    status: "active",
    metrics: [
      { label: "Environment", value: "Local", detail: "Design-time workspace" },
      { label: "API", value: "Auto", detail: "Uses /api proxy" },
      { label: "Theme", value: "System", detail: "Dark-first experience" },
    ],
    activities: [
      "Business configuration remains outside the UI",
      "Secrets are never stored in browser state",
      "Environment settings will be supplied by deployment adapters",
    ],
    emptyTitle: "Workspace settings",
    emptyDescription:
      "Use the theme control in the top bar. Environment configuration will arrive with deployment support.",
    primaryAction: "Open documentation",
  },
};

export const quickActions = [
  { label: "Open simulator", path: "/conversations", icon: MessageSquareText },
  { label: "Open Prompt Studio", path: "/prompts", icon: Braces },
  { label: "Open Knowledge Studio", path: "/knowledge", icon: Database },
  { label: "Open Intelligence Center", path: "/intelligence", icon: Sparkles },
  { label: "View capability map", path: "/", icon: Boxes },
  { label: "Review Policy roadmap", path: "/policies", icon: FileKey2 },
  { label: "Provider planning", path: "/intelligence", icon: Bot },
];
