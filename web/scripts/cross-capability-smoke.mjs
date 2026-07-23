import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { writeFileSync } from "node:fs";
import { authenticateAcceptanceClient, authenticatedFetch } from "./acceptance-auth.mjs";

async function call(path, options = {}) {
  const response = await authenticatedFetch(path, options);
  const body = response.status === 204 ? undefined : await response.json().catch(() => undefined);
  if (!response.ok) throw new Error(`${options.method ?? "GET"} ${path} failed (${response.status}): ${JSON.stringify(body)}`);
  return body;
}
const json = (method, body) => ({ method, headers: { "content-type": "application/json" }, body: JSON.stringify(body) });
const lifecycle = async (path, revision) => call(path, json("POST", { actor: "Stabilization smoke", reason: "Cross-capability acceptance", expectedRevision: revision }));

await authenticateAcceptanceClient();

const suffix = Date.now().toString(36);

// Acceptance runs share the persisted development database. Retire only deny
// policies created by an earlier run so the baseline execution can reach the
// deterministic provider before this run proves denial itself.
const existingPolicies = await call("/api/policies");
for (const existingPolicy of existingPolicies.filter(item =>
  item.name.startsWith("Deny deterministic ") && ["Active", "Suspended"].includes(item.status))) {
  await call(`/api/policies/${existingPolicy.id}/retire`, { method: "POST" });
}

const collection = await call("/api/knowledge/collections", json("POST", {
  name: `Smoke knowledge ${suffix}`, description: "Deterministic stabilization evidence", owner: "Acceptance", classification: "Internal",
}));
const form = new FormData();
form.append("file", new Blob(["Hail damage is covered by comprehensive policies, subject to excess and exclusions."], { type: "text/plain" }), `smoke-${suffix}.txt`);
form.append("owner", "Acceptance");
form.append("classification", "Internal");
let document = await call(`/api/knowledge/collections/${collection.id}/documents`, { method: "POST", body: form });
document = await call(`/api/knowledge/documents/${document.id}/process`, { method: "POST" });
for (const action of ["submit", "approve", "publish"]) document = await lifecycle(`/api/knowledge/documents/${document.id}/${action}`, document.revision);

const prompt = await call("/api/prompts", json("POST", {
  name: `Smoke prompt ${suffix}`, description: "Grounded acceptance prompt", owner: "Acceptance", category: "Claims", tags: ["smoke"],
}));
let promptVersion = await call(`/api/prompts/${prompt.id}/versions`, json("POST", {
  version: "1.0.0", changeSummary: "Acceptance baseline", expectedPromptRevision: prompt.revision,
  sections: [
    { kind: "System", name: "System", content: "Answer only from governed knowledge.", sequence: 1, required: true },
    { kind: "Knowledge", name: "Knowledge", content: "{{knowledgePackage}}", sequence: 2, required: true },
    { kind: "User", name: "Customer", content: "{{customerMessage}}", sequence: 3, required: true },
  ],
}));
for (const action of ["submit", "approve", "publish"]) promptVersion = await lifecycle(`/api/prompts/versions/${promptVersion.id}/${action}`, promptVersion.revision);

const workflow = await call("/api/workflows", json("POST", {
  name: `Smoke workflow ${suffix}`, description: "Cross-capability acceptance path", owner: "Acceptance", tags: ["smoke"],
}));
const startId = randomUUID(); const knowledgeId = randomUUID(); const promptId = randomUUID(); const intelligenceId = randomUUID(); const responseId = randomUUID(); const endId = randomUUID();
let workflowVersion = await call(`/api/workflows/${workflow.id}/versions`, json("POST", {
  version: "1.0.0", changeSummary: "Acceptance baseline", expectedWorkflowRevision: workflow.revision,
  nodes: [
    { id: startId, name: "Start", kind: "Start", positionX: 40, positionY: 80, configuration: {} },
    { id: knowledgeId, name: "Knowledge", kind: "Knowledge", positionX: 260, positionY: 80, configuration: { reference: collection.name } },
    { id: promptId, name: "Prompt", kind: "Prompt", positionX: 480, positionY: 80, configuration: { reference: `${prompt.name} v1.0.0` } },
    { id: intelligenceId, name: "Intelligence", kind: "Intelligence", positionX: 700, positionY: 80, configuration: {} },
    { id: responseId, name: "Response", kind: "Response", positionX: 920, positionY: 80, configuration: {} },
    { id: endId, name: "End", kind: "End", positionX: 1140, positionY: 80, configuration: {} },
  ],
  transitions: [[startId, knowledgeId], [knowledgeId, promptId], [promptId, intelligenceId], [intelligenceId, responseId], [responseId, endId]].map(([fromNodeId, toNodeId]) => ({ id: randomUUID(), fromNodeId, toNodeId, label: "Next", condition: null })),
}));
for (const action of ["submit", "approve", "publish"]) workflowVersion = await lifecycle(`/api/workflows/versions/${workflowVersion.id}/${action}`, workflowVersion.revision);

