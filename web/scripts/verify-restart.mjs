import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const evidencePath = process.env.CONVOLAB_EVIDENCE_PATH;
assert.ok(evidencePath, "CONVOLAB_EVIDENCE_PATH is required.");
const evidence = JSON.parse(readFileSync(evidencePath, "utf8"));
const baseUrl = process.env.CONVOLAB_API_BASE_URL ?? "http://127.0.0.1:5000";
for (const [kind, route] of [
  ["simulation", `/api/simulations/${evidence.simulationId}`],
  ["replay", `/api/replay/experiments/${evidence.experimentId}`],
  ["policy", `/api/policies/${evidence.policyId}`],
  ["plugin", `/api/plugins/${evidence.pluginId}`],
]) {
  const response = await fetch(`${baseUrl}${route}`);
  assert.equal(response.status, 200, `${kind} evidence did not survive restart (${response.status}).`);
}
console.log("Cross-capability identifiers survived the PostgreSQL/API restart.");
