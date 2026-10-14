import { useHelp } from "../contexts/HelpContext";
import { useState } from "react";
import { Link } from "react-router";
import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { ArrowRight, Bot, Building2, Check, KeyRound, ScrollText, Shield, Sliders, UserPlus } from "lucide-react";
import { EmptyState, ErrorState, LoadingState } from "../components/AsyncStates";
import { useAuth } from "../contexts/useAuth";
import {
  createServiceAccount,
  getAudit,
  getMembers,
  getServiceAccounts,
  getWorkspace,
  inviteMember,
  resetPassword,
  updateWorkspace,
  suspendWorkspace,
  deleteWorkspace,
  type AuditEvent,
  type ServiceAccount,
  type WorkspaceDetail,
  type WorkspaceMember,
} from "../services/authApi";
import { getApiErrorMessage } from "../services/apiClient";

const tabs = ["Overview", "Members", "Roles", "Service Accounts", "Audit", "Settings"] as const;
type Tab = (typeof tabs)[number];

export function WorkspacePage({ selectionOnly = false }: Readonly<{ selectionOnly?: boolean }>) {
  useHelp({
    title: "Workspace Administration",
    description: "The membership and governance hub for your workspace. Manage who has access, what roles they hold, machine identities for automation, and a full audit trail of all governed activity.",
    usageSteps: [
      "'Overview' tab: See workspace name, status, slug, and isolation model. A workspace enforces strict data isolation — no data leaks between workspaces.",
      "'Members' tab: Click 'Invite member' to add a person by email and display name. Assign a role: Administrator, Engineer, Reviewer, Operator, or Viewer.",
      "'Roles' tab: Review the permissions granted by each role. Administrator has full access; Viewer can only read non-sensitive resources.",
      "'Service Accounts' tab: Create a named machine identity with a credential for automation (e.g., CI/CD pipelines). The credential is shown only once — copy it immediately.",
      "'Audit' tab: View an immutable append-only log of all governed actions (who did what, when, and the correlation ID for tracing).",
      "Switch workspaces using the dropdown at the top right of this page if you have access to multiple workspaces.",
    ],
    examples: [
      "Onboarding a developer: Go to Members → 'Invite member' → enter email → assign 'Engineer' role. They can now build assets and run simulations.",
      "Setting up CI/CD: Go to Service Accounts → create 'CI Runner' → copy the credential → add it to your GitHub Actions secrets as CONVOLAB_API_KEY.",
    ],
    expectedOutput: "A governed workspace with role-appropriate access for every team member, machine identities for automation, and a complete audit trail of all activity.",
    aiLayerRole: "Workspace isolation ensures that AI knowledge, prompts, policies, and conversation data from one team cannot be accessed by another. Each workspace is a fully isolated execution boundary.",
  });

  const auth = useAuth();
  const queryClient = useQueryClient();
  const [tab, setTab] = useState<Tab>("Overview");
  const active = auth.session?.workspaces.find(item => item.id === auth.session?.activeWorkspaceId);

  if (selectionOnly) {
    return (
      <section className="workspace-select-page">
        <header>
          <span className="page-heading-icon"><Building2 /></span>
          <div>
            <span className="panel-eyebrow">Workspace access</span>
            <h1>Select a workspace</h1>
            <p>Your active workspace scopes every capability and query.</p>
          </div>
        </header>
        <div className="workspace-choice-grid">
          {auth.session?.workspaces.map(item => (
            <button
              type="button"
              key={item.id}
              className={`workspace-choice-card${item.id === active?.id ? " selected" : ""}`}
              onClick={() => auth.switchWorkspace(item.id)}
            >
              <span><Building2 /></span>
              <div>
                <strong>{item.name}</strong>
                <small>{item.role}</small>
              </div>
              {item.id === active?.id && <Check />}
            </button>
          ))}
        </div>
      </section>
    );
  }

  if (!active) return <EmptyState title="No active workspace" description="Select a workspace to continue." />;
  const isAdmin = active.role === "Administrator";

  return (
    <section className="workspace-admin-page page-stack">
      <header className="page-heading">
        <span className="page-heading-icon"><Building2 /></span>
        <div>
          <span className="panel-eyebrow">Organisation and access</span>
          <h1>{active.name}</h1>
          <p>Manage members, service identities, roles, security activity, and workspace settings.</p>
        </div>
        <select aria-label="Active workspace" value={active.id} onChange={event => auth.switchWorkspace(event.target.value)}>
          {auth.session?.workspaces.map(item => <option value={item.id} key={item.id}>{item.name} · {item.role}</option>)}
        </select>
      </header>
      <nav className="workspace-tabs" aria-label="Workspace administration">
        {tabs.map(item => (
          <button type="button" key={item} className={tab === item ? "active" : ""} onClick={() => setTab(item)}>
            {item}
          </button>
        ))}
      </nav>
      {!isAdmin && tab !== "Overview" ? (
        <div className="panel restricted-state">
          <Shield />
          <h2>Administrator permission required</h2>
          <p>Your {active.role} role can use workspace capabilities but cannot manage access.</p>
        </div>
      ) : (
        <WorkspaceTab tab={tab} workspaceId={active.id} onInvalidate={key => queryClient.invalidateQueries({ queryKey: [key, active.id] })} />
      )}
    </section>
  );
}

