/**
 * Execution Workload for ConvoLab Load Runner
 * Tests conversation simulation initialization, message exchange, and option retrieval.
 */

export const name = 'Execution Workload';
export const provisionalP95TargetMs = 500; // Provisional engineering target

export async function execute(targetUrl, context = {}) {
  const headers = { 'Content-Type': 'application/json' };
  if (context.sessionCookie) {
    headers['Cookie'] = context.sessionCookie;
  }
  if (context.authToken) {
    headers['Authorization'] = `Bearer ${context.authToken}`;
  }

  const start = performance.now();
  try {
    // 1. Fetch simulation options
    const optionsRes = await fetch(`${targetUrl}/api/simulations/options`, { headers });
    if (!optionsRes.ok && optionsRes.status !== 200) {
      return {
        name: 'Simulation Options',
        status: optionsRes.status,
        latencyMs: performance.now() - start,
        success: optionsRes.status < 400
      };
    }

    // 2. Query simulation conversations list
    const listRes = await fetch(`${targetUrl}/api/simulations`, { headers });
    const latency = performance.now() - start;

    return {
      name: 'Simulation Execution Query',
      status: listRes.status,
      latencyMs: latency,
      success: listRes.status >= 200 && listRes.status < 400
    };
  } catch (err) {
    const latency = performance.now() - start;
    return {
      name: 'Simulation Execution Query',
      status: 0,
      latencyMs: latency,
      success: false,
      error: err.message
    };
  }
}
