import { api } from "./apiClient";
import type {
  CreateSimulationRequest,
  ReplaySimulationRequest,
  SendSimulationMessageRequest,
  SimulationConversation,
  SimulationOptions,
  SimulationSummary,
} from "../types/simulation";

export async function getSimulationOptions(): Promise<SimulationOptions> {
  const response = await api.get<SimulationOptions>("/api/simulations/options");
  return response.data;
}

export async function listSimulations(): Promise<SimulationSummary[]> {
  const response = await api.get<SimulationSummary[]>("/api/simulations");
  return response.data;
}

export async function getSimulation(simulationId: string): Promise<SimulationConversation> {
  const response = await api.get<SimulationConversation>(`/api/simulations/${simulationId}`);
  return response.data;
}

export async function createSimulation(request: CreateSimulationRequest): Promise<SimulationConversation> {
  const response = await api.post<SimulationConversation>("/api/simulations", request);
  return response.data;
}

export async function sendSimulationMessage(
  simulationId: string,
  request: SendSimulationMessageRequest,
): Promise<SimulationConversation> {
  const response = await api.post<SimulationConversation>(
    `/api/simulations/${simulationId}/messages`,
    request,
  );
  return response.data;
}

export async function replaySimulation(
  simulationId: string,
  request: ReplaySimulationRequest,
): Promise<SimulationConversation> {
  const response = await api.post<SimulationConversation>(
    `/api/simulations/${simulationId}/replay`,
    request,
  );
  return response.data;
}
