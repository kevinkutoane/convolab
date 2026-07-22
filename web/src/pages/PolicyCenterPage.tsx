import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import {
  Activity,
  BadgeCheck,
  Ban,
  BookOpen,
  BookLock,
  CheckCircle2,
  Clock3,
  CopyPlus,
  FileCheck2,
  Filter,
  History,
  KeyRound,
  LoaderCircle,
  LockKeyhole,
  Pencil,
  Play,
  Plus,
  RefreshCw,
  Save,
  Scale,
  ShieldCheck,
  ShieldX,
  SlidersHorizontal,
  Sparkles,
  X,
  XCircle,
} from "lucide-react";
import { MetricCard } from "../components/MetricCard";
import { getApiErrorMessage } from "../services/apiClient";
import {
  createPolicy,
  createPolicyVersion,
  evaluatePolicy,
  getPolicy,
  getPolicyOverview,
  transitionPolicy,
  updatePolicy,
} from "../services/policyApi";
import type {
  CreatePolicyInput,
  PolicyDetail,
  PolicyDomain,
  PolicyEffect,
  PolicyEvaluationResult,
  PolicyRuleInput,
  PolicyScope,
  PolicyStatus,
} from "../types/policy";

const domains: PolicyDomain[] = [
  "ProviderAccess",
  "ModelAccess",
  "BudgetLimit",
  "Safety",
  "KnowledgeAccess",
  "PromptApproval",
  "EvaluationThreshold",
  "RateLimit",
  "Compliance",
  "TenantRule",
];

const effects: PolicyEffect[] = ["Allow", "AllowWithConstraints", "Deny"];
const scopes: PolicyScope[] = ["Global", "Environment", "Tenant"];

type RuleForm = {
  name: string;
  effect: PolicyEffect;
  priority: number;
  matchText: string;
  constraintsText: string;
};

type PolicyForm = {
  name: string;
  description: string;
  owner: string;
  domain: PolicyDomain;
  defaultEffect: PolicyEffect;
  scope: PolicyScope;
  environment: string;
  tenantId: string;
  rules: RuleForm[];
};

const emptyForm: PolicyForm = {
  name: "",
  description: "",
  owner: "Platform Engineering",
  domain: "ProviderAccess",
  defaultEffect: "Deny",
  scope: "Global",
  environment: "All",
  tenantId: "",
  rules: [
    {
      name: "Allow approved execution",
      effect: "Allow",
      priority: 100,
      matchText: "provider=Deterministic",
      constraintsText: "",
    },
  ],
};

