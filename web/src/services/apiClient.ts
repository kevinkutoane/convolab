import axios from "axios";
import { normalizeProblemDetails, type PlatformProblem } from "../lib/problemDetails.js";

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
  withXSRFToken: true,
  xsrfCookieName: "XSRF-TOKEN",
  xsrfHeaderName: "X-XSRF-TOKEN",
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
