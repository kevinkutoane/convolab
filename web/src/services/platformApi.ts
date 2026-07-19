import { api } from "./apiClient";
import { designTimePlatformStatus } from "../data/platform";
import type { PlatformStatus } from "../types/platform";

export async function getPlatformStatus(): Promise<PlatformStatus> {
  try {
    const response = await api.get<PlatformStatus>("/api/platform/status");
    return { ...response.data, source: "api" };
  } catch {
    return {
      ...designTimePlatformStatus,
      apiHealth: "Offline",
      generatedAt: new Date().toISOString(),
      source: "design-time snapshot",
    };
  }
}