export function PolicyCenterPage() {
  const queryClient = useQueryClient();
  const [selectedPolicyId, setSelectedPolicyId] = useState("");
  const [domainFilter, setDomainFilter] = useState<PolicyDomain | "All">("All");
  const [statusFilter, setStatusFilter] = useState<PolicyStatus | "All">("All");
  const [editorMode, setEditorMode] = useState<"create" | "edit" | null>(null);
  const [form, setForm] = useState<PolicyForm>(emptyForm);
  const [evaluationDomain, setEvaluationDomain] = useState<PolicyDomain>("ProviderAccess");
  const [evaluationAttributes, setEvaluationAttributes] = useState("provider=Deterministic\nmodel=deterministic-primary\nenvironment=Development\nsource=Manual");
  const [evaluationResult, setEvaluationResult] = useState<PolicyEvaluationResult | null>(null);

  const overviewQuery = useQuery({ queryKey: ["policy-overview"], queryFn: getPolicyOverview });
  const overview = overviewQuery.data;
  const filteredPolicies = useMemo(() => (overview?.policies ?? []).filter(policy =>
    (domainFilter === "All" || policy.domain === domainFilter)
    && (statusFilter === "All" || policy.status === statusFilter)), [overview, domainFilter, statusFilter]);
  const effectivePolicyId = selectedPolicyId || filteredPolicies[0]?.id || overview?.policies[0]?.id || "";
  const detailQuery = useQuery({
    queryKey: ["policy-detail", effectivePolicyId],
    queryFn: () => getPolicy(effectivePolicyId),
    enabled: Boolean(effectivePolicyId),
  });
  const detail = detailQuery.data;

  const invalidate = async (id?: string) => {
    await queryClient.invalidateQueries({ queryKey: ["policy-overview"] });
    if (id) await queryClient.invalidateQueries({ queryKey: ["policy-detail", id] });
    await queryClient.invalidateQueries({ queryKey: ["simulations"] });
    await queryClient.invalidateQueries({ queryKey: ["trace-overview"] });
  };

  const createMutation = useMutation({
    mutationFn: (input: CreatePolicyInput) => createPolicy(input),
    onSuccess: async created => {
      setSelectedPolicyId(created.summary.id);
      setEditorMode(null);
      await invalidate(created.summary.id);
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ policyId, input }: { policyId: string; input: Parameters<typeof updatePolicy>[1] }) => updatePolicy(policyId, input),
    onSuccess: async updated => {
      setEditorMode(null);
      await invalidate(updated.summary.id);
    },
  });

  const actionMutation = useMutation({
    mutationFn: ({ policyId, action }: { policyId: string; action: string }) => transitionPolicy(policyId, action),
    onSuccess: async updated => {
      setSelectedPolicyId(updated.summary.id);
      await invalidate(updated.summary.id);
    },
  });

  const versionMutation = useMutation({
    mutationFn: ({ policyId, owner }: { policyId: string; owner: string }) => createPolicyVersion(policyId, owner),
    onSuccess: async created => {
      setSelectedPolicyId(created.summary.id);
      setForm(toForm(created));
      setEditorMode("edit");
      await invalidate(created.summary.id);
    },
  });

  const evaluationMutation = useMutation({
    mutationFn: () => evaluatePolicy({
      domain: evaluationDomain,
      attributes: parsePairs(evaluationAttributes),
      source: parsePairs(evaluationAttributes).source || "Manual",
    }),
    onSuccess: async result => {
      setEvaluationResult(result);
      await invalidate(effectivePolicyId);
    },
  });

  const error = overviewQuery.error ?? detailQuery.error ?? createMutation.error ?? updateMutation.error
    ?? actionMutation.error ?? versionMutation.error ?? evaluationMutation.error;

  const startCreate = () => {
    setForm({ ...emptyForm, rules: emptyForm.rules.map(rule => ({ ...rule })) });
    setEditorMode("create");
  };

  const startEdit = () => {
    if (!detail) return;
    setForm(toForm(detail));
    setEditorMode("edit");
  };

  const save = () => {
    const input = toInput(form);
    if (editorMode === "create") createMutation.mutate(input);
    else if (editorMode === "edit" && detail) updateMutation.mutate({
      policyId: detail.summary.id,
      input: { ...input, revision: detail.summary.revision },
    });
  };

  return (
    <div className="policy-center-page">
      <header className="studio-page-header policy-page-header">
        <div>
          <span className="page-eyebrow">Runtime governance capability</span>
          <h1>Policy Center</h1>
          <p>Define versioned governance rules once, evaluate them independently of execution engines, and audit every allow, constrained, or denied runtime decision.</p>
        </div>
        <div className="policy-header-actions">
          <Link className="secondary-button" to="/documentation/policies"><BookOpen size={16} /> Documentation</Link>
          <button className="secondary-button" onClick={() => overviewQuery.refetch()} disabled={overviewQuery.isFetching}>
            <RefreshCw size={16} className={overviewQuery.isFetching ? "spin" : ""} /> Refresh
          </button>
          <button className="primary-button" onClick={startCreate}><Plus size={16} /> New policy</button>
        </div>
      </header>

      {error && <div className="provider-warning"><XCircle size={16} /> {getApiErrorMessage(error)}</div>}

      <section className="metric-grid policy-metric-grid">
        <MetricCard icon={ShieldCheck} label="Logical policies" value={formatNumber(overview?.metrics.logicalPolicies)} detail={`${formatNumber(overview?.metrics.policyVersions)} immutable versions`} tone="accent" />
        <MetricCard icon={BadgeCheck} label="Active" value={formatNumber(overview?.metrics.activePolicies)} detail={`${formatNumber(overview?.metrics.draftPolicies)} drafts`} tone="positive" />
        <MetricCard icon={Activity} label="Decisions" value={formatNumber(overview?.metrics.decisions)} detail={`${formatNumber(overview?.metrics.constrainedDecisions)} constrained`} />
        <MetricCard icon={ShieldX} label="Denials" value={formatNumber(overview?.metrics.denials)} detail={`${formatPercent(overview?.metrics.denyRate)} deny rate`} tone={(overview?.metrics.denials ?? 0) > 0 ? "warning" : "default"} />
      </section>

      <section className="panel policy-coverage-panel">
        <div className="panel-header"><div><span className="panel-eyebrow">Governance coverage</span><h3>Policy domains</h3></div><Scale size={18} /></div>
        <div className="policy-coverage-grid">
          {(overview?.coverage ?? []).map(item => (
            <article key={item.domain} className={`policy-coverage-card ${item.activePolicies > 0 ? "covered" : "uncovered"}`}>
              <div><strong>{friendly(item.domain)}</strong><span>{item.status}</span></div>
              <dl><div><dt>Active</dt><dd>{item.activePolicies}</dd></div><div><dt>Decisions</dt><dd>{item.decisions}</dd></div><div><dt>Denied</dt><dd>{item.denials}</dd></div></dl>
            </article>
          ))}
        </div>
      </section>

      {editorMode && (
        <PolicyEditor
          mode={editorMode}
          form={form}
          onChange={setForm}
          onCancel={() => setEditorMode(null)}
          onSave={save}
          saving={createMutation.isPending || updateMutation.isPending}
        />
      )}

      <section className="policy-workspace">
        <aside className="panel policy-list-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Version registry</span><h3>Policies</h3></div><BookLock size={18} /></div>
          <div className="policy-filter-row">
            <label><Filter size={14} /><select value={domainFilter} onChange={event => setDomainFilter(event.target.value as PolicyDomain | "All")}><option>All</option>{domains.map(domain => <option key={domain}>{domain}</option>)}</select></label>
            <label><select value={statusFilter} onChange={event => setStatusFilter(event.target.value as PolicyStatus | "All")}><option>All</option>{["Draft", "PendingApproval", "Active", "Suspended", "Retired"].map(status => <option key={status}>{status}</option>)}</select></label>
          </div>
          <div className="policy-list">
            {filteredPolicies.map(policy => (
              <button key={policy.id} className={`policy-list-item ${effectivePolicyId === policy.id ? "selected" : ""}`} onClick={() => { setSelectedPolicyId(policy.id); setEditorMode(null); }}>
                <div><strong>{policy.name}</strong><span>{friendly(policy.domain)} · v{policy.version}</span></div>
                <PolicyPill effect={policy.defaultEffect} status={policy.status} />
                <small>{policy.scope} · {policy.ruleCount} rule{policy.ruleCount === 1 ? "" : "s"}</small>
              </button>
            ))}
            {filteredPolicies.length === 0 && <div className="empty-state compact-empty"><BookLock size={26} /><h3>No matching policies</h3><p>Adjust the filters or create a policy version.</p></div>}
          </div>
        </aside>

        <main className="panel policy-detail-panel">
          {!detail ? <div className="empty-state"><ShieldCheck size={34} /><h3>Select a policy</h3><p>Choose a policy version to inspect its lifecycle, rules, decisions, and version history.</p></div> : (
            <PolicyDetailView
              detail={detail}
              onEdit={startEdit}
              onAction={action => actionMutation.mutate({ policyId: detail.summary.id, action })}
              onVersion={() => versionMutation.mutate({ policyId: detail.summary.id, owner: detail.summary.owner })}
              onSelectVersion={setSelectedPolicyId}
              pending={actionMutation.isPending || versionMutation.isPending}
            />
          )}
        </main>
      </section>

      <section className="policy-lower-grid">
        <article className="panel policy-simulator-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Decision simulator</span><h3>Evaluate context</h3></div><Play size={18} /></div>
          <div className="policy-simulator-form">
            <label>Policy domain<select value={evaluationDomain} onChange={event => setEvaluationDomain(event.target.value as PolicyDomain)}>{domains.map(domain => <option key={domain}>{domain}</option>)}</select></label>
            <label>Context attributes<textarea rows={7} value={evaluationAttributes} onChange={event => setEvaluationAttributes(event.target.value)} /><small>One <code>key=value</code> pair per line. Rules use exact, case-insensitive matching.</small></label>
            <button className="primary-button" onClick={() => evaluationMutation.mutate()} disabled={evaluationMutation.isPending}>{evaluationMutation.isPending ? <LoaderCircle size={16} className="spin" /> : <Sparkles size={16} />} Evaluate policies</button>
          </div>
          {evaluationResult && <EvaluationResult result={evaluationResult} />}
        </article>

        <article className="panel policy-decisions-panel">
          <div className="panel-header"><div><span className="panel-eyebrow">Compliance audit</span><h3>Recent decisions</h3></div><History size={18} /></div>
          <div className="policy-decision-list">
            {(overview?.recentDecisions ?? []).slice(0, 16).map(decision => (
              <article key={decision.id} className={`policy-decision-row effect-${decision.effect.toLowerCase()}`}>
                <div className="policy-decision-icon">{decision.effect === "Deny" ? <Ban size={15} /> : decision.effect === "AllowWithConstraints" ? <SlidersHorizontal size={15} /> : <CheckCircle2 size={15} />}</div>
                <div><strong>{decision.policyName}</strong><span>{friendly(decision.domain)} · {decision.source}</span><p>{decision.reason}</p></div>
                <time>{formatDate(decision.createdAt)}</time>
              </article>
            ))}
            {(overview?.recentDecisions.length ?? 0) === 0 && <div className="empty-state compact-empty"><FileCheck2 size={26} /><h3>No decisions yet</h3><p>Use the simulator or run a conversation to create the first policy audit record.</p></div>}
          </div>
        </article>
      </section>
    </div>
  );
}

