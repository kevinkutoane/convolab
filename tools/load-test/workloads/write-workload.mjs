/**
 * Write Workload for ConvoLab Load Runner
 * Tests prompt mutations, workflow creation, and state updates.
 */

import crypto from 'node:crypto';

export const name = 'Write Workload';
export const provisionalP95TargetMs = 300; // Provisional engineering target

export async function execute(targetUrl, context = {}) {
  const id = crypto.randomUUID();
  const name = `LoadTest_Prompt_${Date.now()}_${crypto.randomBytes(4).toString('hex')}`;
  const payload = {
    id,
    name,
    description: 'Automated performance test prompt mutation',
    template: 'Respond concisely to user query: {{query}}',
    model: 'gemini-2.5-flash',
    category: 'Benchmark'
  };

  const headers = { 'Content-Type': 'application/json' };
  if (context.sessionCookie) {
    headers['Cookie'] = context.sessionCookie;
  }
  if (context.authToken) {
    headers['Authorization'] = `Bearer ${context.authToken}`;
  }

  const start = performance.now();
  try {
    const res = await fetch(`${targetUrl}/api/prompt-studio/prompts`, {
      method: 'POST',
      headers,
      body: JSON.stringify(payload)
    });
    const latency = performance.now() - start;
    return {
      name: 'Create Prompt Template',
      status: res.status,
      latencyMs: latency,
      success: res.status === 200 || res.status === 201
    };
  } catch (err) {
    const latency = performance.now() - start;
    return {
      name: 'Create Prompt Template',
      status: 0,
      latencyMs: latency,
      success: false,
      error: err.message
    };
  }
}
