import { useHelp } from "../contexts/HelpContext";
import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { Building2, Check, KeyRound, ScrollText, Shield, UserPlus } from "lucide-react";
import { EmptyState, ErrorState, LoadingState } from "../components/AsyncStates";
import { useAuth } from "../contexts/useAuth";
import { createServiceAccount, getAudit, getMembers, getServiceAccounts, getWorkspace, inviteMember, resetPassword, updateWorkspace, suspendWorkspace, deleteWorkspace, type ServiceAccount, type WorkspaceMember } from "../services/authApi";
import { getApiErrorMessage } from "../services/apiClient";
import { listWorkspaceSettings, upsertWorkspaceSetting, listSecretReferences, createSecretReference } from "../services/settingsApi";

const tabs = ["Overview", "Members", "Roles", "Service Accounts", "Audit", "Settings"] as const;
type Tab = (typeof tabs)[number];

export function WorkspacePage({ selectionOnly = false }: { selectionOnly?: boolean }) {
  useHelp({
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
  });

  const auth = useAuth();
  const queryClient = useQueryClient();
  const [tab, setTab] = useState<Tab>("Overview");
  const active = auth.session?.workspaces.find(item => item.id === auth.session?.activeWorkspaceId);
  if (selectionOnly) return <section className="workspace-select-page"><header><span className="page-heading-icon"><Building2 /></span><div><span className="panel-eyebrow">Workspace access</span><h1>Select a workspace</h1><p>Your active workspace scopes every capability and query.</p></div></header><div className="workspace-choice-grid">{auth.session?.workspaces.map(item => <button key={item.id} className={`workspace-choice-card${item.id === active?.id ? " selected" : ""}`} onClick={() => auth.switchWorkspace(item.id)}><span><Building2 /></span><div><strong>{item.name}</strong><small>{item.role}</small></div>{item.id === active?.id && <Check />}</button>)}</div></section>;
  if (!active) return <EmptyState title="No active workspace" description="Select a workspace to continue." />;
  const isAdmin = active.role === "Administrator";
  return <section className="workspace-admin-page page-stack"><header className="page-heading"><span className="page-heading-icon"><Building2 /></span><div><span className="panel-eyebrow">Organisation and access</span><h1>{active.name}</h1><p>Manage members, service identities, roles, security activity, and workspace settings.</p></div><select aria-label="Active workspace" value={active.id} onChange={event => auth.switchWorkspace(event.target.value)}>{auth.session?.workspaces.map(item => <option value={item.id} key={item.id}>{item.name} · {item.role}</option>)}</select></header><nav className="workspace-tabs" aria-label="Workspace administration">{tabs.map(item => <button key={item} className={tab === item ? "active" : ""} onClick={() => setTab(item)}>{item}</button>)}</nav>{!isAdmin && tab !== "Overview" ? <div className="panel restricted-state"><Shield /><h2>Administrator permission required</h2><p>Your {active.role} role can use workspace capabilities but cannot manage access.</p></div> : <WorkspaceTab tab={tab} workspaceId={active.id} onInvalidate={key => queryClient.invalidateQueries({ queryKey: [key, active.id] })} />}</section>;
}

