import { useMemo, useState } from "react";
import { useHelp } from "../contexts/HelpContext";
import { 
  BookOpen, 
  Search, 
  LayoutDashboard, 
  MessageSquareText, 
  Database, 
  Braces, 
  Workflow, 
  BrainCircuit, 
  ShieldCheck, 
  BookOpenCheck, 
  Activity, 
  RotateCcw, 
  PlugZap, 
  UsersRound, 
  BarChart3, 
  Settings, 
  Gauge, 
  ChevronRight,
  ChevronDown,
  X
} from "lucide-react";
import { useNavigate } from "react-router";
import "./HelpCenterPage.css";
import { StatusPill } from "../components/StatusPill";

type Tab = "getting-started" | "screens" | "ai-layer" | "concepts" | "shortcuts" | "release-notes";

const screenGuides = [
  {
    id: "dashboard",
    title: "Platform Dashboard",
    icon: LayoutDashboard,
    path: "/",
    summary: "Your home screen. It gives you a quick overview of system health and provides shortcuts to get started.",
    details: [
      "Check the metric cards at the top to see if the system is healthy and running smoothly.",
      "Look at the list of tools to see what is ready to use and what is still being built.",
      "Use the 'Start building' buttons to jump straight into your work.",
      "The dashboard automatically shows you the tools that are most useful for your specific role."
    ],
    role: "All Roles"
  },
  {
    id: "conversations",
    title: "Conversation Simulator",
    icon: MessageSquareText,
    path: "/conversations",
    summary: "A chat window where you can talk to your AI and test how it behaves before it goes live.",
    details: [
      "Click '+ New simulation' to start a new test chat.",
      "Pick the rules, instructions, and documents you want the AI to use.",
      "Type a message and see how the AI responds.",
      "Open the 'Inspector' tab on the right to look under the hood and see exactly why the AI answered the way it did."
    ],
    role: "All Roles"
  },
  {
    id: "workflows",
    title: "Workflow Designer",
    icon: Workflow,
    path: "/workflows",
    summary: "A drag-and-drop tool to design the step-by-step process of how your AI thinks and responds.",
    details: [
      "Click '+ New workflow' to start building a process.",
      "Add steps to tell the AI what to do: look up knowledge, check rules, or ask an AI model.",
      "Drag the steps around to put them in the right order.",
      "Add branching paths (like a flowchart) so the AI can make different choices depending on the situation."
    ],
    role: "Administrator, Engineer"
  },
  {
    id: "prompts",
    title: "Prompt Studio",
    icon: Braces,
    path: "/prompts",
    summary: "A text editor where you write the core instructions that tell your AI how to act and what to say.",
    details: [
      "Write out the AI's personality, rules, and goals.",
      "Use placeholders (like {{customerMessage}}) that will be automatically replaced with real text later.",
      "Preview your instructions to see exactly how they look before you save them.",
      "Save your work as a new version so you can safely try changes without losing the original."
    ],
    role: "Administrator, Engineer, Reviewer"
  },
  {
    id: "knowledge",
    title: "Knowledge Studio",
    icon: Database,
    path: "/knowledge",
    summary: "A place to upload your company's documents (like PDFs or Word files) so the AI can read them and answer questions.",
    details: [
      "Group your documents into folders (Collections) and set security levels.",
      "Upload your files and click 'Process' to let the system chop them into readable pieces.",
      "Review the processed files and approve them for the AI to use.",
      "Type a sample question into the test panel to see if the AI can find the right paragraphs in your documents."
    ],
    role: "Administrator, Engineer, Reviewer"
  },
  {
    id: "intelligence",
    title: "Intelligence Center",
    icon: BrainCircuit,
    path: "/intelligence",
    summary: "A dashboard to monitor your AI providers (like OpenAI) to ensure they are fast, cheap, and working properly.",
    details: [
      "Run tests to check if your AI providers are online.",
      "Use 'Preview plan' to see how much a message will cost before you actually send it.",
      "Look at recent chats to check how long they took and how much they cost.",
      "Set up backup AI models that automatically take over if the main one goes offline."
    ],
    role: "Administrator, Engineer, Operator"
  },
  {
    id: "policies",
    title: "Policy Center",
    icon: ShieldCheck,
    path: "/policies",
    summary: "A tool to set strict rules that stop the AI from saying bad things or breaking company guidelines.",
    details: [
      "Create rules for safety, keeping on topic, and budget limits.",
      "Decide what happens when a rule is broken: allow it, change the answer, or block it completely.",
      "Test your rules in the chat simulator to make sure they work.",
      "These rules run instantly in the background every time the AI speaks."
    ],
    role: "Administrator, Engineer, Reviewer"
  },
  {
    id: "evaluation",
    title: "Evaluation Studio",
    icon: BookOpenCheck,
    path: "/evaluation",
    summary: "A testing area where you can run hundreds of automated tests to grade how well the AI is doing.",
    details: [
      "Create a 'Scorecard' that defines what makes a good answer (like being polite and factual).",
      "Add test questions and the answers you expect to see.",
      "Run a batch of tests to automatically grade the AI on hundreds of questions at once.",
      "Compare the latest test scores with older scores to make sure the AI isn't getting worse."
    ],
    role: "Administrator, Reviewer"
  },
  {
    id: "traces",
    title: "Trace Explorer",
    icon: Activity,
    path: "/traces",
    summary: "A detailed history log of every single message and exactly how the system processed it.",
    details: [
      "Search for specific conversations using an ID or keywords.",
      "See a visual timeline of how long each step took so you can find slow parts.",
      "See exactly which rules were triggered and what choices the AI made.",
      "Sensitive data (like the actual chat text) is hidden by default to protect privacy."
    ],
    role: "All Roles (Sensitive Data: Admin/Engineer)"
  },
  {
    id: "replay",
    title: "Replay Studio",
    icon: RotateCcw,
    path: "/replay",
    summary: "A place to run experiments. You can change a setting and replay old conversations to see if the AI performs better.",
    details: [
      "Pick an old conversation to use as your starting point.",
      "Change a setting, like picking a smarter AI model or rewriting the instructions.",
      "Re-run the conversation to see how the AI would have answered with the new settings.",
      "Compare the new answers to the old answers to see if you improved things."
    ],
    role: "Administrator, Engineer, Operator"
  },
  {
    id: "plugins",
    title: "Plugin Center",
    icon: PlugZap,
    path: "/plugins",
    summary: "A store to add extra tools and connections to your AI.",
    details: [
      "Add new plugins by providing a web link to their code.",
      "Find plugins that connect to new AI models, databases, or outside tools.",
      "Turn plugins on or off for the whole workspace.",
      "Check if plugins are healthy and update their settings."
    ],
    role: "Administrator"
  },
  {
    id: "workspace",
    title: "Workspace & Access",
    icon: UsersRound,
    path: "/workspace",
    summary: "A settings area to manage who has access to the platform and what they are allowed to do.",
    details: [
      "Invite people to your team and assign them roles (like Admin or Viewer).",
      "Create special machine accounts for automated tasks and outside apps.",
      "Look at the security log to see a record of every important action taken by your team."
    ],
    role: "Administrator (Read-only for others)"
  },
  {
    id: "analytics",
    title: "Platform Analytics",
    icon: BarChart3,
    path: "/analytics",
    summary: "A dashboard showing charts and graphs about how much your AI is used and how much it costs.",
    details: [
      "Pick a date range to see data for a specific time period.",
      "Look at charts showing how many messages were sent, how much they cost, and how fast they were.",
      "Click the Events tab to see the raw data behind the charts.",
      "Admins can download the data as a spreadsheet file."
    ],
    role: "All Roles (Tabs vary by role)"
  },
  {
    id: "settings",
    title: "Settings",
    icon: Settings,
    path: "/settings",
    summary: "General settings for your workspace and technical environments.",
    details: [
      "Manage different environments (like a testing area vs a live production area).",
      "Add your secret keys and passwords for AI providers.",
      "Set a budget limit to make sure your AI doesn't spend too much money.",
      "Turn experimental features on or off."
    ],
    role: "Administrator"
  },
  {
    id: "operations",
    title: "Operations",
    icon: Gauge,
    path: "/operations",
    summary: "Advanced tools for technical admins to manage the underlying servers and system health.",
    details: [
      "Check if the background databases and servers are healthy.",
      "Turn on 'Safe Mode' to instantly stop the AI from talking to the outside world during an emergency.",
      "Manage database backups so you don't lose data.",
      "Approve changes when moving code from the testing area to the live area."
    ],
    role: "Platform Administrator"
  }
];

