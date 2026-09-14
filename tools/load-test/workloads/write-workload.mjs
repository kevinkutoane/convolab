/**
 * Write Workload for ConvoLab Load Runner
 * Tests prompt mutations, workflow creation, and state updates.
 */

import crypto from 'node:crypto';

export const name = 'Write Workload';
export const provisionalP95TargetMs = 300; // Provisional engineering target

export async function execute(targetUrl, context = {}) {
  const name = `LoadTest_Prompt_${Date.now()}_${crypto.randomBytes(4).toString('hex')}`;
  const payload = {
    name,
    description: 'Automated performance test prompt mutation',
    owner: 'LoadRunner',
    category: 'Benchmark',
    tags: []
  };

  const headers = { 'Content-Type': 'application/json' };
  if (context.sessionCookie) {
    headers['Cookie'] = context.sessionCookie;
  }
  if (context.xsrfToken) {
    headers['X-XSRF-TOKEN'] = context.xsrfToken;
  }
  if (context.authToken) {
    headers['Authorization'] = `Bearer ${context.authToken}`;
  }

  const start = performance.now();
  try {
    const res = await fetch(`${targetUrl}/api/prompts`, {
      method: 'POST',
      headers,
      body: JSON.stringify(payload)
    });
    const latency = performance.now() - start;
    const isAuth = Boolean(context.isAuthenticated || context.sessionCookie || context.authToken);
    const success = isAuth
      ? (res.status === 200 || res.status === 201)
      : (res.status === 401 || res.status === 200 || res.status === 201);

    return {
      name: 'Create Prompt Template',
      status: res.status,
      latencyMs: latency,
      success
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