function WorkspaceTab({ tab, workspaceId, onInvalidate }: { tab: Tab; workspaceId: string; onInvalidate: (key: string) => void }) {
  const workspace = useQuery({ queryKey: ["workspace", workspaceId], queryFn: () => getWorkspace(workspaceId), enabled: tab === "Overview" || tab === "Settings" });
  const members = useQuery({ queryKey: ["workspace-members", workspaceId], queryFn: () => getMembers(workspaceId), enabled: tab === "Members" || tab === "Roles" });
  const accounts = useQuery({ queryKey: ["service-accounts", workspaceId], queryFn: () => getServiceAccounts(workspaceId), enabled: tab === "Service Accounts" });
  const audit = useQuery({ queryKey: ["workspace-audit", workspaceId], queryFn: () => getAudit(workspaceId), enabled: tab === "Audit" });
  if (tab === "Overview") return <div className="workspace-overview-grid"><article className="panel"><span className="panel-eyebrow">Workspace</span>{workspace.isLoading ? <LoadingState compact /> : workspace.isError ? <ErrorState message={getApiErrorMessage(workspace.error)} onRetry={() => workspace.refetch()} /> : <><h2>{workspace.data?.name}</h2><dl><div><dt>Status</dt><dd>{workspace.data?.status}</dd></div><div><dt>Slug</dt><dd>{workspace.data?.slug}</dd></div><div><dt>Revision</dt><dd>{workspace.data?.revision}</dd></div></dl><p>{workspace.data?.description}</p></>}</article><article className="panel"><span className="panel-eyebrow">Isolation</span><h2>Server-enforced ownership</h2><p>Every capability root is filtered by the trusted workspace context. Guessed cross-workspace identifiers resolve as not found.</p></article></div>;
  if (tab === "Members") return <MembersPanel workspaceId={workspaceId} query={members} onChanged={() => onInvalidate("workspace-members")} />;
  if (tab === "Roles") return <section className="panel role-grid">{[{ name: "Administrator", copy: "Full workspace, membership, service-account, governance, publication, and execution access." }, { name: "Engineer", copy: "Build assets, draft governance, execute simulations and replays, and inspect sensitive traces." }, { name: "Reviewer", copy: "Review and publish controlled assets, evaluations, replays, and policies." }, { name: "Operator", copy: "Run approved workloads and inspect operational traces, decisions, and provider health." }, { name: "Viewer", copy: "Read non-sensitive workspace resources." }].map(role => <article key={role.name}><Shield /><div><h3>{role.name}</h3><p>{role.copy}</p></div></article>)}</section>;
  if (tab === "Service Accounts") return <ServiceAccountsPanel workspaceId={workspaceId} query={accounts} onChanged={() => onInvalidate("service-accounts")} />;
  if (tab === "Audit") return <section className="panel audit-panel"><div className="panel-header"><div><span className="panel-eyebrow">Append-only activity</span><h2>Audit events</h2></div><ScrollText /></div>{audit.isLoading ? <LoadingState compact /> : audit.isError ? <ErrorState message={getApiErrorMessage(audit.error)} onRetry={() => audit.refetch()} /> : !audit.data?.length ? <EmptyState title="No audit events yet" description="Governed activity will appear here." /> : <div className="audit-list">{audit.data.map(item => <article key={item.id}><span>{item.actorType}</span><div><strong>{item.action}</strong><p>{item.actorDisplay} · {item.resourceType}</p><small>Correlation {item.correlationId}</small></div><time>{new Date(item.occurredAt).toLocaleString("en-ZA")}</time></article>)}</div>}</section>;
  return <WorkspaceSettingsPanel workspaceId={workspaceId} />;
}

function MembersPanel({ workspaceId, query, onChanged }: { workspaceId: string; query: UseQueryResult<WorkspaceMember[], Error>; onChanged: () => void }) {
  const [open, setOpen] = useState(false); const [email, setEmail] = useState(""); const [name, setName] = useState(""); const [role, setRole] = useState("Viewer");
  const mutation = useMutation({ mutationFn: () => inviteMember(workspaceId, { email, displayName: name, role }), onSuccess: () => { setOpen(false); setEmail(""); setName(""); onChanged(); } });
  return <section className="panel"><div className="panel-header"><div><span className="panel-eyebrow">People</span><h2>Members</h2></div><button className="primary-button" onClick={() => setOpen(value => !value)}><UserPlus />Invite member</button></div>{open && <form className="workspace-inline-form" onSubmit={(event: FormEvent) => { event.preventDefault(); mutation.mutate(); }}><input aria-label="Email" type="email" placeholder="name@company.com" value={email} onChange={event => setEmail(event.target.value)} required /><input aria-label="Display name" placeholder="Display name" value={name} onChange={event => setName(event.target.value)} required /><select aria-label="Role" value={role} onChange={event => setRole(event.target.value)}>{["Administrator", "Engineer", "Reviewer", "Operator", "Viewer"].map(item => <option key={item}>{item}</option>)}</select><button className="primary-button" disabled={mutation.isPending}>{mutation.isPending ? "Inviting…" : "Create invitation"}</button>{mutation.isError && <p role="alert">{getApiErrorMessage(mutation.error)}</p>}</form>}{query.isLoading ? <LoadingState compact /> : query.isError ? <ErrorState message={getApiErrorMessage(query.error)} onRetry={() => query.refetch()} /> : <div className="member-list">{query.data?.map(item => <article key={item.id}><span className="member-avatar">{item.displayName.split(" ").map(value => value[0]).slice(0, 2).join("")}</span><div><strong>{item.displayName}</strong><small>{item.email}</small></div><span className="role-pill">{item.role}</span><span>{item.status}</span></article>)}</div>}</section>;
}

