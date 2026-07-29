import axios from "axios";
import { normalizeProblemDetails, type PlatformProblem } from "../lib/problemDetails.js";
import { prepareAntiforgery } from "./authApi";
import { getRuntimeEnvironmentId } from "./runtimeEnvironment";

export class PlatformApiError extends Error {
  readonly problem: PlatformProblem;
  readonly status?: number;

  constructor(problem: PlatformProblem, status?: number) {
    super(problem.detail);
    this.name = "PlatformApiError";
    this.problem = problem;
    this.status = status;
  }
}

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? "",
  timeout: 30_000,
  withCredentials: true,
  xsrfCookieName: "",  // Disable axios auto-XSRF; our interceptor sends the correct request token
});

function isEnvironmentAwareRequest(url?: string, method?: string) {
  if (!url || ["GET", "HEAD", "OPTIONS", "TRACE"].includes((method ?? "GET").toUpperCase())) return false;
  return url.startsWith("/api/simulations")
    || url.startsWith("/api/evaluation")
    || url.startsWith("/api/replay")
    || url.startsWith("/api/plugins");
}

api.interceptors.request.use((config) => {
  const method = (config.method ?? "get").toUpperCase();
  const unsafeMethod = !["GET", "HEAD", "OPTIONS", "TRACE"].includes(method);
  const runtimeEnvironmentId = getRuntimeEnvironmentId();
  if (runtimeEnvironmentId && isEnvironmentAwareRequest(config.url, method)) {
    config.headers.set("X-ConvoLab-Environment-Id", runtimeEnvironmentId);
  }
  if (!unsafeMethod) return config;
  return prepareAntiforgery(true).then((token) => {
    if (token) config.headers.set("X-XSRF-TOKEN", token);
    return config;
  });
});

api.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    if (axios.isAxiosError(error)) {
      const problem = normalizeProblemDetails(error.response?.data, error.message);
      return Promise.reject(new PlatformApiError(problem, error.response?.status));
    }
    return Promise.reject(error);
  },
);

export function getApiErrorMessage(error: unknown): string {
  if (error instanceof PlatformApiError) {
    const suffix = error.problem.correlationId
      ? ` Correlation: ${error.problem.correlationId}.`
      : "";
    return `${error.problem.detail}${suffix}`;
  }
  return error instanceof Error ? error.message : "The request could not be completed.";
}
