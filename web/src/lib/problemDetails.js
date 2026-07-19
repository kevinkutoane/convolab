/**
 * @typedef {Object} PlatformProblem
 * @property {string} code
 * @property {string} title
 * @property {string} detail
 * @property {string | undefined} correlationId
 * @property {Record<string, string[]>} errors
 */

/**
 * Normalizes RFC 7807 responses without exposing transport-specific details to pages.
 * @param {unknown} payload
 * @param {string} fallback
 * @returns {PlatformProblem}
 */
export function normalizeProblemDetails(payload, fallback = "The request could not be completed.") {
  const value = payload && typeof payload === "object" ? payload : {};
  const extensions = /** @type {Record<string, unknown>} */ (value);
  return {
    code: typeof extensions.code === "string" ? extensions.code : "platform.request_failed",
    title: typeof extensions.title === "string" ? extensions.title : "Request failed",
    detail: typeof extensions.detail === "string" ? extensions.detail : fallback,
    correlationId: typeof extensions.correlationId === "string" ? extensions.correlationId : undefined,
    errors:
      extensions.errors && typeof extensions.errors === "object"
        ? /** @type {Record<string, string[]>} */ (extensions.errors)
        : {},
  };
}

/** @param {unknown} error */
export function isConcurrencyProblem(error) {
  return Boolean(
    error &&
      typeof error === "object" &&
      "code" in error &&
      /** @type {{code?: unknown}} */ (error).code === "concurrency.conflict",
  );
}