function WorkspaceOverviewPanel({ query }: Readonly<{ query: UseQueryResult<WorkspaceDetail, Error> }>) {
  if (query.isLoading) return <LoadingState compact />;
  if (query.isError) return <ErrorState message={getApiErrorMessage(query.error)} onRetry={() => query.refetch()} />;

  const data = query.data;
  return (
    <div className="workspace-overview-grid">
      <article className="panel">
        <span className="panel-eyebrow">Workspace</span>
        <h2>{data?.name}</h2>
        <dl>
          <div><dt>Status</dt><dd>{data?.status}</dd></div>
          <div><dt>Slug</dt><dd>{data?.slug}</dd></div>
          <div><dt>Revision</dt><dd>{data?.revision}</dd></div>
        </dl>
        <p>{data?.description}</p>
      </article>
      <article className="panel">
        <span className="panel-eyebrow">Isolation</span>
        <h2>Server-enforced ownership</h2>
        <p>Every capability root is filtered by the trusted workspace context. Guessed cross-workspace identifiers resolve as not found.</p>
      </article>
    </div>
  );
}

function WorkspaceRolesPanel() {
  const roles = [
    { name: "Administrator", copy: "Full workspace, membership, service-account, governance, publication, and execution access." },
    { name: "Engineer", copy: "Build assets, draft governance, execute simulations and replays, and inspect sensitive traces." },
    { name: "Reviewer", copy: "Review and publish controlled assets, evaluations, replays, and policies." },
    { name: "Operator", copy: "Run approved workloads and inspect operational traces, decisions, and provider health." },
    { name: "Viewer", copy: "Read non-sensitive workspace resources." },
  ];
  return (
    <section className="panel role-grid">
      {roles.map(role => (
        <article key={role.name}>
          <Shield />
          <div>
            <h3>{role.name}</h3>
            <p>{role.copy}</p>
          </div>
        </article>
      ))}
    </section>
  );
}

function WorkspaceAuditPanel({ query }: Readonly<{ query: UseQueryResult<AuditEvent[], Error> }>) {
  if (query.isLoading) return <LoadingState compact />;
  if (query.isError) return <ErrorState message={getApiErrorMessage(query.error)} onRetry={() => query.refetch()} />;
  if (!query.data?.length) return <EmptyState title="No audit events yet" description="Governed activity will appear here." />;

  return (
    <section className="panel audit-panel">
      <div className="panel-header">
        <div>
          <span className="panel-eyebrow">Append-only activity</span>
          <h2>Audit events</h2>
        </div>
        <ScrollText />
      </div>
      <div className="audit-list">
        {query.data.map(item => (
          <article key={item.id}>
            <span>{item.actorType}</span>
            <div>
              <strong>{item.action}</strong>
              <p>{item.actorDisplay} · {item.resourceType}</p>
              <small>Correlation {item.correlationId}</small>
            </div>
            <time>{new Date(item.occurredAt).toLocaleString("en-ZA")}</time>
          </article>
        ))}
      </div>
    </section>
  );
}

function WorkspaceTab({
  tab,
  workspaceId,
  onInvalidate,
}: Readonly<{ tab: Tab; workspaceId: string; onInvalidate: (key: string) => void }>) {
  const workspace = useQuery({ queryKey: ["workspace", workspaceId], queryFn: () => getWorkspace(workspaceId), enabled: tab === "Overview" || tab === "Settings" });
  const members = useQuery({ queryKey: ["workspace-members", workspaceId], queryFn: () => getMembers(workspaceId), enabled: tab === "Members" || tab === "Roles" });
  const accounts = useQuery({ queryKey: ["service-accounts", workspaceId], queryFn: () => getServiceAccounts(workspaceId), enabled: tab === "Service Accounts" });
  const audit = useQuery({ queryKey: ["workspace-audit", workspaceId], queryFn: () => getAudit(workspaceId), enabled: tab === "Audit" });

  switch (tab) {
    case "Overview":
      return <WorkspaceOverviewPanel query={workspace} />;
    case "Members":
      return <MembersPanel workspaceId={workspaceId} query={members} onChanged={() => onInvalidate("workspace-members")} />;
    case "Roles":
      return <WorkspaceRolesPanel />;
    case "Service Accounts":
      return <ServiceAccountsPanel workspaceId={workspaceId} query={accounts} onChanged={() => onInvalidate("service-accounts")} />;
    case "Audit":
      return <WorkspaceAuditPanel query={audit} />;
    default:
      return <WorkspaceSettingsPanel workspaceId={workspaceId} />;
  }
}

