export interface PlatformProblem {
  code: string;
  title: string;
  detail: string;
  correlationId?: string;
  errors: Record<string, string[]>;
}
export function normalizeProblemDetails(payload: unknown, fallback?: string): PlatformProblem;
export function isConcurrencyProblem(error: unknown): boolean;
