import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { authenticateAcceptanceClient, authenticatedFetch } from "./acceptance-auth.mjs";

const evidencePath = process.env.CONVOLAB_EVIDENCE_PATH;
assert.ok(evidencePath, "CONVOLAB_EVIDENCE_PATH is required.");
const evidence = JSON.parse(readFileSync(evidencePath, "utf8"));
await authenticateAcceptanceClient();
for (const [kind, route] of [
  ["simulation", `/api/simulations/${evidence.simulationId}`],
  ["replay", `/api/replay/experiments/${evidence.experimentId}`],
  ["policy", `/api/policies/${evidence.policyId}`],
  ["plugin", `/api/plugins/${evidence.pluginId}`],
]) {
  const response = await authenticatedFetch(route);
  assert.equal(response.status, 200, `${kind} evidence did not survive restart (${response.status}).`);
}
console.log("Cross-capability identifiers survived the PostgreSQL/API restart.");