function PolicyEditor({ mode, form, onChange, onCancel, onSave, saving }: { mode: "create" | "edit"; form: PolicyForm; onChange: (value: PolicyForm) => void; onCancel: () => void; onSave: () => void; saving: boolean }) {
  const field = <K extends keyof PolicyForm>(key: K, value: PolicyForm[K]) => onChange({ ...form, [key]: value });
  const ruleField = <K extends keyof RuleForm>(index: number, key: K, value: RuleForm[K]) => {
    const rules = form.rules.map((rule, position) => position === index ? { ...rule, [key]: value } : rule);
    field("rules", rules);
  };
  return <section className="panel policy-editor-panel">
    <div className="panel-header"><div><span className="panel-eyebrow">{mode === "create" ? "New logical policy" : "Draft policy version"}</span><h3>{mode === "create" ? "Create governance policy" : `Edit ${form.name}`}</h3></div><button className="icon-button" onClick={onCancel}><X size={17} /></button></div>
    <div className="policy-editor-grid">
      <label>Name<input value={form.name} onChange={event => field("name", event.target.value)} /></label>
      <label>Owner<input value={form.owner} onChange={event => field("owner", event.target.value)} /></label>
      <label>Domain<select value={form.domain} disabled={mode === "edit"} onChange={event => field("domain", event.target.value as PolicyDomain)}>{domains.map(domain => <option key={domain}>{domain}</option>)}</select></label>
      <label>Default effect<select value={form.defaultEffect} onChange={event => field("defaultEffect", event.target.value as PolicyEffect)}>{effects.map(effect => <option key={effect}>{effect}</option>)}</select></label>
      <label>Scope<select value={form.scope} onChange={event => field("scope", event.target.value as PolicyScope)}>{scopes.map(scope => <option key={scope}>{scope}</option>)}</select></label>
      <label>Environment<input value={form.environment} onChange={event => field("environment", event.target.value)} disabled={form.scope !== "Environment"} /></label>
      {form.scope === "Tenant" && <label>Tenant ID<input value={form.tenantId} onChange={event => field("tenantId", event.target.value)} placeholder="GUID" /></label>}
      <label className="policy-description-field">Description<textarea rows={3} value={form.description} onChange={event => field("description", event.target.value)} /></label>
    </div>
    <div className="policy-rule-editor-header"><div><span className="panel-eyebrow">Ordered rules</span><h4>Highest matching priority wins</h4></div><button className="secondary-button" onClick={() => field("rules", [...form.rules, { name: `Rule ${form.rules.length + 1}`, effect: "Allow", priority: 10, matchText: "", constraintsText: "" }])}><Plus size={14} /> Add rule</button></div>
    <div className="policy-rule-editor-list">
      {form.rules.map((rule, index) => <article key={`${index}-${rule.name}`} className="policy-rule-editor-card">
        <div className="policy-rule-editor-fields">
          <label>Rule name<input value={rule.name} onChange={event => ruleField(index, "name", event.target.value)} /></label>
          <label>Effect<select value={rule.effect} onChange={event => ruleField(index, "effect", event.target.value as PolicyEffect)}>{effects.map(effect => <option key={effect}>{effect}</option>)}</select></label>
          <label>Priority<input type="number" value={rule.priority} onChange={event => ruleField(index, "priority", Number(event.target.value))} /></label>
          <label>Match attributes<textarea rows={3} value={rule.matchText} onChange={event => ruleField(index, "matchText", event.target.value)} placeholder="provider=Gemini" /></label>
          <label>Constraints<textarea rows={3} value={rule.constraintsText} onChange={event => ruleField(index, "constraintsText", event.target.value)} placeholder="maxOutputTokens=1000" /></label>
        </div>
        <button className="icon-button danger" onClick={() => field("rules", form.rules.filter((_, position) => position !== index))}><X size={15} /></button>
      </article>)}
    </div>
    <div className="policy-editor-actions"><button className="secondary-button" onClick={onCancel}>Cancel</button><button className="primary-button" onClick={onSave} disabled={saving || !form.name.trim() || !form.owner.trim()}>{saving ? <LoaderCircle className="spin" size={16} /> : <Save size={16} />} Save draft</button></div>
  </section>;
}