function ServiceAccountsPanel({ workspaceId, query, onChanged }: { workspaceId: string; query: UseQueryResult<ServiceAccount[], Error>; onChanged: () => void }) {
  const [name, setName] = useState(""); const [credential, setCredential] = useState<string>();
  const mutation = useMutation({ mutationFn: () => createServiceAccount(workspaceId, { name, scopes: ["WorkspaceMember", "CanRunSimulation", "CanViewOperationalTrace"] }), onSuccess: (value: { credential: string }) => { setCredential(value.credential); setName(""); onChanged(); } });
  return <section className="panel"><div className="panel-header"><div><span className="panel-eyebrow">Machine identities</span><h2>Service accounts</h2></div></div><form className="workspace-inline-form" onSubmit={(event: FormEvent) => { event.preventDefault(); mutation.mutate(); }}><input aria-label="Service account name" value={name} onChange={event => setName(event.target.value)} placeholder="Automation runner" required /><button className="primary-button" disabled={mutation.isPending}><KeyRound />{mutation.isPending ? "Creating…" : "Create credential"}</button></form>{credential && <div className="credential-reveal" role="status"><strong>Copy this credential now</strong><code>{credential}</code><small>It is never stored or shown again.</small></div>}{query.isLoading ? <LoadingState compact /> : query.isError ? <ErrorState message={getApiErrorMessage(query.error)} onRetry={() => query.refetch()} /> : !query.data?.length ? <EmptyState title="No service accounts" description="Create a scoped identity for automation." /> : <div className="member-list">{query.data.map(item => <article key={item.id}><KeyRound /><div><strong>{item.name}</strong><small>Last used {item.lastUsedAt ? new Date(item.lastUsedAt).toLocaleString("en-ZA") : "Never"}</small></div><span className="role-pill">{item.status}</span><span>r{item.revision}</span></article>)}</div>}</section>;
}

function WorkspaceSettingsPanel({ workspaceId }: { workspaceId: string }) {
  const workspace = useQuery({ queryKey: ["workspace", workspaceId], queryFn: () => getWorkspace(workspaceId) });
  const members = useQuery({ queryKey: ["workspace-members", workspaceId], queryFn: () => getMembers(workspaceId) });
  const settings = useQuery({ queryKey: ["workspace-settings", workspaceId], queryFn: () => listWorkspaceSettings(workspaceId) });
  const secrets = useQuery({ queryKey: ["secret-references", workspaceId], queryFn: () => listSecretReferences(workspaceId) });
  const queryClient = useQueryClient();

  if (workspace.isLoading || settings.isLoading) return <LoadingState />;
  if (workspace.isError) return <ErrorState message={getApiErrorMessage(workspace.error)} onRetry={() => workspace.refetch()} />;

  const reloadSettings = () => queryClient.invalidateQueries({ queryKey: ["workspace-settings", workspaceId] });
  const reloadWorkspace = () => queryClient.invalidateQueries({ queryKey: ["workspace", workspaceId] });
  const reloadSecrets = () => queryClient.invalidateQueries({ queryKey: ["secret-references", workspaceId] });

  const getSetting = (key: string) => {
    const s = settings.data?.find(s => s.definitionKey === key);
    return s ? JSON.parse(s.valueJson) : undefined;
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "24px", maxWidth: "800px", margin: "0 auto", padding: "24px" }}>
      <article className="panel ws-settings-card">
        <span className="panel-eyebrow">General</span>
        <h2>Workspace Configuration</h2>
        <WorkspaceConfigForm workspaceId={workspaceId} initialName={workspace.data?.name ?? ""} initialDescription={workspace.data?.description ?? ""} expectedRevision={workspace.data?.revision ?? 0} onSaved={reloadWorkspace} />
      </article>

      <article className="panel ws-settings-card">
        <span className="panel-eyebrow">AI Configuration</span>
        <h2>AI & Defaults</h2>
        <WorkspaceDefaultsForm workspaceId={workspaceId} 
          initialProvider={getSetting("workspace.ai.default_llm_provider") ?? "openai"}
          initialBudget={getSetting("workspace.ai.budget_limit_zar") ?? 1000}
          initialRetention={getSetting("workspace.data.retention_days") ?? 30}
          onSaved={reloadSettings} />
      </article>

      <article className="panel ws-settings-card">
        <span className="panel-eyebrow">Security</span>
        <h2>Security Policies</h2>
        <WorkspaceSecurityForm workspaceId={workspaceId} 
          initialMfa={getSetting("workspace.security.require_mfa") ?? false} 
          members={members.data ?? []} 
          onSaved={reloadSettings} />
      </article>

      <article className="panel ws-settings-card">
        <span className="panel-eyebrow">Integration</span>
        <h2>API Keys & Secrets</h2>
        <WorkspaceSecretsForm workspaceId={workspaceId} secrets={secrets.data ?? []} onSaved={reloadSecrets} />
      </article>

      <article className="panel ws-settings-card danger-zone">
        <span className="panel-eyebrow">Danger Zone</span>
        <h2>Destructive Actions</h2>
        <p>Suspending or deleting a workspace will immediately revoke access for all members and halt all running AI jobs. This cannot be easily undone.</p>
        <div style={{ display: "flex", gap: "1rem", marginTop: "1.5rem", flexWrap: "wrap" }}>
          <button className="secondary-button danger" onClick={() => {
            if (window.confirm(`Are you sure you want to SUSPEND "${workspace.data?.name}"?\n\nThis will lock out all members immediately. You can reactivate the workspace later.`))
              suspendWorkspace(workspaceId, workspace.data?.revision ?? 0).then(reloadWorkspace);
          }}>Suspend workspace</button>
          <button className="primary-button danger" onClick={() => {
            const confirmed = window.prompt(`This will permanently DELETE "${workspace.data?.name}" and all its data.\n\nType the workspace name to confirm:`);
            if (confirmed === workspace.data?.name)
              deleteWorkspace(workspaceId, workspace.data?.revision ?? 0).then(() => window.location.reload());
            else if (confirmed !== null)
              alert("Workspace name did not match. Deletion cancelled.");
          }}>Delete workspace</button>
        </div>
      </article>
    </div>
  );
}

