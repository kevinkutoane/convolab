import { api } from "./apiClient";
import type {
  CreatePolicyInput,
  EvaluatePolicyInput,
  PolicyDetail,
  PolicyEvaluationResult,
  PolicyOverview,
  PolicySummary,
  UpdatePolicyInput,
} from "../types/policy";

export const getPolicyOverview = async () =>
  (await api.get<PolicyOverview>("/api/policies/overview")).data;

export const listPolicies = async () =>
  (await api.get<PolicySummary[]>("/api/policies")).data;

export const getPolicy = async (policyId: string) =>
  (await api.get<PolicyDetail>(`/api/policies/${policyId}`)).data;

export const createPolicy = async (input: CreatePolicyInput) =>
  (await api.post<PolicyDetail>("/api/policies", input)).data;

export const updatePolicy = async (policyId: string, input: UpdatePolicyInput) =>
  (await api.put<PolicyDetail>(`/api/policies/${policyId}`, input)).data;

export const createPolicyVersion = async (policyId: string, owner: string) =>
  (await api.post<PolicyDetail>(`/api/policies/${policyId}/versions`, { owner })).data;

export const transitionPolicy = async (policyId: string, action: string) =>
  (await api.post<PolicyDetail>(`/api/policies/${policyId}/${action}`)).data;

export const evaluatePolicy = async (input: EvaluatePolicyInput) =>
  (await api.post<PolicyEvaluationResult>("/api/policies/evaluate", input)).data;
