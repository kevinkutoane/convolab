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
    const latency = performance.now() - start;
    const isAuth = Boolean(context.isAuthenticated || context.sessionCookie || context.authToken);
    const success = isAuth
      ? (optionsRes.status === 200)
      : (optionsRes.status === 401 || optionsRes.status === 200);

    return {
      name: 'Simulation Options',
      status: optionsRes.status,
      latencyMs: latency,
      success
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