function WorkspaceConfigForm({ workspaceId, initialName, initialDescription, expectedRevision, onSaved }: any) {
  const [name, setName] = useState(initialName);
  const [desc, setDesc] = useState(initialDescription);
  const mutation = useMutation({ mutationFn: () => updateWorkspace(workspaceId, { name, description: desc, expectedRevision }), onSuccess: onSaved });
  return (
    <form className="ws-settings-form" onSubmit={e => { e.preventDefault(); mutation.mutate(); }}>
      <div className="ws-field-group">
        <label className="ws-field">
          <span>Workspace Name</span>
          <input value={name} onChange={e => setName(e.target.value)} required placeholder="e.g. Production AI" />
        </label>
        <label className="ws-field">
          <span>Description</span>
          <input value={desc} onChange={e => setDesc(e.target.value)} placeholder="A short description of this workspace" />
        </label>
      </div>
      {mutation.isError && <p className="ws-error">{getApiErrorMessage(mutation.error)}</p>}
      <div className="ws-form-actions">
        <button className="primary-button" disabled={mutation.isPending}>{mutation.isPending ? "Saving..." : "Save changes"}</button>
      </div>
    </form>
  );
}

function WorkspaceDefaultsForm({ workspaceId, initialProvider, initialBudget, initialRetention, onSaved }: any) {
  const [provider, setProvider] = useState(initialProvider);
  const [budget, setBudget] = useState(initialBudget);
  const [retention, setRetention] = useState(initialRetention);
  const mutation = useMutation({ 
    mutationFn: async () => {
      await upsertWorkspaceSetting(workspaceId, "workspace.ai.default_llm_provider", { valueJson: JSON.stringify(provider) });
      await upsertWorkspaceSetting(workspaceId, "workspace.ai.budget_limit_zar", { valueJson: JSON.stringify(Number(budget)) });
      await upsertWorkspaceSetting(workspaceId, "workspace.data.retention_days", { valueJson: JSON.stringify(Number(retention)) });
    }, 
    onSuccess: onSaved 
  });
  return (
    <form className="ws-settings-form" onSubmit={e => { e.preventDefault(); mutation.mutate(); }}>
      <div className="ws-field-group">
        <label className="ws-field">
          <span>Default LLM Provider</span>
          <select value={provider} onChange={e => setProvider(e.target.value)}>
            <option value="openai">OpenAI</option>
            <option value="anthropic">Anthropic</option>
            <option value="google">Google Gemini</option>
          </select>
        </label>
        <label className="ws-field">
          <span>Monthly Budget Limit (ZAR)</span>
          <input type="number" min={0} value={budget} onChange={e => setBudget(e.target.value)} placeholder="e.g. 5000" />
        </label>
        <label className="ws-field">
          <span>Data Retention (Days)</span>
          <input type="number" min={1} value={retention} onChange={e => setRetention(e.target.value)} placeholder="e.g. 30" />
        </label>
      </div>
      {mutation.isError && <p className="ws-error">{getApiErrorMessage(mutation.error)}</p>}
      <div className="ws-form-actions">
        <button className="primary-button" disabled={mutation.isPending}>{mutation.isPending ? "Saving..." : "Save defaults"}</button>
      </div>
    </form>
  );
}

