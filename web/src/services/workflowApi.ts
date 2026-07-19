import { api } from "./apiClient";
import type {
  WorkflowDetail,
  WorkflowNodeInput,
  WorkflowSummary,
  WorkflowTransitionInput,
  WorkflowVersion,
} from "../types/workflow";

export const listWorkflows = async () => (await api.get<WorkflowSummary[]>("/api/workflows")).data;
export const getWorkflow = async (id: string) => (await api.get<WorkflowDetail>(`/api/workflows/${id}`)).data;
export const createWorkflow = async (input: { name: string; description: string; owner: string; tags: string[] }) =>
  (await api.post<WorkflowDetail>("/api/workflows", input)).data;
export const createWorkflowVersion = async (
  id: string,
  input: {
    version: string;
    changeSummary: string;
    nodes: WorkflowNodeInput[];
    transitions: WorkflowTransitionInput[];
    expectedWorkflowRevision: number;
  },
) => (await api.post<WorkflowVersion>(`/api/workflows/${id}/versions`, input)).data;
export const updateWorkflowGraph = async (
  versionId: string,
  input: {
    nodes: WorkflowNodeInput[];
    transitions: WorkflowTransitionInput[];
    changeSummary: string;
    expectedRevision: number;
  },
) => (await api.put<WorkflowVersion>(`/api/workflows/versions/${versionId}/graph`, input)).data;
export const validateWorkflow = async (versionId: string) =>
  (await api.get<WorkflowVersion>(`/api/workflows/versions/${versionId}/validate`)).data;
export const transitionWorkflowVersion = async (versionId: string, action: string, expectedRevision: number) =>
  (await api.post<WorkflowVersion>(`/api/workflows/versions/${versionId}/${action}`, {
    actor: "Studio user",
    reason: `${action} from ConvoLab Studio`,
    expectedRevision,
  })).data;