export function HelpCenterPage() {
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<Tab>("getting-started");
  const [searchQuery, setSearchQuery] = useState("");
  const [expandedCardId, setExpandedCardId] = useState<string | null>(null);

  useHelp({
    title: "Help Center",
    description: "The main documentation hub for learning how to use ConvoLab Studio.",
    usageSteps: [
      "Use the search bar to find documentation across all screens and concepts.",
      "Switch tabs to explore Getting Started guides, Screen guides, or Architectural concepts.",
      "Click on any screen card to expand it and read detailed usage instructions."
    ],
    examples: [
      "Searching for 'token limit' to find where budget constraints are configured.",
      "Reading the AI Layer guide to understand the order of execution for a chat message."
    ],
    expectedOutput: "Comprehensive guides that explain both the 'how' and the 'why' of the platform.",
    aiLayerRole: "The Help Center documents how you interact with the AI Layer, but is itself a static documentation application."
  });

  const filteredScreens = useMemo(() => {
    if (!searchQuery) return screenGuides;
    const lowerQ = searchQuery.toLowerCase();
    return screenGuides.filter(s => 
      s.title.toLowerCase().includes(lowerQ) || 
      s.summary.toLowerCase().includes(lowerQ) ||
      s.details.some(d => d.toLowerCase().includes(lowerQ))
    );
  }, [searchQuery]);

  return (
    <div className="help-center-page page-stack">
      <header className="help-center-header">
        <div className="page-heading-icon">
          <BookOpen size={24} />
        </div>
        <h1>ConvoLab Help Center</h1>
        <p>Comprehensive guides, screen references, and architectural concepts for mastering conversational AI engineering.</p>
      </header>

      <div className="help-search-container">
        <Search className="help-search-icon" size={20} />
        <input 
          type="text" 
          placeholder="Search for screens, workflows, or concepts..." 
          value={searchQuery}
          onChange={(e) => {
            setSearchQuery(e.target.value);
            if (e.target.value && activeTab !== "screens") {
              setActiveTab("screens");
            }
          }}
        />
      </div>

      <nav className="help-tabs">
        <button className={activeTab === "getting-started" ? "active" : ""} onClick={() => setActiveTab("getting-started")}>Getting Started</button>
        <button className={activeTab === "screens" ? "active" : ""} onClick={() => setActiveTab("screens")}>Screen-by-Screen Guide</button>
        <button className={activeTab === "ai-layer" ? "active" : ""} onClick={() => setActiveTab("ai-layer")}>AI Layer Explained</button>
        <button className={activeTab === "concepts" ? "active" : ""} onClick={() => setActiveTab("concepts")}>Concepts & Workflows</button>
        <button className={activeTab === "shortcuts" ? "active" : ""} onClick={() => setActiveTab("shortcuts")}>Keyboard Shortcuts</button>
        <button className={activeTab === "release-notes" ? "active" : ""} onClick={() => setActiveTab("release-notes")}>Release Notes</button>
      </nav>

      {activeTab === "getting-started" && (
        <section className="help-content-text">
          <div className="panel">
            <h2>Welcome to ConvoLab Studio</h2>
            <p>ConvoLab Studio is an enterprise platform for designing, testing, and governing conversational AI. It breaks the AI application into distinct, versioned capabilities that can be managed by different teams.</p>
            
            <h3 style={{marginTop: "32px"}}>The 3-Step Quickstart</h3>
            <div className="ai-layer-flow">
              <div className="ai-flow-step">
                <div className="ai-flow-number">1</div>
                <div>
                  <h4>Build your assets</h4>
                  <p>Upload your documents to the <strong>Knowledge Studio</strong> and publish them. Write your system instructions in the <strong>Prompt Studio</strong> and publish a version.</p>
                </div>
              </div>
              <div className="ai-flow-step">
                <div className="ai-flow-number">2</div>
                <div>
                  <h4>Connect the pipeline</h4>
                  <p>Open the <strong>Workflow Designer</strong> and create a pipeline that connects your Knowledge collection to your Prompt, then routes it to the Intelligence engine.</p>
                </div>
              </div>
              <div className="ai-flow-step">
                <div className="ai-flow-number">3</div>
                <div>
                  <h4>Simulate and Trace</h4>
                  <p>Open the <strong>Conversation Simulator</strong>, select your workflow, and send a message. Check the Inspector panel to see exactly what knowledge was retrieved and how many tokens were used.</p>
                </div>
              </div>
            </div>
          </div>
        </section>
      )}

      {activeTab === "screens" && (
        <section className="help-content-grid">
          {filteredScreens.map(screen => {
            const Icon = screen.icon;
            const isExpanded = expandedCardId === screen.id;
            
            return (
              <article 
                key={screen.id} 
                className={`help-card ${isExpanded ? 'help-card-expanded' : ''}`}
                onClick={() => !isExpanded && setExpandedCardId(screen.id)}
              >
                <div className="help-card-header">
                  <div className="help-card-icon">
                    <Icon size={18} />
                  </div>
                  <div>
                    <h3>{screen.title}</h3>
                  </div>
                  {isExpanded && (
                    <button 
                      className="icon-button" 
                      style={{marginLeft: 'auto'}} 
                      onClick={(e) => { e.stopPropagation(); setExpandedCardId(null); }}
                    >
                      <X size={18} />
                    </button>
                  )}
                </div>
                
                <p>{screen.summary}</p>
                
                {isExpanded && (
                  <div className="help-card-details" onClick={(e) => e.stopPropagation()}>
                    <h4>Required Role</h4>
                    <p style={{marginBottom: "24px", color: "var(--text-primary)"}}>{screen.role}</p>

                    <h4>Key Actions</h4>
                    <ul>
                      {screen.details.map((detail, i) => (
                        <li key={i}>{detail}</li>
                      ))}
                    </ul>

                    <div className="help-card-actions">
                      <button className="primary-button" onClick={() => navigate(screen.path)}>
                        Open {screen.title} <ChevronRight size={16} />
                      </button>
                    </div>
                  </div>
                )}
                
                {!isExpanded && (
                  <div style={{display: 'flex', alignItems: 'center', color: '#e0004d', fontSize: '13px', fontWeight: 500, marginTop: '8px'}}>
                    Read more <ChevronDown size={14} style={{marginLeft: '4px'}} />
                  </div>
                )}
              </article>
            );
          })}
          
          {filteredScreens.length === 0 && (
            <div style={{gridColumn: "1 / -1", textAlign: "center", padding: "48px", color: "var(--text-muted)"}}>
              No screens found matching "{searchQuery}"
            </div>
          )}
        </section>
      )}

      {activeTab === "ai-layer" && (
        <section className="panel">
          <h2>The AI Execution Pipeline</h2>
          <p>When a user sends a message, it travels through several governed engines before a response is generated.</p>
          
          <div className="ai-layer-flow" style={{marginTop: "24px"}}>
            <div className="ai-flow-step">
              <div className="ai-flow-number">1</div>
              <div>
                <h4>Policy Pre-check (Policy Engine)</h4>
                <p>The user's raw message is evaluated against active policies. If it violates a 'Deny' rule (e.g. toxicity, off-topic), the pipeline stops immediately and returns a governed rejection message.</p>
              </div>
            </div>
            <div className="ai-flow-step">
              <div className="ai-flow-number">2</div>
              <div>
                <h4>Knowledge Retrieval (Knowledge Engine)</h4>
                <p>The message is converted to an embedding and compared against the vector database for the active Knowledge Collection. The top N matching chunks are retrieved and packaged.</p>
              </div>
            </div>
            <div className="ai-flow-step">
              <div className="ai-flow-number">3</div>
              <div>
                <h4>Prompt Compilation (Prompt Engine)</h4>
                <p>The active Prompt Version is loaded. Variables like <code>{`{{knowledgePackage}}`}</code> are replaced with the retrieved chunks. <code>{`{{customerMessage}}`}</code> is replaced with the user input. The final string is assembled.</p>
              </div>
            </div>
            <div className="ai-flow-step">
              <div className="ai-flow-number">4</div>
              <div>
                <h4>Execution (Intelligence Engine)</h4>
                <p>The compiled prompt is sent to the primary LLM Provider (e.g. OpenAI). The engine monitors token usage against budgets and handles retries or fallback to secondary models if the primary fails.</p>
              </div>
            </div>
            <div className="ai-flow-step">
              <div className="ai-flow-number">5</div>
              <div>
                <h4>Policy Post-check & Trace (Policy Engine & Trace Engine)</h4>
                <p>The generated response is evaluated against outbound policies (e.g. PII redaction). If it passes, the response is sent to the user and the entire lifecycle is saved as a Trace.</p>
              </div>
            </div>
          </div>
        </section>
      )}

      {activeTab === "concepts" && (
        <section className="panel">
          <h2>Platform Concepts</h2>
          <div className="status-legend" style={{marginTop: "24px"}}>
            <div className="status-legend-item">
              <div style={{paddingTop: '2px'}}><StatusPill status="stable" /></div>
              <div>
                <h4>Stable Capabilities</h4>
                <p>Features marked as Stable are production-ready. They have complete UIs, full test coverage, and API contracts that will not introduce breaking changes without a major version bump.</p>
              </div>
            </div>
            <div className="status-legend-item">
              <div style={{paddingTop: '2px'}}><StatusPill status="active" /></div>
              <div>
                <h4>Active Capabilities</h4>
                <p>Features marked as Active are live and working, but may still be evolving. They might be missing secondary features or their internal structures could change.</p>
              </div>
            </div>
            <div className="status-legend-item">
              <div style={{paddingTop: '2px'}}><StatusPill status="foundation" /></div>
              <div>
                <h4>Foundation Capabilities</h4>
                <p>Features marked as Foundation are scaffolded in the API but lack complete user interfaces or are restricted to Platform Administrators.</p>
              </div>
            </div>
          </div>
        </section>
      )}

      {activeTab === "shortcuts" && (
        <section className="panel">
          <h2>Keyboard Shortcuts</h2>
          <table style={{width: '100%', borderCollapse: 'collapse', marginTop: '16px'}}>
            <thead>
              <tr style={{borderBottom: '1px solid var(--border-color)', textAlign: 'left'}}>
                <th style={{padding: '12px 16px', fontWeight: 600}}>Action</th>
                <th style={{padding: '12px 16px', fontWeight: 600}}>Windows / Linux</th>
                <th style={{padding: '12px 16px', fontWeight: 600}}>macOS</th>
              </tr>
            </thead>
            <tbody>
              <tr style={{borderBottom: '1px solid var(--border-color)'}}>
                <td style={{padding: '12px 16px'}}>Open Command Palette</td>
                <td style={{padding: '12px 16px'}}><kbd>Ctrl</kbd> + <kbd>K</kbd></td>
                <td style={{padding: '12px 16px'}}><kbd>Cmd</kbd> + <kbd>K</kbd></td>
              </tr>
              <tr style={{borderBottom: '1px solid var(--border-color)'}}>
                <td style={{padding: '12px 16px'}}>Send message (Simulator)</td>
                <td style={{padding: '12px 16px'}}><kbd>Ctrl</kbd> + <kbd>Enter</kbd></td>
                <td style={{padding: '12px 16px'}}><kbd>Cmd</kbd> + <kbd>Enter</kbd></td>
              </tr>
              <tr style={{borderBottom: '1px solid var(--border-color)'}}>
                <td style={{padding: '12px 16px'}}>Save form / changes</td>
                <td style={{padding: '12px 16px'}}><kbd>Ctrl</kbd> + <kbd>S</kbd></td>
                <td style={{padding: '12px 16px'}}><kbd>Cmd</kbd> + <kbd>S</kbd></td>
              </tr>
              <tr>
                <td style={{padding: '12px 16px'}}>Close modals / sidebars</td>
                <td style={{padding: '12px 16px'}}><kbd>Esc</kbd></td>
                <td style={{padding: '12px 16px'}}><kbd>Esc</kbd></td>
              </tr>
            </tbody>
          </table>
        </section>
      )}

      {activeTab === "release-notes" && (
        <section className="panel">
          <h2>Release Notes</h2>
          
          <div style={{marginTop: '24px', paddingBottom: '24px', borderBottom: '1px solid var(--border-color)'}}>
            <h3 style={{display: 'flex', alignItems: 'center', gap: '8px'}}>
              v1.0.0-alpha.17 <StatusPill status="stable" compact />
            </h3>
            <p style={{color: 'var(--text-muted)', fontSize: '14px', marginBottom: '16px'}}>September 2026</p>
            <ul>
              <li style={{marginBottom: '8px'}}><strong>Help Center:</strong> Added dedicated Help Center page with deep-dive guides for every screen.</li>
              <li style={{marginBottom: '8px'}}><strong>Navigation Statuses:</strong> Audited and updated all capability badges to accurately reflect stable/active states.</li>
              <li style={{marginBottom: '8px'}}><strong>Help FAB UX:</strong> Floating action button now smoothly rotates to an 'X' when open and sits above all overlays.</li>
              <li style={{marginBottom: '8px'}}><strong>Dashboard Quick Actions:</strong> The "Start building" section now intelligently sorts shortcuts based on your workspace role.</li>
            </ul>
          </div>
          
          <div style={{marginTop: '24px'}}>
            <h3 style={{display: 'flex', alignItems: 'center', gap: '8px', color: 'var(--text-muted)'}}>
              v1.0.0-alpha.16 <StatusPill status="active" compact />
            </h3>
            <p style={{color: 'var(--text-muted)', fontSize: '14px', marginBottom: '16px'}}>August 2026</p>
            <ul style={{color: 'var(--text-muted)'}}>
              <li style={{marginBottom: '8px'}}><strong>Plugin Engine:</strong> Initial release of the Plugin Center for managing third-party extensions.</li>
              <li style={{marginBottom: '8px'}}><strong>Replay Studio:</strong> Added candidate comparison table metrics.</li>
            </ul>
          </div>
        </section>
      )}

    </div>
  );
}