function PolicyDetailView({ detail, onEdit, onAction, onVersion, onSelectVersion, pending }: { detail: PolicyDetail; onEdit: () => void; onAction: (action: string) => void; onVersion: () => void; onSelectVersion: (id: string) => void; pending: boolean }) {
  const policy = detail.summary;
  return <div className="policy-detail-content">
    <div className="policy-detail-heading">
      <div><span className="panel-eyebrow">{friendly(policy.domain)} · {policy.scope}</span><h2>{policy.name} <small>v{policy.version}</small></h2><p>{policy.description || "No description supplied."}</p></div>
      <PolicyPill effect={policy.defaultEffect} status={policy.status} />
    </div>
    <div className="policy-detail-actions">
      {policy.status === "Draft" && <><button className="secondary-button" onClick={onEdit}><Pencil size={15} /> Edit draft</button><button className="primary-button" onClick={() => onAction("submit")} disabled={pending}><FileCheck2 size={15} /> Submit</button><button className="secondary-button" onClick={() => onAction("activate")} disabled={pending}><BadgeCheck size={15} /> Activate</button></>}
      {policy.status === "PendingApproval" && <button className="primary-button" onClick={() => onAction("activate")} disabled={pending}><BadgeCheck size={15} /> Activate</button>}
      {policy.status === "Active" && <><button className="secondary-button" onClick={() => onAction("suspend")} disabled={pending}><Clock3 size={15} /> Suspend</button><button className="secondary-button" onClick={onVersion} disabled={pending}><CopyPlus size={15} /> New version</button></>}
      {policy.status === "Suspended" && <><button className="primary-button" onClick={() => onAction("activate")} disabled={pending}><BadgeCheck size={15} /> Reactivate</button><button className="secondary-button" onClick={onVersion} disabled={pending}><CopyPlus size={15} /> New version</button></>}
      {policy.status !== "Retired" && <button className="danger-button" onClick={() => onAction("retire")} disabled={pending}><Ban size={15} /> Retire</button>}
    </div>
    <dl className="policy-fact-grid"><div><dt>Owner</dt><dd>{policy.owner}</dd></div><div><dt>Environment</dt><dd>{policy.environment}</dd></div><div><dt>Default</dt><dd>{friendly(policy.defaultEffect)}</dd></div><div><dt>Revision</dt><dd>{policy.revision}</dd></div><div><dt>Updated</dt><dd>{formatDate(policy.updatedAt)}</dd></div><div><dt>Activated</dt><dd>{policy.activatedAt ? formatDate(policy.activatedAt) : "Not active"}</dd></div></dl>
    <section className="policy-detail-section"><div className="section-heading"><div><span className="panel-eyebrow">Decision rules</span><h3>{detail.rules.length} ordered rule{detail.rules.length === 1 ? "" : "s"}</h3></div><KeyRound size={17} /></div><div className="policy-rule-list">{detail.rules.map(rule => <article key={rule.name} className="policy-rule-card"><div><strong>{rule.name}</strong><PolicyEffectBadge effect={rule.effect} /></div><span>Priority {rule.priority}</span><DictionaryBlock label="Matches" values={rule.match} empty="Matches every context" /><DictionaryBlock label="Constraints" values={rule.constraints} empty="No constraints" /></article>)}{detail.rules.length === 0 && <div className="empty-state compact-empty"><LockKeyhole size={24} /><h3>No rules</h3><p>The default effect applies to every matching context.</p></div>}</div></section>
    <section className="policy-version-section"><div className="section-heading"><div><span className="panel-eyebrow">Immutable history</span><h3>Versions</h3></div><History size={17} /></div><div className="policy-version-list">{detail.versionHistory.map(version => <button key={version.id} className={version.id === policy.id ? "selected" : ""} onClick={() => onSelectVersion(version.id)}><strong>v{version.version}</strong><span>{version.status}</span><small>{formatDate(version.updatedAt)}</small></button>)}</div></section>
  </div>;
}

