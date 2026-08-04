import { api } from "./apiClient";
import type { PlatformStatus } from "../types/platform";

export async function getPlatformStatus(): Promise<PlatformStatus> {
  const response = await api.get<PlatformStatus>("/api/platform/status");
  return { ...response.data, source: "api" };
}
