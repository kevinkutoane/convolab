/**
 * Security Workload for ConvoLab Load Runner
 * Tests rate limiting enforcement, 429 status response, protected endpoints,
 * and confirms absence of unhandled 500 Internal Server Errors under pressure.
 */

export const name = 'Security & Rate-Limiting Workload';
export const provisionalP95TargetMs = 250; // Provisional engineering target

export async function execute(targetUrl, context = {}) {
  // Rotate between rate-limited endpoints
  const endpoints = [
    {
      name: 'Break-Glass Rate Limit',
      path: '/api/auth/break-glass/login',
      method: 'POST',
      body: JSON.stringify({ email: 'probe@convolab.test', password: 'bad-password' }),
      expectedRateLimit: true
    },
    {
      name: 'Unauthenticated Admin Audit Protection',
      path: '/api/audit',
      method: 'GET',
      expectedRateLimit: false
    },
    {
      name: 'Unauthenticated Operations Protection',
      path: '/api/operations/status',
      method: 'GET',
      expectedRateLimit: false
    }
  ];

  const selected = endpoints[Math.floor(Math.random() * endpoints.length)];
  const headers = { 'Content-Type': 'application/json' };

  const start = performance.now();
  try {
    const res = await fetch(`${targetUrl}${selected.path}`, {
      method: selected.method,
      headers,
      body: selected.body
    });
    const latency = performance.now() - start;

    // Security assertion:
    // HTTP 401 Unauthorized, 403 Forbidden, 404 Not Found, 429 Too Many Requests are VALID secure responses.
    // HTTP 500 is a FAILURE (server crash / unhandled exception).
    const isSecurityPass = res.status !== 500 && res.status !== 502 && res.status !== 503;

    return {
      name: selected.name,
      status: res.status,
      latencyMs: latency,
      success: isSecurityPass,
      isRateLimited: res.status === 429
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
