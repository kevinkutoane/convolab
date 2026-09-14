/**
 * Read Workload for ConvoLab Load Runner
 * Tests platform status, health checks, capabilities, prompts, and analytics.
 */

export const name = 'Read Workload';
export const provisionalP95TargetMs = 150; // Provisional engineering target

export function getEndpoints(targetUrl, context = {}) {
  const endpoints = [
    { method: 'GET', path: '/health/ready', name: 'Health Readiness' },
    { method: 'GET', path: '/health/live', name: 'Health Liveness' },
    { method: 'GET', path: '/api/platform/status', name: 'Platform Status' },
  ];

  if (context.isAuthenticated || context.sessionCookie) {
    endpoints.push(
      { method: 'GET', path: '/api/prompts', name: 'Prompt Templates' },
      { method: 'GET', path: '/api/workflows', name: 'Workflow Definitions' },
    );
  }

  return endpoints;
}

export async function execute(targetUrl, context = {}) {
  const endpoints = getEndpoints(targetUrl, context);
  const selected = endpoints[Math.floor(Math.random() * endpoints.length)];
  const headers = {};
  if (context.sessionCookie) {
    headers['Cookie'] = context.sessionCookie;
  }
  if (context.authToken) {
    headers['Authorization'] = `Bearer ${context.authToken}`;
  }

  const start = performance.now();
  try {
    const res = await fetch(`${targetUrl}${selected.path}`, {
      method: selected.method,
      headers
    });
    const latency = performance.now() - start;
    return {
      name: selected.name,
      status: res.status,
      latencyMs: latency,
      success: res.status >= 200 && res.status < 400
    };
  } catch (err) {
    const latency = performance.now() - start;
    return {
      name: selected.name,
      status: 0,
      latencyMs: latency,
      success: false,
      error: err.message
    };
  }
}
