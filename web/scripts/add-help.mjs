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
    description: "The Analytics page provides a comprehensive dashboard of platform usage, token consumption, latency, and conversation volume across all deployed workflows.",
    usageSteps: [
      "Select a date range from the top right calendar picker.",
      "Filter the metrics by specific workflows or capabilities using the sidebar.",
      "Hover over any chart (e.g., Token Usage over Time) to see granular data points.",
      "Click 'Export CSV' to download the raw data for external reporting."
    ],
    examples: [
      "Monitoring cost: Check the 'Token Consumption' widget to ensure you aren't exceeding your budget.",
      "Debugging latency: Look for spikes in the 'Average Response Time' graph to identify degraded performance."
    ],
    expectedOutput: "Real-time graphs and KPIs that help you understand the health, cost, and usage patterns of your conversational applications.",
    aiLayerRole: "The AI layer automatically categorizes conversation topics and surfaces anomalies (e.g., sudden spikes in negative sentiment) directly into the dashboard."
  },
  "CapabilityPage.tsx": {
    title: "Capability Configuration",
    description: "Capabilities are reusable AI components (like a specific search tool or an API integration) that can be attached to workflows.",
    usageSteps: [
      "Review the list of available capabilities.",
      "Click 'Add Capability' to integrate a new tool (e.g., Salesforce lookup).",
      "Configure the authentication and endpoint settings for the selected capability.",
      "Enable or disable capabilities across different environments (Dev/Prod)."
    ],
    examples: [
      "Connecting a database so the AI can retrieve order statuses.",
      "Adding a web search capability to allow the AI to answer current events."
    ],
    expectedOutput: "A configured integration that is now available to be used by any AI workflow or simulator in the studio.",
    aiLayerRole: "The AI layer determines when to invoke these capabilities during a conversation, autonomously mapping user intent to the correct API call."
  },
  "ConversationSimulatorPage.tsx": {
    title: "Conversation Simulator",
    description: "The Simulator allows you to chat with your AI in real-time before deploying it, while inspecting exactly how it formulates its answers.",
    usageSteps: [
      "Select a Persona and Workflow from the top dropdown menus.",
      "Type a message in the chat interface on the left and hit send.",
      "Click on the AI's response to open the 'Inspector' tab on the right.",
      "Review the Trace, retrieved Knowledge, and Prompt context used to generate that specific answer."
    ],
    examples: [
      "Testing a new policy by trying to get the AI to break a rule.",
      "Verifying that a newly uploaded document is actually retrieved when you ask a related question."
    ],
    expectedOutput: "A complete chat transcript with deep observability into the AI's internal reasoning, context, and tool usage for every single turn.",
    aiLayerRole: "The AI executes the selected persona, processes your intents, retrieves relevant knowledge, and streams the response dynamically based on live policies."
  },
  "DashboardPage.tsx": {
    title: "Platform Dashboard",
    description: "Your central hub in ConvoLab Studio. It provides a quick glance at system health, recent evaluations, and active operations.",
    usageSteps: [
      "Review the 'System Status' to ensure the API and Database are healthy.",
      "Check the 'Recent Activity' feed to see what your team has changed recently.",
      "Use the quick links to jump directly to your most used studios."
    ],
    examples: [
      "Starting your day by verifying no policies are failing.",
      "Jumping straight back into a draft prompt you were editing yesterday."
    ],
    expectedOutput: "A high-level summary of your workspace's operational status and shortcuts to your active work.",
    aiLayerRole: "The AI summarizes recent platform activities and alerts you to any sudden drops in conversation quality or system errors."
  },
  "DocumentationPage.tsx": {
    title: "Platform Documentation",
    description: "The central repository for API references, SDK guides, and architectural documentation for ConvoLab.",
    usageSteps: [
      "Use the left sidebar to navigate between topics (e.g., API Reference, Webhooks).",
      "Use the search bar (`Cmd+K`) to jump to specific functions or endpoints.",
      "Copy code snippets directly from the examples."
    ],
    examples: [
      "Looking up the exact payload structure for a webhook event.",
      "Finding the authentication headers required for the REST API."
    ],
    expectedOutput: "Detailed technical documentation and code snippets to help developers integrate ConvoLab into external systems.",
    aiLayerRole: "The documentation uses an AI-powered semantic search, allowing you to ask natural language questions about how to use the API."
  },
  "EvaluationStudioPage.tsx": {
    title: "Evaluation Studio",
    description: "Run automated batch tests to ensure AI quality and verify that regressions don't occur when you update prompts or knowledge.",
    usageSteps: [
      "Define a 'Scorecard' (e.g., Helpfulness, Toxicity, Faithfulness).",
      "Upload a CSV dataset of test questions and expected answers.",
      "Click 'Run Evaluation' to execute the batch test.",
      "Review the resulting scores and drill down into conversations that failed."
    ],
    examples: [
      "Running a regression suite of 500 questions after updating a core prompt to ensure accuracy didn't drop.",
      "Testing a new LLM model against your existing benchmark dataset."
    ],
    expectedOutput: "A detailed report card showing how the AI performed across your dataset, with aggregate scores and individual pass/fail metrics.",
    aiLayerRole: "The AI acts as an evaluator (LLM-as-a-judge), analyzing conversation transcripts and scoring them based on the criteria defined in your scorecard."
  },
  "IntelligenceCenterPage.tsx": {
    title: "Intelligence Center",
    description: "Manage the core AI models and inference configurations that power your studio.",
    usageSteps: [
      "Select your primary LLM provider (e.g., OpenAI, Anthropic, Custom endpoint).",
      "Configure default parameters like Temperature, Max Tokens, and Top P.",
      "Set up fallback models in case your primary provider experiences an outage."
    ],
    examples: [
      "Switching from GPT-4o to Claude 3.5 Sonnet for a specific workflow.",
      "Lowering the temperature to 0.1 for tasks that require strict factual extraction."
    ],
    expectedOutput: "Global intelligence settings that dictate how text generation behaves across the platform.",
    aiLayerRole: "This screen directly controls the 'brain' of the platform, managing the weights, biases, and models used for all generative tasks."
  },
  "KnowledgeStudioPage.tsx": {
    title: "Knowledge Studio",
    description: "Manage the documents and data sources the AI uses for Retrieval-Augmented Generation (RAG).",
    usageSteps: [
      "Click 'New Collection' to create a distinct knowledge base (e.g., 'HR Policies').",
      "Upload PDFs, Markdown files, or connect a web scraper.",
      "Configure the chunking strategy (e.g., 512 tokens with 50 token overlap).",
      "Verify the 'Sync Status' to ensure documents are embedded."
    ],
    examples: [
      "Uploading an employee handbook so the AI can answer PTO questions.",
      "Syncing a Zendesk help center to keep the AI's knowledge up to date."
    ],
    expectedOutput: "A fully vectorized database of documents that the AI can instantly search and cite during conversations.",
    aiLayerRole: "The AI automatically chunks your documents, converts them into vector embeddings, and performs semantic similarity searches against them to ground its answers."
  },
  "LoginPage.tsx": {
    title: "Authentication",
    description: "Secure access point for ConvoLab Studio.",
    usageSteps: [
      "Enter your corporate credentials or use Single Sign-On (SSO).",
      "Complete Multi-Factor Authentication if prompted."
    ],
    examples: [
      "Logging in via Okta or Microsoft Entra ID."
    ],
    expectedOutput: "Access to your assigned workspaces and environments based on your RBAC role.",
    aiLayerRole: "Authentication is handled by standard security protocols; AI is not heavily involved here."
  },
  "NotFoundPage.tsx": {
    title: "Page Not Found",
    description: "You have navigated to a URL that does not exist in the Studio.",
    usageSteps: [
      "Click the 'Return Home' button to go back to the Dashboard.",
      "Use the Command Palette (Cmd+K) to search for the page you were looking for."
    ],
    examples: ["N/A"],
    expectedOutput: "Navigation back to a valid platform area.",
    aiLayerRole: "N/A"
  },
  "OperationsPage.tsx": {
    title: "Platform Operations",
    description: "Administrative controls for infrastructure, backups, and platform-wide maintenance.",
    usageSteps: [
      "View active background jobs and worker queues.",
      "Trigger manual backups of the PostgreSQL database.",
      "Review system logs and memory usage."
    ],
    examples: [
      "Flushing the Redis cache after a major configuration change.",
      "Exporting an encrypted backup of all workspace data."
    ],
    expectedOutput: "Successful execution of infrastructure maintenance tasks and system stability.",
    aiLayerRole: "The AI monitors system logs to proactively warn administrators of potential bottlenecks or failing background jobs."
  },
  "PluginCenterPage.tsx": {
    title: "Plugin Center",
    description: "Install and manage third-party extensions that add new features or UI components to the Studio.",
    usageSteps: [
      "Browse the catalog of available plugins.",
      "Click 'Install' on a plugin (e.g., 'Advanced Markdown Editor').",
      "Configure any necessary API keys or permissions required by the plugin."
    ],
    examples: [
      "Installing a plugin that exports analytics data directly to Snowflake.",
      "Adding a custom theme pack to the Studio."
    ],
    expectedOutput: "New capabilities, UI elements, or integrations are seamlessly added to your workspace.",
    aiLayerRole: "Certain plugins may introduce new AI models, prompt templates, or evaluation scorecards into your environment."
  },
  "PolicyCenterPage.tsx": {
    title: "Policy Center",
    description: "Define strict guardrails, compliance rules, and safety filters that the AI must follow.",
    usageSteps: [
      "Toggle standard safety filters (e.g., Block Profanity, Redact PII).",
      "Create a custom policy using natural language (e.g., 'Never mention our competitors').",
      "Define the action to take if a policy is violated (Block, Flag, or Rewrite)."
    ],
    examples: [
      "Creating a policy that automatically masks credit card numbers before they are logged.",
      "Setting a strict guardrail that prevents the AI from offering medical advice."
    ],
    expectedOutput: "Active guardrails that intercept and evaluate every message in real-time to ensure compliance.",
    aiLayerRole: "A lightweight, high-speed AI model evaluates every incoming user message and outgoing AI response against these policies in milliseconds."
  },
  "PromptStudioPage.tsx": {
    title: "Prompt Studio",
    description: "A dedicated IDE for writing, versioning, and testing the system prompts that guide your AI's behavior.",
    usageSteps: [
      "Create a new Prompt Template.",
      "Write the system instructions using variables like `{{user_name}}` or `{{knowledge_context}}`.",
      "Use the 'Test Bench' panel to inject sample variables and see the generated output.",
      "Publish the prompt to a specific version (e.g., v1.2) for use in workflows."
    ],
    examples: [
      "Writing a prompt that instructs the AI to act as a sarcastic pirate.",
      "Refining an extraction prompt to perfectly output JSON data."
    ],
    expectedOutput: "A version-controlled, highly optimized prompt that dictates the tone, format, and behavior of the AI.",
    aiLayerRole: "The AI executes your prompt against the test variables in real-time, helping you rapidly iterate on prompt engineering."
  },
  "ReplayStudioPage.tsx": {
    title: "Replay Studio",
    description: "Debug historical conversations by stepping through them turn-by-turn exactly as they happened.",
    usageSteps: [
      "Select a past conversation ID from the Trace Explorer.",
      "Use the timeline controls (Play, Step Forward, Step Back) to move through the chat.",
      "Inspect the exact state of the context window, retrieved documents, and API responses at each specific turn."
    ],
    examples: [
      "Investigating why the AI hallucinated an answer on turn 4 of a user's conversation.",
      "Stepping through a complex multi-turn booking flow to see where the API call failed."
    ],
    expectedOutput: "A clear understanding of the AI's internal state at any given moment in the past, enabling root-cause analysis.",
    aiLayerRole: "The platform reconstructs the exact AI execution trace, showing you the probabilistic tokens and reasoning paths used historically."
  },
  "SettingsPage.tsx": {
    title: "Workspace Settings",
    description: "Manage your team's workspace, billing, users, and environment variables.",
    usageSteps: [
      "Invite new team members and assign them Roles (Admin, Editor, Viewer).",
      "Manage billing details and view usage limits.",
      "Define secure secrets (API Keys) that the AI capabilities can access securely."
    ],
    examples: [
      "Adding a new developer to the workspace with 'Editor' permissions.",
      "Storing an OpenAI API key securely so it can be used by the Intelligence Center."
    ],
    expectedOutput: "Configured access control, secure secrets management, and healthy workspace administration.",
    aiLayerRole: "N/A"
  },
  "TraceExplorerPage.tsx": {
    title: "Trace Explorer",
    description: "A powerful search and filtering interface for every conversation and API call that happens on the platform.",
    usageSteps: [
      "Use the search bar to find conversations by User ID, Session ID, or specific keywords.",
      "Filter traces by latency, token count, or policy violations.",
      "Click on any trace to view the full log of events, tool calls, and LLM responses."
    ],
    examples: [
      "Searching for all conversations where a 'Toxicity' policy was triggered today.",
      "Finding the slowest 1% of API calls to optimize prompt sizes."
    ],
    expectedOutput: "A filtered list of raw conversation logs and system traces for debugging and auditing.",
    aiLayerRole: "The AI automatically tags and indexes conversations for semantic search, allowing you to find traces based on user intent rather than exact keywords."
  },
  "WorkflowDesignerPage.tsx": {
    title: "Workflow Designer",
    description: "A visual canvas to build complex, multi-step conversational flows, routing logic, and business processes.",
    usageSteps: [
      "Drag and drop nodes (LLM Call, Condition, API Request) onto the canvas.",
      "Connect nodes with edges to define the logic flow.",
      "Configure the settings for each node (e.g., selecting a prompt for an LLM node).",
      "Click 'Deploy' to make the workflow active."
    ],
    examples: [
      "Building a workflow that asks for an order number, checks an API, and then summarizes the status.",
      "Creating a routing layer that classifies user intent and sends them to a specific sub-agent."
    ],
    expectedOutput: "A robust, deployed state machine that orchestrates AI agents, logic, and external tools.",
    aiLayerRole: "The workflow orchestrates the AI, determining when it should speak, when it should use a tool, and when it should hand off to a human."
  },
  "WorkspacePage.tsx": {
    title: "Workspace Selection",
    description: "Navigate between different organizational workspaces.",
    usageSteps: [
      "View the list of workspaces you have access to.",
      "Click on a workspace to load its specific prompts, knowledge, and settings."
    ],
    examples: [
      "Switching from the 'Customer Support' workspace to the 'Internal HR Bot' workspace."
    ],
    expectedOutput: "The studio environment completely reloads with the context and data of the selected workspace.",
    aiLayerRole: "Workspaces ensure that AI knowledge, prompts, and policies are strictly isolated between different teams or projects."
  }
};

