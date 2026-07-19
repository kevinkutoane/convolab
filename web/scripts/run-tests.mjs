import assert from "node:assert/strict";
import { isConcurrencyProblem, normalizeProblemDetails } from "../src/lib/problemDetails.js";

const normalized = normalizeProblemDetails({
  title: "Concurrency conflict",
  detail: "Refresh and retry.",
  code: "concurrency.conflict",
  correlationId: "abc123",
  errors: { revision: ["Stale revision"] },
});
assert.equal(normalized.code, "concurrency.conflict");
assert.equal(normalized.correlationId, "abc123");
assert.equal(normalized.errors.revision[0], "Stale revision");
assert.equal(isConcurrencyProblem(normalized), true);

const fallback = normalizeProblemDetails(null, "Offline");
assert.equal(fallback.detail, "Offline");
assert.equal(fallback.code, "platform.request_failed");
console.log("Frontend contract tests passed.");