function EvaluationResult({ result }: { result: PolicyEvaluationResult }) {
  return <div className={`policy-evaluation-result effect-${result.effect.toLowerCase()}`}>
    <div>{result.isAllowed ? <CheckCircle2 size={22} /> : <ShieldX size={22} />}<div><span>Final decision</span><strong>{friendly(result.effect)}</strong></div></div>
    <p>{result.reason}</p>
    <DictionaryBlock label="Effective constraints" values={result.constraints} empty="No constraints applied" />
    <small>Correlation {result.correlationId} · {result.decisions.length} decision record{result.decisions.length === 1 ? "" : "s"}</small>
  </div>;
}

function DictionaryBlock({ label, values, empty }: { label: string; values: Record<string, string>; empty: string }) {
  const entries = Object.entries(values);
  return <div className="policy-dictionary"><span>{label}</span>{entries.length === 0 ? <em>{empty}</em> : <div>{entries.map(([key, value]) => <code key={key}>{key}={value}</code>)}</div>}</div>;
}

function PolicyPill({ effect, status }: { effect: PolicyEffect; status: PolicyStatus }) {
  return <span className={`policy-status-pill status-${status.toLowerCase()} effect-${effect.toLowerCase()}`}>{status}</span>;
}

function PolicyEffectBadge({ effect }: { effect: PolicyEffect }) {
  return <span className={`policy-effect-badge effect-${effect.toLowerCase()}`}>{friendly(effect)}</span>;
}

