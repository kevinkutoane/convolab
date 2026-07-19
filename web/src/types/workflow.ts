export type WorkflowStatus = "Draft" | "PendingApproval" | "Approved" | "Published" | "Deprecated" | "Archived";
export type WorkflowNodeKind = "Start" | "Prompt" | "Knowledge" | "Decision" | "Intelligence" | "Response" | "End";

export interface WorkflowNode {
  id: string;
  name: string;
  kind: WorkflowNodeKind;
  positionX: number;
  positionY: number;
  configuration: Record<string, string>;
}

export interface WorkflowTransition {
  id: string;
  fromNodeId: string;
  toNodeId: string;
  label: string;
  condition?: string | null;
}

export interface WorkflowValidationIssue {
  code: string;
  message: string;
  nodeId?: string | null;
}

export interface WorkflowVersion {
  id: string;
  workflowId: string;
  version: string;
  status: WorkflowStatus;
  changeSummary: string;
  nodes: WorkflowNode[];
  transitions: WorkflowTransition[];
  validationIssues: WorkflowValidationIssue[];
  isValid: boolean;
  approvedBy?: string | null;
  approvedAt?: string | null;
  publishedAt?: string | null;
  createdAt: string;
  updatedAt: string;
  revision: number;
}

export interface WorkflowSummary {
  id: string;
  name: string;
  description: string;
  owner: string;
  tags: string[];
  isActive: boolean;
  status: WorkflowStatus;
  latestVersion: string;
  versionCount: number;
  updatedAt: string;
  revision: number;
}

export interface WorkflowAudit {
  id: string;
  workflowVersionId: string;
  actor: string;
  action: string;
  reason?: string | null;
  previousStatus: WorkflowStatus;
  newStatus: WorkflowStatus;
  createdAt: string;
}

export interface WorkflowDetail {
  id: string;
  name: string;
  description: string;
  owner: string;
  tags: string[];
  isActive: boolean;
  versions: WorkflowVersion[];
  audit: WorkflowAudit[];
  createdAt: string;
  updatedAt: string;
  revision: number;
}

export interface WorkflowNodeInput {
  id?: string;
  name: string;
  kind: WorkflowNodeKind;
  positionX: number;
  positionY: number;
  configuration: Record<string, string>;
}

export interface WorkflowTransitionInput {
  id?: string;
  fromNodeId: string;
  toNodeId: string;
  label?: string;
  condition?: string | null;
}