pages.forEach(page => {
  const filePath = path.join(pagesDir, page);
  if (!fs.existsSync(filePath)) return;
  
  let content = fs.readFileSync(filePath, 'utf8');
  
  // A regex to match useHelp({ ... })
  // This is a bit tricky because of nested brackets. We can use a simpler approach:
  // Find "useHelp({" and find the matching "});"
  const startIndex = content.indexOf('useHelp({');
  if (startIndex !== -1) {
    let endIndex = content.indexOf('});', startIndex);
    if (endIndex !== -1) {
      endIndex += 3; // include });
      
      const config = detailedHelpContent[page] || detailedHelpContent['DashboardPage.tsx'];
      
      const replacement = `useHelp({
    title: ${JSON.stringify(config.title)},
    description: ${JSON.stringify(config.description)},
    usageSteps: ${JSON.stringify(config.usageSteps, null, 4).replace(/\n/g, '\n    ')},
    examples: ${JSON.stringify(config.examples, null, 4).replace(/\n/g, '\n    ')},
    expectedOutput: ${JSON.stringify(config.expectedOutput)},
    aiLayerRole: ${JSON.stringify(config.aiLayerRole)}
  });`;

      content = content.slice(0, startIndex) + replacement + content.slice(endIndex);
      fs.writeFileSync(filePath, content, 'utf8');
      console.log(`Updated ${page} with detailed content.`);
    }
  } else {
    console.log(`Could not find useHelp block in ${page}`);
  }
});