function MembersPanel({
  workspaceId,
  query,
  onChanged,
}: Readonly<{ workspaceId: string; query: UseQueryResult<WorkspaceMember[], Error>; onChanged: () => void }>) {
  const [open, setOpen] = useState(false);
  const [email, setEmail] = useState("");
  const [name, setName] = useState("");
  const [role, setRole] = useState("Viewer");
  const mutation = useMutation({
    mutationFn: () => inviteMember(workspaceId, { email, displayName: name, role }),
    onSuccess: () => { setOpen(false); setEmail(""); setName(""); onChanged(); },
  });

  const renderContent = () => {
    if (query.isLoading) return <LoadingState compact />;
    if (query.isError) return <ErrorState message={getApiErrorMessage(query.error)} onRetry={() => query.refetch()} />;
    return (
      <div className="member-list">
        {query.data?.map(item => (
          <article key={item.id}>
            <span className="member-avatar">{item.displayName.split(" ").map(value => value[0]).slice(0, 2).join("")}</span>
            <div>
              <strong>{item.displayName}</strong>
              <small>{item.email}</small>
            </div>
            <span className="role-pill">{item.role}</span>
            <span>{item.status}</span>
          </article>
        ))}
      </div>
    );
  };

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="panel-eyebrow">People</span>
          <h2>Members</h2>
        </div>
        <button type="button" className="primary-button" onClick={() => setOpen(value => !value)}>
          <UserPlus />Invite member
        </button>
      </div>
      {open && (
        <form className="workspace-inline-form" onSubmit={event => { event.preventDefault(); mutation.mutate(); }}>
          <input aria-label="Email" type="email" placeholder="name@company.com" value={email} onChange={event => setEmail(event.target.value)} required />
          <input aria-label="Display name" placeholder="Display name" value={name} onChange={event => setName(event.target.value)} required />
          <select aria-label="Role" value={role} onChange={event => setRole(event.target.value)}>
            {["Administrator", "Engineer", "Reviewer", "Operator", "Viewer"].map(item => <option key={item}>{item}</option>)}
          </select>
          <button type="submit" className="primary-button" disabled={mutation.isPending}>
            {mutation.isPending ? "Inviting…" : "Create invitation"}
          </button>
          {mutation.isError && <p role="alert">{getApiErrorMessage(mutation.error)}</p>}
        </form>
      )}
      {renderContent()}
    </section>
  );
}

function ServiceAccountsPanel({
  workspaceId,
  query,
  onChanged,
}: Readonly<{ workspaceId: string; query: UseQueryResult<ServiceAccount[], Error>; onChanged: () => void }>) {
  const [name, setName] = useState("");
  const [credential, setCredential] = useState<string>();
  const mutation = useMutation({
    mutationFn: () => createServiceAccount(workspaceId, { name, scopes: ["WorkspaceMember", "CanRunSimulation", "CanViewOperationalTrace"] }),
    onSuccess: (value: { credential: string }) => { setCredential(value.credential); setName(""); onChanged(); },
  });

  const renderContent = () => {
    if (query.isLoading) return <LoadingState compact />;
    if (query.isError) return <ErrorState message={getApiErrorMessage(query.error)} onRetry={() => query.refetch()} />;
    if (!query.data?.length) return <EmptyState title="No service accounts" description="Create a scoped identity for automation." />;
    return (
      <div className="member-list">
        {query.data.map(item => (
          <article key={item.id}>
            <KeyRound />
            <div>
              <strong>{item.name}</strong>
              <small>Last used {item.lastUsedAt ? new Date(item.lastUsedAt).toLocaleString("en-ZA") : "Never"}</small>
            </div>
            <span className="role-pill">{item.status}</span>
            <span>r{item.revision}</span>
          </article>
        ))}
      </div>
    );
  };

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="panel-eyebrow">Machine identities</span>
          <h2>Service accounts</h2>
        </div>
      </div>
      <form className="workspace-inline-form" onSubmit={event => { event.preventDefault(); mutation.mutate(); }}>
        <input aria-label="Service account name" value={name} onChange={event => setName(event.target.value)} placeholder="Automation runner" required />
        <button type="submit" className="primary-button" disabled={mutation.isPending}>
          <KeyRound />{mutation.isPending ? "Creating…" : "Create credential"}
        </button>
      </form>
      {credential && (
        <output className="credential-reveal">
          <strong>Copy this credential now</strong>
          <code>{credential}</code>
          <small>It is never stored or shown again.</small>
        </output>
      )}
      {renderContent()}
    </section>
  );
}