let simulation = await call("/api/simulations", json("POST", {
  title: `Stabilization ${suffix}`, workflow: `${workflow.name} v1.0.0`, promptVersion: `${prompt.name} v1.0.0`, knowledgeCollection: collection.name,
}));
simulation = await call(`/api/simulations/${simulation.id}/messages`, json("POST", {
  content: "Can I claim for hail damage?", mode: "Normal", provider: "Deterministic", temperature: 0.2, maxOutputTokens: 400,
}));
const run = simulation.runs.at(-1);
assert.ok(run?.id, "Simulation did not persist a run.");
assert.equal(run.metrics?.currency ?? run.executionPlan?.currency, "ZAR");

const evaluations = await call("/api/evaluations/overview");
assert.ok(evaluations.recentRuns.some(item => item.sourceRunId === run.id), "Evaluation correlation was not persisted.");
const traces = await call(`/api/traces?query=${run.id}`);
assert.ok(traces.some(item => item.sourceRunId === run.id), "Trace correlation was not persisted.");

let experiment = await call("/api/replay/experiments", json("POST", {
  name: `Replay ${suffix}`, simulationId: simulation.id, sourceRunId: run.id, candidateLabel: "Candidate A", provider: "Deterministic", temperature: 0.1, maxOutputTokens: 350, mode: "Normal",
}));
assert.ok(experiment.candidates.length, "Replay candidate was not persisted.");
experiment = await call(`/api/replay/experiments/${experiment.summary.id}/complete`, { method: "POST" });
assert.equal(experiment.summary.status, "Completed");

let policy = await call("/api/policies", json("POST", {
  name: `Deny deterministic ${suffix}`, description: "Prove denial occurs before provider invocation", owner: "Acceptance", domain: "ProviderAccess", defaultEffect: "Allow", scope: "Global", environment: "All", tenantId: null,
  rules: [{ name: "Deny deterministic", effect: "Deny", priority: 1, match: { provider: "Deterministic" }, constraints: {} }],
}));
policy = await call(`/api/policies/${policy.summary.id}/submit`, { method: "POST" });
policy = await call(`/api/policies/${policy.summary.id}/activate`, { method: "POST" });
const executionsBeforeDenial = await call("/api/intelligence/executions?limit=500");
const denied = await call(`/api/simulations/${simulation.id}/messages`, json("POST", { content: "This must be denied", provider: "Deterministic", mode: "Normal", temperature: 0.2, maxOutputTokens: 400 }));
const deniedRun = denied.runs.at(-1);
assert.equal(deniedRun?.status, "Failed", "Active deny policy did not fail the governed run.");
assert.match(deniedRun?.failureReason ?? "", /deny/i, "Denied run did not retain the policy reason.");
const executionsAfterDenial = await call("/api/intelligence/executions?limit=500");
assert.equal(executionsAfterDenial.length, executionsBeforeDenial.length, "Provider execution was recorded despite policy denial.");

const pluginOverview = await call("/api/plugins/overview");
const plugin = pluginOverview.plugins.find(item => item.key === "deterministic-provider");
assert.ok(plugin, "Built-in deterministic plugin was not seeded.");
const health = await call(`/api/plugins/${plugin.id}/health`, { method: "POST" });
assert.equal(health.status, "Healthy");

const evidence = { simulationId: simulation.id, runId: run.id, traceId: traces[0].id, experimentId: experiment.summary.id, policyId: policy.summary.id, pluginId: plugin.id };
if (process.env.CONVOLAB_EVIDENCE_PATH) writeFileSync(process.env.CONVOLAB_EVIDENCE_PATH, JSON.stringify(evidence));
console.log(JSON.stringify(evidence, null, 2));