function toForm(detail: PolicyDetail): PolicyForm {
  return {
    name: detail.summary.name,
    description: detail.summary.description,
    owner: detail.summary.owner,
    domain: detail.summary.domain,
    defaultEffect: detail.summary.defaultEffect,
    scope: detail.summary.scope,
    environment: detail.summary.environment,
    tenantId: detail.summary.tenantId ?? "",
    rules: detail.rules.map(rule => ({
      name: rule.name,
      effect: rule.effect,
      priority: rule.priority,
      matchText: stringifyPairs(rule.match),
      constraintsText: stringifyPairs(rule.constraints),
    })),
  };
}

function toInput(form: PolicyForm): CreatePolicyInput {
  return {
    name: form.name.trim(),
    description: form.description.trim(),
    owner: form.owner.trim(),
    domain: form.domain,
    defaultEffect: form.defaultEffect,
    scope: form.scope,
    environment: form.scope === "Environment" ? form.environment.trim() || "Development" : "All",
    tenantId: form.scope === "Tenant" ? form.tenantId.trim() || null : null,
    rules: form.rules.map(rule => ({ name: rule.name.trim(), effect: rule.effect, priority: rule.priority, match: parsePairs(rule.matchText), constraints: parsePairs(rule.constraintsText) })) as PolicyRuleInput[],
  };
}

function parsePairs(value: string): Record<string, string> {
  return value.split(/\r?\n|,/).map(line => line.trim()).filter(Boolean).reduce<Record<string, string>>((result, line) => {
    const separator = line.indexOf("=");
    if (separator > 0) result[line.slice(0, separator).trim()] = line.slice(separator + 1).trim();
    return result;
  }, {});
}

function stringifyPairs(values: Record<string, string>) {
  return Object.entries(values).map(([key, value]) => `${key}=${value}`).join("\n");
}

function friendly(value: string) {
  return value.replace(/([a-z])([A-Z])/g, "$1 $2");
}

function formatNumber(value?: number) {
  return new Intl.NumberFormat("en-ZA").format(value ?? 0);
}

function formatPercent(value?: number) {
  return `${Math.round((value ?? 0) * 100)}%`;
}

function formatDate(value: string) {
  return new Date(value).toLocaleString("en-ZA", { dateStyle: "medium", timeStyle: "short" });
}