function WorkspaceSecurityForm({ workspaceId, initialMfa, members, onSaved }: any) {
  const [mfa, setMfa] = useState(initialMfa);
  const [userId, setUserId] = useState("");
  const [password, setPassword] = useState("");
  const settingsMutation = useMutation({ mutationFn: () => upsertWorkspaceSetting(workspaceId, "workspace.security.require_mfa", { valueJson: JSON.stringify(mfa) }), onSuccess: onSaved });
  const resetMutation = useMutation({
    mutationFn: () => {
      const member = members.find((m: any) => m.userId === userId);
      if (!window.confirm(`Reset the password for ${member?.displayName ?? "this user"}?\n\nThey will need to log in with the new password immediately.`))
        return Promise.reject(new Error("Cancelled"));
      return resetPassword(userId, { password });
    },
    onSuccess: () => { setUserId(""); setPassword(""); alert("Password reset successfully."); }
  });

  return (
    <div className="ws-settings-form">
      <label className="ws-toggle">
        <input type="checkbox" checked={mfa} onChange={e => { setMfa(e.target.checked); settingsMutation.mutate(); }} />
        <span>Require Multi-Factor Authentication for all members</span>
      </label>

      <div className="ws-section-divider">
        <h3>Force Password Reset</h3>
        <p>As an Administrator you can set a new password for any local (email/password) member. This is not reversible — the member will need to use the new password immediately.</p>
        <form className="ws-settings-form" style={{ marginTop: "1rem" }} onSubmit={e => { e.preventDefault(); resetMutation.mutate(); }}>
          <div className="ws-field-group">
            <label className="ws-field">
              <span>Member</span>
              <select value={userId} onChange={e => setUserId(e.target.value)} required>
                <option value="">Select a member...</option>
                {members.map((m: any) => <option key={m.userId} value={m.userId}>{m.displayName} ({m.email})</option>)}
              </select>
            </label>
            <label className="ws-field">
              <span>New Password <small>(min. 12 characters)</small></span>
              <input type="password" placeholder="New password" value={password} onChange={e => setPassword(e.target.value)} required minLength={12} />
            </label>
          </div>
          {resetMutation.isError && <p className="ws-error">{getApiErrorMessage(resetMutation.error)}</p>}
          <div className="ws-form-actions">
            <button className="secondary-button" disabled={resetMutation.isPending || !userId}>{resetMutation.isPending ? "Resetting..." : "Reset password"}</button>
          </div>
        </form>
      </div>
    </div>
  );
}

function WorkspaceSecretsForm({ workspaceId, secrets, onSaved }: any) {
  const [name, setName] = useState("");
  const [type, setType] = useState("api-key");
  const [value, setValue] = useState("");
  const mutation = useMutation({ mutationFn: () => createSecretReference(workspaceId, name, value), onSuccess: () => { setName(""); setValue(""); onSaved(); } });

  return (
    <div className="ws-settings-form">
      <div className="ws-field-group">
        <label className="ws-field">
          <span>Secret Name</span>
          <input placeholder="e.g. OPENAI_API_KEY" value={name} onChange={e => setName(e.target.value)} required />
        </label>
        <label className="ws-field">
          <span>Type</span>
          <select value={type} onChange={e => setType(e.target.value)}>
            <option value="api-key">API Key</option>
            <option value="connection-string">Connection String</option>
          </select>
        </label>
        <label className="ws-field" style={{ gridColumn: "1 / -1" }}>
          <span>Secret Value</span>
          <input type="password" placeholder="Paste your secret here" value={value} onChange={e => setValue(e.target.value)} required />
        </label>
      </div>
      {mutation.isError && <p className="ws-error">{getApiErrorMessage(mutation.error)}</p>}
      <div className="ws-form-actions">
        <button className="primary-button" disabled={mutation.isPending}>{mutation.isPending ? "Adding..." : "Add Secret"}</button>
      </div>
      {secrets.length > 0 && (
        <div className="member-list" style={{ marginTop: "1.5rem" }}>
          {secrets.map((s: any) => (
            <article key={s.id}>
              <KeyRound />
              <div>
                <strong>{s.name}</strong>
                <small>{s.type} · Added {new Date(s.createdAt).toLocaleDateString()}</small>
              </div>
              <span className="role-pill">{s.status}</span>
            </article>
          ))}
        </div>
      )}
    </div>
  );
}
