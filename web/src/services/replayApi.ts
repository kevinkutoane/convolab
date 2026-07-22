import { api } from "./apiClient";
import type {
  CreateReplayExperimentInput,
  ReplayCandidateInput,
  ReplayExperimentDetail,
  ReplayExperimentSummary,
  ReplayOverview,
  ReplaySource,
} from "../types/replay";

export const getReplayOverview = async () =>
  (await api.get<ReplayOverview>("/api/replay/overview")).data;

export const listReplaySources = async () =>
  (await api.get<ReplaySource[]>("/api/replay/sources")).data;

export const listReplayExperiments = async () =>
  (await api.get<ReplayExperimentSummary[]>("/api/replay/experiments")).data;

export const getReplayExperiment = async (experimentId: string) =>
  (await api.get<ReplayExperimentDetail>(`/api/replay/experiments/${experimentId}`)).data;

export const createReplayExperiment = async (input: CreateReplayExperimentInput) => {
  const { candidateLabel, ...rest } = input;
  return (await api.post<ReplayExperimentDetail>("/api/replay/experiments", {
    ...rest,
    candidateLabel,
  })).data;
};

export const addReplayCandidate = async (experimentId: string, input: ReplayCandidateInput) =>
  (await api.post<ReplayExperimentDetail>(`/api/replay/experiments/${experimentId}/candidates`, input)).data;

export const completeReplayExperiment = async (experimentId: string) =>
  (await api.post<ReplayExperimentDetail>(`/api/replay/experiments/${experimentId}/complete`)).data;

export const archiveReplayExperiment = async (experimentId: string) =>
  (await api.post<ReplayExperimentDetail>(`/api/replay/experiments/${experimentId}/archive`)).data;