function WorkspaceSettingsPanel({ workspaceId }: Readonly<{ workspaceId: string }>) {
  const workspace = useQuery({ queryKey: ["workspace", workspaceId], queryFn: () => getWorkspace(workspaceId) });
  const members = useQuery({ queryKey: ["workspace-members", workspaceId], queryFn: () => getMembers(workspaceId) });
  const queryClient = useQueryClient();

  if (workspace.isLoading) return <LoadingState />;
  if (workspace.isError) return <ErrorState message={getApiErrorMessage(workspace.error)} onRetry={() => workspace.refetch()} />;

  const reloadWorkspace = () => queryClient.invalidateQueries({ queryKey: ["workspace", workspaceId] });

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "24px", maxWidth: "800px", margin: "0 auto", padding: "24px" }}>
      <article className="panel ws-settings-card">
        <span className="panel-eyebrow">General</span>
        <h2>Workspace Configuration</h2>
        <WorkspaceConfigForm workspaceId={workspaceId} initialName={workspace.data?.name ?? ""} initialDescription={workspace.data?.description ?? ""} expectedRevision={workspace.data?.revision ?? 0} onSaved={reloadWorkspace} />
      </article>

      <article className="panel ws-settings-card">
        <span className="panel-eyebrow">Platform Configuration</span>
        <h2>Runtime Settings & Environment Guardrails</h2>
        <p style={{ color: "var(--muted, #64748b)", margin: "0.5rem 0 1.25rem", fontSize: "0.95rem" }}>
          AI provider credentials (like <code>GEMINI_API_KEY</code>), model parameters, ZAR budgets, evaluation thresholds, and retention policies are managed centrally in <strong>Settings</strong> with environment-scoped inheritance.
        </p>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "12px" }}>
          <Link to="/settings/provider" className="settings-overview-card" style={{ padding: "14px", border: "1px solid var(--border-color, #e2e8f0)", borderRadius: "8px", textDecoration: "none", color: "inherit", display: "flex", flexDirection: "column", gap: "6px" }}>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
              <span style={{ display: "inline-flex", alignItems: "center", gap: "6px", fontWeight: 600 }}><Bot size={16} /> AI Provider & Model</span>
              <ArrowRight size={14} />
            </div>
            <small style={{ color: "var(--muted, #64748b)" }}>Configure Google Gemini provider, temperature, and tokens.</small>
          </Link>
          <Link to="/settings/secrets" className="settings-overview-card" style={{ padding: "14px", border: "1px solid var(--border-color, #e2e8f0)", borderRadius: "8px", textDecoration: "none", color: "inherit", display: "flex", flexDirection: "column", gap: "6px" }}>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
              <span style={{ display: "inline-flex", alignItems: "center", gap: "6px", fontWeight: 600 }}><KeyRound size={16} /> API Keys & Secrets</span>
              <ArrowRight size={14} />
            </div>
            <small style={{ color: "var(--muted, #64748b)" }}>Register and validate env:GEMINI_API_KEY references.</small>
          </Link>
          <Link to="/settings/budgets" className="settings-overview-card" style={{ padding: "14px", border: "1px solid var(--border-color, #e2e8f0)", borderRadius: "8px", textDecoration: "none", color: "inherit", display: "flex", flexDirection: "column", gap: "6px" }}>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
              <span style={{ display: "inline-flex", alignItems: "center", gap: "6px", fontWeight: 600 }}><Sliders size={16} /> Budgets & Retentions</span>
              <ArrowRight size={14} />
            </div>
            <small style={{ color: "var(--muted, #64748b)" }}>Manage monthly ZAR budget caps and trace retention days.</small>
          </Link>
        </div>
      </article>

      <article className="panel ws-settings-card">
        <span className="panel-eyebrow">Member Access</span>
        <h2>Force Password Reset</h2>
        <WorkspacePasswordResetForm members={members.data ?? []} />
      </article>

      <article className="panel ws-settings-card danger-zone">
        <span className="panel-eyebrow">Danger Zone</span>
        <h2>Destructive Actions</h2>
        <p>Suspending or deleting a workspace will immediately revoke access for all members and halt all running AI jobs. This cannot be easily undone.</p>
        <div style={{ display: "flex", gap: "1rem", marginTop: "1.5rem", flexWrap: "wrap" }}>
          <button
            type="button"
            className="secondary-button danger"
            onClick={() => {
              if (window.confirm(`Are you sure you want to SUSPEND "${workspace.data?.name}"?\n\nThis will lock out all members immediately. You can reactivate the workspace later.`)) {
                void suspendWorkspace(workspaceId, workspace.data?.revision ?? 0).then(reloadWorkspace, () => {});
              }
            }}
          >
            Suspend workspace
          </button>
          <button
            type="button"
            className="primary-button danger"
            onClick={() => {
              const confirmed = window.prompt(`This will permanently DELETE "${workspace.data?.name}" and all its data.\n\nType the workspace name to confirm:`);
              if (confirmed === workspace.data?.name) {
                void deleteWorkspace(workspaceId, workspace.data?.revision ?? 0).then(() => window.location.reload(), () => {});
              } else if (confirmed !== null) {
                alert("Workspace name did not match. Deletion cancelled.");
              }
            }}
          >
            Delete workspace
          </button>
        </div>
      </article>
    </div>
  );
}

