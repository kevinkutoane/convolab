export type PolicyEffect = "Allow" | "AllowWithConstraints" | "Deny";
export type PolicyDomain =
  | "ModelAccess"
  | "ProviderAccess"
  | "BudgetLimit"
  | "RateLimit"
  | "PromptApproval"
  | "KnowledgeAccess"
  | "TenantRule"
  | "Compliance"
  | "EvaluationThreshold"
  | "Safety";
export type PolicyStatus = "Draft" | "PendingApproval" | "Active" | "Suspended" | "Retired";
export type PolicyScope = "Global" | "Environment" | "Tenant";

export interface PolicyRuleInput {
  name: string;
  effect: PolicyEffect;
  priority: number;
  match: Record<string, string>;
  constraints: Record<string, string>;
}

export interface PolicySummary {
  id: string;
  policyKey: string;
  version: number;
  name: string;
  description: string;
  owner: string;
  domain: PolicyDomain;
  status: PolicyStatus;
  scope: PolicyScope;
  environment: string;
  tenantId?: string | null;
  defaultEffect: PolicyEffect;
  ruleCount: number;
  revision: number;
  createdAt: string;
  updatedAt: string;
  activatedAt?: string | null;
}

export interface PolicyDecision {
  id: string;
  policyId?: string | null;
  policyKey?: string | null;
  policyVersion?: number | null;
  policyName: string;
  domain: PolicyDomain;
  effect: PolicyEffect;
  reason: string;
  context: Record<string, string>;
  constraints: Record<string, string>;
  source: string;
  correlationId: string;
  simulationId?: string | null;
  runId?: string | null;
  createdAt: string;
}

export interface PolicyDetail {
  summary: PolicySummary;
  rules: PolicyRuleInput[];
  recentDecisions: PolicyDecision[];
  versionHistory: PolicySummary[];
}

export interface PolicyCoverage {
  domain: PolicyDomain;
  activePolicies: number;
  decisions: number;
  denials: number;
  denyRate: number;
  status: string;
}

export interface PolicyMetrics {
  logicalPolicies: number;
  policyVersions: number;
  activePolicies: number;
  draftPolicies: number;
  decisions: number;
  denials: number;
  constrainedDecisions: number;
  denyRate: number;
}

export interface PolicyOverview {
  metrics: PolicyMetrics;
  policies: PolicySummary[];
  recentDecisions: PolicyDecision[];
  coverage: PolicyCoverage[];
  environments: string[];
  generatedAt: string;
}

export interface CreatePolicyInput {
  name: string;
  description: string;
  owner: string;
  domain: PolicyDomain;
  defaultEffect: PolicyEffect;
  scope: PolicyScope;
  environment: string;
  tenantId?: string | null;
  rules: PolicyRuleInput[];
}

export interface UpdatePolicyInput extends Omit<CreatePolicyInput, "domain"> {
  revision: number;
}

export interface EvaluatePolicyInput {
  domain: PolicyDomain;
  tenantId?: string | null;
  attributes: Record<string, string>;
  source: string;
  correlationId?: string | null;
}

export interface PolicyEvaluationResult {
  effect: PolicyEffect;
  isAllowed: boolean;
  reason: string;
  constraints: Record<string, string>;
  decisions: PolicyDecision[];
  correlationId: string;
  evaluatedAt: string;
}
