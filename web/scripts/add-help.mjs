import fs from 'fs';
import path from 'path';

const pagesDir = "c:/Users/W1022804/convolab-main/web/src/pages";

const pages = [
  "AnalyticsPage.tsx",
  "CapabilityPage.tsx",
  "ConversationSimulatorPage.tsx",
  "DashboardPage.tsx",
  "DocumentationPage.tsx",
  "EvaluationStudioPage.tsx",
  "IntelligenceCenterPage.tsx",
  "KnowledgeStudioPage.tsx",
  "LoginPage.tsx",
  "NotFoundPage.tsx",
  "OperationsPage.tsx",
  "PluginCenterPage.tsx",
  "PolicyCenterPage.tsx",
  "PromptStudioPage.tsx",
  "ReplayStudioPage.tsx",
  "SettingsPage.tsx",
  "TraceExplorerPage.tsx",
  "WorkflowDesignerPage.tsx",
  "WorkspacePage.tsx",
];

const detailedHelpContent = {
  "AnalyticsPage.tsx": {
    title: "Platform Analytics",
    description: "A role-filtered analytics hub that shows usage, cost, quality, governance, performance, and adoption metrics. The tabs you see depend on your role — Administrators see all tabs, while Reviewers see Quality and Adoption but not Cost.",
    usageSteps: [
      "Select a time window using the 'Last 30 / 60 / 90 days' selector in the filter bar.",
      "Switch between tabs (Overview, Usage, Cost & Budget, Quality, Governance, Performance, Adoption, Events, Exports).",
      "Use the filter dropdowns to narrow data by Provider, Model, Workflow, Prompt, or Knowledge Collection.",
      "On the 'Exports' tab (Administrators only), click 'Create Export' to generate a downloadable report.",
      "On the 'Events' tab, click any event row to expand its full detail payload."
    ],
    examples: [
      "Cost monitoring: Navigate to the 'Cost & Budget' tab to see ZAR spend vs your configured budget threshold.",
      "Quality check: Open the 'Quality' tab after updating a prompt to verify conversation scores haven't dropped.",
      "Governance audit: The 'Governance' tab shows every policy trigger, violation, and enforcement action."
    ],
    expectedOutput: "Live metric cards, time-series charts, and filterable event logs that give you a complete picture of platform usage and health.",
    aiLayerRole: "The AI layer generates the analytics events that populate these charts — every token consumed, policy evaluated, and knowledge retrieval performed is automatically recorded as a structured event."
  },
  "CapabilityPage.tsx": {
    title: "Capability Explorer",
    description: "Shows the detailed specification of a single platform capability (e.g., Conversation Engine, Knowledge Engine). This page is reached by clicking a capability from the Dashboard or Intelligence Center.",
    usageSteps: [
      "Review the capability's domain events, version, and current health status.",
      "Click through the tabs to see the API contract, configuration schema, and dependency map.",
      "Use the 'Open in Intelligence Center' link to see live execution metrics for this capability."
    ],
    examples: [
      "Checking the Conversation Engine to see all 16 domain events it emits.",
      "Verifying that the Knowledge Engine is on version 1.0 and status 'stable' before deploying."
    ],
    expectedOutput: "A complete technical specification for a single capability boundary, used for architectural review and health monitoring.",
    aiLayerRole: "Each capability boundary isolates a specific AI function (e.g., Knowledge retrieval, Prompt rendering) to ensure provider independence and governed execution."
  },
  "ConversationSimulatorPage.tsx": {
    title: "Conversation Simulator",
    description: "An interactive chat environment where you test your AI configuration end-to-end before deploying it. It shows you not just the AI's response, but exactly how it arrived at that answer — including which knowledge was retrieved, which prompt was used, and the full execution trace.",
    usageSteps: [
      "Click '+ New simulation' to create a test session and give it a name.",
      "Select your Workflow, Prompt Version, and Knowledge Collection from the dropdowns in the configuration panel.",
      "Type a message in the chat input and press Send. Use the starter messages as quick-start suggestions.",
      "After the AI responds, click the 'Inspector' tab on the right to see the Trace, retrieved Knowledge chunks, and rendered Prompt.",
      "Use 'Replay' on any existing simulation to re-run it with different settings for comparison.",
      "Toggle 'Adversarial' mode to test how the AI handles edge cases and policy violations."
    ],
    examples: [
      "Testing a claims workflow: Ask 'Can I claim for hail damage?' and verify the AI cites the correct policy document.",
      "Policy testing: Try asking something the AI should refuse (e.g., competitor comparisons) and confirm the policy block appears in the trace."
    ],
    expectedOutput: "A complete chat transcript with a side-by-side inspector showing the AI's internal reasoning, retrieved context, and execution metrics (latency, token count, cost) for every turn.",
    aiLayerRole: "The AI executes the full conversation pipeline — intent classification, knowledge retrieval, prompt rendering, response generation, and policy evaluation — in real-time for every message you send."
  },
  "DashboardPage.tsx": {
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
  },
  "DocumentationPage.tsx": {
    title: "Platform Documentation",
    description: "The built-in reference hub for ConvoLab Studio, containing API specifications, SDK guides, and conceptual documentation for every capability.",
    usageSteps: [
      "Use the left sidebar to navigate between documentation topics.",
      "Use the Command Palette (Ctrl+K / Cmd+K) and type a keyword to jump directly to a specific topic.",
      "Copy code snippets by clicking the copy icon on any code block.",
      "Use the breadcrumb trail at the top to navigate back to parent sections."
    ],
    examples: [
      "Looking up the REST API payload for creating a new simulation.",
      "Finding the list of available policy domains when writing a custom governance rule."
    ],
    expectedOutput: "Structured technical documentation, code examples, and conceptual guides that help you use the platform correctly.",
    aiLayerRole: "Documentation covers how the AI layer processes requests — from how knowledge packages are assembled, to how execution plans are generated, to how policy evaluation works at runtime."
  },
  "EvaluationStudioPage.tsx": {
    title: "Evaluation Studio",
    description: "A quality assurance workspace for running automated batch tests against your AI. Create scorecards that define what 'good' looks like, then run them against datasets to detect regressions before they reach users.",
    usageSteps: [
      "Click '+ New scorecard' to define your quality criteria (name, description, and pass/fail threshold, e.g., 0.85 = 85%).",
      "Once a scorecard is created, click 'Publish' to make it the active quality gate.",
      "Click '+ Add test case' to add individual test scenarios (input + expected verdict) to a scorecard.",
      "Click 'Run batch' to execute all test cases against the current AI configuration.",
      "Select any run from the list on the left to see detailed results — pass/fail per case, aggregate score, and comparison to baseline.",
      "Use 'Compare runs' to diff two run IDs side-by-side to see exactly what changed between versions."
    ],
    examples: [
      "Regression testing: After updating a prompt, run your 'Customer Support Quality' scorecard and compare the new run against last week's baseline.",
      "Safety gate: Create a scorecard with a 0.95 threshold for toxicity checks before promoting a prompt to production."
    ],
    expectedOutput: "A scored report card for each batch run showing aggregate pass rate, individual test case results, and a comparison to previous runs.",
    aiLayerRole: "The AI acts as a judge — it reads each test case, executes the conversation, and scores the result against your scorecard criteria using LLM-as-a-judge evaluation."
  },
  "IntelligenceCenterPage.tsx": {
    title: "Intelligence Center",
    description: "The observability and planning hub for AI model execution. It shows live provider health, model capabilities, execution metrics (latency, cost, token usage), and lets you preview and test execution plans before running real conversations.",
    usageSteps: [
      "Review the metric cards at the top: total executions, average latency, budget utilisation, and success rate.",
      "In the 'Providers' section, click 'Test' next to any provider to run a live health check — it will confirm the provider is responding and report latency.",
      "In 'Execution plan preview', configure a hypothetical plan (provider, model, token estimates, fallback settings) and click 'Preview plan' to see the estimated cost and routing decision.",
      "Review 'Recent executions' to see actual AI calls with their provider, latency, tokens used, and cost.",
      "Check the 'Daily usage' bar chart to see execution volume trends over the last 30 days."
    ],
    examples: [
      "Before a high-volume event: Run 'Preview plan' with your expected token counts to estimate cost and confirm fallback is configured.",
      "After a provider outage: Click 'Test' on your primary provider to confirm it has recovered before re-enabling it."
    ],
    expectedOutput: "Live execution telemetry, provider health status, and a cost/routing preview for any hypothetical execution plan configuration.",
    aiLayerRole: "This screen shows the internals of the Intelligence Engine — the component responsible for selecting the right model, enforcing budget constraints, managing retries, and handling provider fallback."
  },
  "KnowledgeStudioPage.tsx": {
    title: "Knowledge Studio",
    description: "The governance workspace for the documents your AI uses to answer questions. Organise documents into Collections, then move each document through a controlled lifecycle before it can be retrieved during conversations.",
    usageSteps: [
      "Click '+ New collection' to create a named knowledge base (e.g., 'Claims Policies'). Set an owner, description, and classification (Public / Internal / Confidential / Restricted).",
      "Select a collection from the left list, then click 'Upload document' to add a PDF, DOCX, TXT, or Markdown file.",
      "Once uploaded, click 'Process' on the document to chunk and embed it. This makes the content searchable.",
      "After processing, click 'Submit' then 'Approve' then 'Publish' to move the document through the approval workflow.",
      "Use the 'Retrieval test' panel at the bottom to type a query and verify your published documents are being retrieved correctly.",
      "Click 'Chunks' on any processed document to inspect how it was split and how many tokens each chunk uses."
    ],
    examples: [
      "Uploading a motor insurance policy PDF, processing it, and testing that 'Can I claim for hail damage?' retrieves the correct policy clause.",
      "Checking chunk quality: If retrieval results are poor, inspect the chunks to see if the document was split at sensible boundaries."
    ],
    expectedOutput: "A collection of published, vectorised documents that the AI can retrieve from during conversations. The 'Retrieval test' shows ranked results with confidence scores and matching terms.",
    aiLayerRole: "The AI Engine chunks your documents into manageable segments, converts each chunk into a vector embedding using the configured embedding model, and performs semantic similarity search at runtime to retrieve the most relevant context."
  },
  "LoginPage.tsx": {
    title: "Sign In",
    description: "The authentication entry point for ConvoLab Studio. Access is controlled by your organisation's identity provider.",
    usageSteps: [
      "Enter your email and password, or use the SSO button to sign in via your corporate identity provider (e.g., Microsoft Entra, Okta).",
      "If Multi-Factor Authentication is required, complete the MFA challenge after your initial credentials.",
      "After sign-in, you will be directed to your active workspace. If you have access to multiple workspaces, you can switch them from the top bar."
    ],
    examples: [
      "First-time login: Use your work email and the temporary password sent by your Administrator.",
      "SSO login: Click 'Sign in with SSO' and enter your corporate email to be redirected to your organisation's login portal."
    ],
    expectedOutput: "A valid authenticated session scoped to your assigned workspaces and role-based permissions.",
    aiLayerRole: "Authentication is handled by standard secure protocols. The AI layer is not involved in sign-in, but once authenticated, your role determines which AI capabilities and data you can access."
  },
  "NotFoundPage.tsx": {
    title: "Page Not Found",
    description: "The URL you navigated to does not exist in ConvoLab Studio.",
    usageSteps: [
      "Click 'Return to Dashboard' to go back to the main platform hub.",
      "Use the Command Palette (Ctrl+K / Cmd+K) to search for the feature you were looking for.",
      "Check the URL for typos — Studio paths use lowercase with hyphens (e.g., /conversations, /knowledge)."
    ],
    examples: [
      "If you typed '/prompt-studio', the correct path is '/prompts'.",
      "If you followed a broken link, report it to your workspace Administrator."
    ],
    expectedOutput: "Navigation back to a valid area of the Studio.",
    aiLayerRole: "N/A — this page is shown when a route cannot be matched."
  },
  "OperationsPage.tsx": {
    title: "Platform Operations",
    description: "An administrator-only control centre for infrastructure monitoring, backup management, deployment approvals, and platform-wide safe mode. Only Platform Administrators can access this page.",
    usageSteps: [
      "The 'Overview' tab shows system readiness, dependency health (API, database, cache, queues), active worker status, and the safe mode toggle.",
      "Toggle 'Safe Mode' in the Overview tab to block all external AI execution and plugin activity platform-wide — useful during incidents.",
      "The 'Deployments' tab lists pending and approved deployments. Click 'Approve' to promote a deployment to production.",
      "The 'Backups' tab lets you trigger a manual database backup, list available restore points, and initiate a recovery.",
      "The 'Auth' tab shows authentication evidence and secret provider health.",
      "The 'Telemetry' tab shows the analytics pipeline status and event processing throughput.",
      "The 'Build' tab shows the CI/CD build evidence and test coverage for the current deployed version."
    ],
    examples: [
      "Pre-maintenance: Enable Safe Mode to stop all AI execution while you perform infrastructure changes.",
      "Disaster recovery: Go to Backups → 'List available backups' → select a restore point → click 'Restore'."
    ],
    expectedOutput: "Live infrastructure health metrics, backup management controls, deployment approval workflow, and a platform-wide safe mode emergency stop.",
    aiLayerRole: "The Operations page lets Administrators monitor the infrastructure that hosts the AI layer — including worker queues processing AI jobs and the analytics pipeline that records execution telemetry."
  },
  "PluginCenterPage.tsx": {
    title: "Plugin Center",
    description: "Register, configure, activate, and monitor third-party extensions to the ConvoLab platform. Plugins can extend the system with new AI providers, workflow node types, knowledge connectors, evaluation scoring models, and more.",
    usageSteps: [
      "Click '+ Register plugin' to add a new plugin by providing its manifest URL, entry point, and configuration schema.",
      "Select a plugin from the left list to view its full detail — capabilities, permissions, configuration, and health status.",
      "Click 'Activate' to enable a registered plugin for use across the platform. Click 'Deactivate' to suspend it without removing it.",
      "Click 'Health check' to run a live liveness probe against the plugin's endpoint.",
      "Use the filter bar to find plugins by category: Provider, Tool, KnowledgeConnector, Channel, Evaluator, TraceExporter, WorkflowNode, or EnterpriseConnector.",
      "Click 'Edit configuration' to update the plugin's runtime settings (e.g., API endpoint, credentials)."
    ],
    examples: [
      "Adding a Provider plugin: Register a custom OpenAI-compatible LLM endpoint so it appears as an option in the Intelligence Center.",
      "Adding a KnowledgeConnector plugin: Connect SharePoint so documents sync automatically into a Knowledge Collection.",
      "Adding an Evaluator plugin: Register a custom scoring model that grades conversations using your domain-specific rubric."
    ],
    expectedOutput: "A registered, activated, and health-checked plugin that adds new capabilities to the platform's provider, knowledge, or evaluation layers.",
    aiLayerRole: "Provider plugins directly extend which AI models the Intelligence Engine can use. Evaluator plugins add new scoring criteria to the Evaluation Studio. All plugin types integrate with the platform's governed execution layer."
  },
  "PolicyCenterPage.tsx": {
    title: "Policy Center",
    description: "The governance control centre for defining and enforcing rules that every AI interaction must comply with. Policies are evaluated in real-time on every message — before the AI responds and after.",
    usageSteps: [
      "Click '+ New policy' and give it a name, domain (e.g., Safety, Compliance, BudgetLimit), scope (Global / Environment / Tenant), and effect (Allow / AllowWithConstraints / Deny).",
      "Add Rules to your policy using natural language match conditions (e.g., 'message contains competitor name') and set the effect and priority.",
      "Click 'Create version' to save a new draft, then 'Submit' → 'Approve' → 'Activate' to deploy it.",
      "Use the 'Test policy' panel to enter a sample message and see immediately which rules would match and what the enforcement action would be.",
      "Use 'Clone' to copy an existing policy as a starting point for a new one.",
      "Review the 'Policy metrics' cards at the top to see how many policies are active and how many evaluations have been performed."
    ],
    examples: [
      "Safety policy: Create a 'Safety' domain policy with a Deny rule that matches 'instructions to harm'. Effect: Block the response and log the violation.",
      "Budget policy: Create a 'BudgetLimit' domain policy with AllowWithConstraints that caps max tokens to 500 for Viewer-role users.",
      "Compliance policy: Add a 'Compliance' domain rule that ensures no PII (credit card, ID numbers) appears in any AI response."
    ],
    expectedOutput: "Active, versioned governance policies that are evaluated in milliseconds against every conversation turn. The Test panel shows you exactly which rules fire for any given input.",
    aiLayerRole: "The Policy Engine uses a fast, lightweight AI classifier to evaluate every user input and AI output against your rules. Violations trigger the configured effect (Block, Log, Rewrite) before the response reaches the user."
  },
  "PromptStudioPage.tsx": {
    title: "Prompt Studio",
    description: "A version-controlled IDE for building and governing the system prompts that shape your AI's personality, tone, and behaviour. Prompts are composed of typed sections and pass through a formal approval lifecycle before being used in conversations.",
    usageSteps: [
      "Click '+ New prompt' to create a prompt definition (name, owner, category, tags, description).",
      "In the 'Prompt definition' panel, edit the sections — each section has a type (System, Developer, Knowledge, Conversation, User, Output) and text content.",
      "Use template variables like {{customerMessage}}, {{knowledgePackage}}, or {{conversationHistory}} in section content — these are resolved at runtime.",
      "Enter a version number (e.g., 1.1.0) and click '+ Create version' to snapshot your current sections.",
      "In the 'Version inspector', click 'Submit' → 'Approve' → 'Publish' to deploy a version for use in simulations and workflows.",
      "Click 'Render preview' to see exactly how the prompt will look when runtime variables are resolved, showing estimated token count.",
      "Use 'Compare with' to diff any two versions — the comparison shows token delta and added/removed variables."
    ],
    examples: [
      "Creating a grounded claims assistant prompt: Set the System section to 'You are a careful claims assistant. Answer only from governed knowledge.' Add a Knowledge section using {{knowledgePackage}}.",
      "Version comparison: After updating tone instructions in v1.2.0, compare it with v1.1.0 to confirm the token count increased by less than 50 tokens."
    ],
    expectedOutput: "Published, versioned prompt templates with a clear audit trail of changes, variable inventory, and a rendered preview showing exactly what the AI will receive.",
    aiLayerRole: "Published prompts are injected into the AI's context window at conversation runtime. The Knowledge section variable ({{knowledgePackage}}) is populated by the Knowledge Engine with the most relevant retrieved chunks."
  },
  "ReplayStudioPage.tsx": {
    title: "Replay Studio",
    description: "An experiment workspace for running controlled A/B tests on your AI configuration. You take a real historical conversation (the 'source run') and replay it against multiple candidate configurations (different prompts, providers, or models) to compare outcomes side-by-side.",
    usageSteps: [
      "Click '+ New experiment' and provide the source run ID (from Trace Explorer or Conversation Simulator) and an experiment name.",
      "Once the experiment is created, click '+ Add candidate' to define a test configuration: choose a Workflow, Prompt Version, Knowledge Collection, Provider, Model, Temperature, and Mode.",
      "Add multiple candidates (e.g., Candidate A uses GPT-4o, Candidate B uses Claude 3.5 Sonnet).",
      "Click 'Complete' on a candidate to execute the replay — it re-runs the original conversation messages against your candidate configuration.",
      "Select a completed run to inspect its output, metrics (latency, tokens, cost), and compare it against other candidates.",
      "Use 'Archive' to close an experiment when testing is complete."
    ],
    examples: [
      "Model comparison: Take a flagged conversation from Trace Explorer, replay it with your current model vs. a new one, and compare the response quality.",
      "Prompt regression test: Replay 10 production conversations against a new prompt version before promoting it."
    ],
    expectedOutput: "Side-by-side run snapshots for each candidate configuration, showing the AI's responses, execution metrics, and quality indicators so you can make an informed promotion decision.",
    aiLayerRole: "The Replay Engine re-executes the original user messages through the full AI pipeline — knowledge retrieval, prompt rendering, and response generation — but using your candidate configuration instead of the original one."
  },
  "SettingsPage.tsx": {
    title: "Workspace Settings",
    description: "Configuration management for your workspace's runtime environments, AI provider connections, budget limits, evaluation thresholds, trace retention policies, feature flags, and secret references. Changes here affect how the entire platform behaves at runtime.",
    usageSteps: [
      "Navigate sections using the left sidebar: Runtime (Environments, General, AI Provider), Guardrails (Budgets, Evaluation, Trace & Retention, Feature Flags), Operations (Secrets, Audit, Import/Export, Deployment).",
      "'Environments' tab: Create isolated runtime contexts (e.g., Dev, Staging, Production) and set one as the default. Click 'Activate' to switch the platform to a different environment.",
      "'AI Provider' tab: Configure your LLM provider key and model. Click 'Validate provider' to run a live connectivity test before saving.",
      "'Budgets' tab: Set a ZAR monthly spend limit. The platform will block executions that would exceed this threshold.",
      "'Secrets' tab: Store API keys and credentials securely as named references (e.g., OPENAI_API_KEY). These are never shown again after creation.",
      "'Import / Export' tab: Export your entire workspace configuration as an encrypted JSON file. Use Import to restore it or move it to another workspace.",
      "'Deployment' tab: Promote a validated environment configuration to a target environment."
    ],
    examples: [
      "Adding a production environment: Create a new environment named 'Production', set it as default, then configure the AI provider key for that environment.",
      "Setting a budget guard: Enter R5,000 in the Budgets section — the platform will stop executing AI calls once monthly spend approaches this limit."
    ],
    expectedOutput: "Correctly configured runtime environments, provider connections, and guardrail settings that control how the AI operates across the platform.",
    aiLayerRole: "The AI Provider settings directly configure which model the Intelligence Engine uses. Budget settings impose hard limits on AI token consumption. Feature flags enable or disable experimental AI capabilities per environment."
  },
  "TraceExplorerPage.tsx": {
    title: "Trace Explorer",
    description: "A searchable, filterable log of every AI execution that has occurred on the platform. Each trace captures the complete lifecycle of a single conversation turn — from the incoming message to the final response — including all spans, events, and artifacts.",
    usageSteps: [
      "Use the search bar to find traces by correlation ID, session ID, run ID, or any keyword from the conversation.",
      "Filter by Status (Completed, Failed), Provider, or Capability using the filter dropdowns.",
      "Click any trace in the list to open its detail view. Use the tabs: Spans (execution steps), Events (domain events emitted), Artifacts (prompt renders, knowledge packages), Context (raw request/response).",
      "Toggle 'Include sensitive data' to reveal PII and prompt content (requires Engineer role or above).",
      "Click 'Open in Replay Studio' from any trace to start a controlled re-run experiment.",
      "Review the metric chips on each trace row: latency (ms), token count, cost, and policy enforcement indicator."
    ],
    examples: [
      "Debugging a slow response: Filter by Status=Completed, sort by latency descending, and inspect the Spans tab to see which step (knowledge retrieval? token generation?) took the longest.",
      "Investigating a policy block: Search for traces with a 'ShieldAlert' icon — open the Events tab to see which policy rule fired and why."
    ],
    expectedOutput: "A detailed breakdown of any AI execution, showing the exact sequence of steps, knowledge retrieved, prompt used, tokens consumed, latency at each stage, and any policy violations.",
    aiLayerRole: "Every AI execution is automatically instrumented with OpenTelemetry-compatible spans and events, providing full observability into model selection, knowledge retrieval, prompt rendering, and policy evaluation."
  },
  "WorkflowDesignerPage.tsx": {
    title: "Workflow Designer",
    description: "A visual node editor for defining the step-by-step logic of your conversational AI pipeline. Workflows connect Knowledge retrieval, Prompt rendering, Intelligence generation, Decision branching, and Response steps into a governed execution flow.",
    usageSteps: [
      "Click '+ New workflow' to create a workflow definition (name, owner, tags, description).",
      "Select a workflow from the left list. A starter graph is created automatically with the standard pipeline: Start → Knowledge → Prompt → Intelligence → Response → End.",
      "Click '+ Add step' to insert a new node. Choose from: Start, Knowledge, Prompt, Decision, Intelligence, Response, End.",
      "Drag the grip handle (⠿) on any node to reorder it within the flow.",
      "Click 'Save workflow' to persist your changes.",
      "Click 'Run' to execute the workflow against a test message and see the output.",
      "Use 'Add transition' to create conditional branches between Decision nodes — set the condition text that must match for each path."
    ],
    examples: [
      "Standard pipeline: Start → Knowledge (retrieve relevant docs) → Prompt (render system instructions) → Intelligence (generate response) → Response (return to user) → End.",
      "Branching flow: After a Decision node, route high-confidence queries to a fast model and low-confidence ones to a more capable (but slower) model."
    ],
    expectedOutput: "A saved, versioned workflow definition that can be selected in the Conversation Simulator, Replay Studio, or deployed to production as the AI's execution pipeline.",
    aiLayerRole: "The workflow orchestrates the full AI pipeline — each node type invokes a different engine (Knowledge Engine for retrieval, Intelligence Engine for generation) in the correct sequence with governed transitions."
  },
  "WorkspacePage.tsx": {
    title: "Workspace Administration",
    description: "The membership and governance hub for your workspace. Manage who has access, what roles they hold, machine identities for automation, and a full audit trail of all governed activity.",
    usageSteps: [
      "'Overview' tab: See workspace name, status, slug, and isolation model. A workspace enforces strict data isolation — no data leaks between workspaces.",
      "'Members' tab: Click 'Invite member' to add a person by email and display name. Assign a role: Administrator, Engineer, Reviewer, Operator, or Viewer.",
      "'Roles' tab: Review the permissions granted by each role. Administrator has full access; Viewer can only read non-sensitive resources.",
      "'Service Accounts' tab: Create a named machine identity with a credential for automation (e.g., CI/CD pipelines). The credential is shown only once — copy it immediately.",
      "'Audit' tab: View an immutable append-only log of all governed actions (who did what, when, and the correlation ID for tracing).",
      "Switch workspaces using the dropdown at the top right of this page if you have access to multiple workspaces."
    ],
    examples: [
      "Onboarding a developer: Go to Members → 'Invite member' → enter email → assign 'Engineer' role. They can now build assets and run simulations.",
      "Setting up CI/CD: Go to Service Accounts → create 'CI Runner' → copy the credential → add it to your GitHub Actions secrets as CONVOLAB_API_KEY."
    ],
    expectedOutput: "A governed workspace with role-appropriate access for every team member, machine identities for automation, and a complete audit trail of all activity.",
    aiLayerRole: "Workspace isolation ensures that AI knowledge, prompts, policies, and conversation data from one team cannot be accessed by another. Each workspace is a fully isolated execution boundary."
  }
};