function WorkspaceConfigForm({
  workspaceId,
  initialName,
  initialDescription,
  expectedRevision,
  onSaved,
}: Readonly<{
  workspaceId: string;
  initialName: string;
  initialDescription: string;
  expectedRevision: number;
  onSaved: () => void;
}>) {
  const [name, setName] = useState(initialName);
  const [desc, setDesc] = useState(initialDescription);
  const mutation = useMutation({ mutationFn: () => updateWorkspace(workspaceId, { name, description: desc, expectedRevision }), onSuccess: onSaved });

  return (
    <form className="ws-settings-form" onSubmit={event => { event.preventDefault(); mutation.mutate(); }}>
      <div className="ws-field-group">
        <label className="ws-field">
          <span>Workspace Name</span>
          <input value={name} onChange={event => setName(event.target.value)} required placeholder="e.g. Production AI" />
        </label>
        <label className="ws-field">
          <span>Description</span>
          <input value={desc} onChange={event => setDesc(event.target.value)} placeholder="A short description of this workspace" />
        </label>
      </div>
      {mutation.isError && <p className="ws-error">{getApiErrorMessage(mutation.error)}</p>}
      <div className="ws-form-actions">
        <button type="submit" className="primary-button" disabled={mutation.isPending}>{mutation.isPending ? "Saving..." : "Save changes"}</button>
      </div>
    </form>
  );
}

function WorkspacePasswordResetForm({
  members,
}: Readonly<{
  members: { userId: string; displayName: string; email: string }[];
}>) {
  const [userId, setUserId] = useState("");
  const [password, setPassword] = useState("");
  const resetMutation = useMutation({
    mutationFn: () => {
      const member = members.find(item => item.userId === userId);
      if (!window.confirm(`Reset the password for ${member?.displayName ?? "this user"}?\n\nThey will need to log in with the new password immediately.`)) {
        return Promise.reject(new Error("Cancelled"));
      }
      return resetPassword(userId, { password });
    },
    onSuccess: () => { setUserId(""); setPassword(""); alert("Password reset successfully."); },
  });

  return (
    <form className="ws-settings-form" onSubmit={event => { event.preventDefault(); resetMutation.mutate(); }}>
      <p style={{ color: "var(--muted, #64748b)", fontSize: "0.9rem", marginBottom: "1rem" }}>
        As an Administrator, you can set a new password for any local account. This takes effect immediately.
      </p>
      <div className="ws-field-group">
        <label className="ws-field">
          <span>Member</span>
          <select value={userId} onChange={event => setUserId(event.target.value)} required>
            <option value="">Select a member...</option>
            {members.map(member => <option key={member.userId} value={member.userId}>{member.displayName} ({member.email})</option>)}
          </select>
        </label>
        <label className="ws-field">
          <span>New Password <small>(min. 12 characters)</small></span>
          <input type="password" placeholder="New password" value={password} onChange={event => setPassword(event.target.value)} required minLength={12} />
        </label>
      </div>
      {resetMutation.isError && <p className="ws-error">{getApiErrorMessage(resetMutation.error)}</p>}
      <div className="ws-form-actions">
        <button type="submit" className="secondary-button" disabled={resetMutation.isPending || !userId}>{resetMutation.isPending ? "Resetting..." : "Reset password"}</button>
      </div>
    </form>
  );
}