pages.forEach(page => {
  const filePath = path.join(pagesDir, page);
  if (!fs.existsSync(filePath)) return;
  
  let content = fs.readFileSync(filePath, 'utf8');
  
  const startIndex = content.indexOf('useHelp({');
  if (startIndex !== -1) {
    let depth = 0;
    let endIndex = startIndex;
    for (let i = startIndex; i < content.length; i++) {
      if (content[i] === '{') depth++;
      if (content[i] === '}') {
        depth--;
        if (depth === 0) {
          // Find the next semicolon or closing paren + semicolon
          let j = i + 1;
          while (j < content.length && (content[j] === ')' || content[j] === ';' || content[j] === '\n' || content[j] === '\r')) {
            if (content[j] === ';') { endIndex = j + 1; break; }
            j++;
          }
          if (endIndex === startIndex) endIndex = i + 1;
          break;
        }
      }
    }
    
    const config = detailedHelpContent[page] || detailedHelpContent['DashboardPage.tsx'];
    
    const replacement = `useHelp({
    title: ${JSON.stringify(config.title)},
    description: ${JSON.stringify(config.description)},
    usageSteps: ${JSON.stringify(config.usageSteps, null, 6).replace(/\n/g, '\n    ')},
    examples: ${JSON.stringify(config.examples, null, 6).replace(/\n/g, '\n    ')},
    expectedOutput: ${JSON.stringify(config.expectedOutput)},
    aiLayerRole: ${JSON.stringify(config.aiLayerRole)}
  });`;

    content = content.slice(0, startIndex) + replacement + content.slice(endIndex);
    fs.writeFileSync(filePath, content, 'utf8');
    console.log(`✅ Updated ${page}`);
  } else {
    console.log(`⚠️  Could not find useHelp block in ${page}`);
  }
});
