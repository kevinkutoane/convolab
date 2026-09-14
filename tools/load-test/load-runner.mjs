#!/usr/bin/env node

/**
 * ConvoLab Native Load Test Runner
 * 
 * Executes representative workloads (Read, Write, Execution, Security) with
 * configurable concurrency and duration without external binary dependencies.
 * Collects latency percentiles, throughput, error rates, and HTTP status distribution.
 */

import fs from 'node:fs';
import path from 'node:path';
import * as readWorkload from './workloads/read-workload.mjs';
import * as writeWorkload from './workloads/write-workload.mjs';
import * as executionWorkload from './workloads/execution-workload.mjs';
import * as securityWorkload from './workloads/security-workload.mjs';

const args = process.argv.slice(2);

function getArgValue(flag, defaultVal) {
  const idx = args.indexOf(flag);
  if (idx !== -1 && idx + 1 < args.length) return args[idx + 1];
  return defaultVal;
}

const isSmoke = args.includes('--smoke');
const targetUrl = (getArgValue('--target', process.env.CONVOLAB_TARGET_URL || 'http://localhost:5000')).replace(/\/$/, '');
const selectedWorkload = getArgValue('--workload', 'all').toLowerCase();
const concurrency = parseInt(getArgValue('--concurrency', isSmoke ? '2' : '5'), 10);
const durationSec = parseInt(getArgValue('--duration', isSmoke ? '5' : '10'), 10);
const maxRequests = parseInt(getArgValue('--requests', '0'), 10);
const outputFile = getArgValue('--output', null);

const workloads = {
  read: readWorkload,
  write: writeWorkload,
  execution: executionWorkload,
  security: securityWorkload
};

function calculatePercentiles(latencies) {
  if (latencies.length === 0) return { min: 0, mean: 0, p50: 0, p90: 0, p95: 0, p99: 0, max: 0 };
  const sorted = [...latencies].sort((a, b) => a - b);
  const sum = sorted.reduce((acc, v) => acc + v, 0);

  const getP = (p) => {
    const idx = Math.min(Math.floor((p / 100) * sorted.length), sorted.length - 1);
    return sorted[idx];
  };

  return {
    min: Math.round(sorted[0] * 100) / 100,
    mean: Math.round((sum / sorted.length) * 100) / 100,
    p50: Math.round(getP(50) * 100) / 100,
    p90: Math.round(getP(90) * 100) / 100,
    p95: Math.round(getP(95) * 100) / 100,
    p99: Math.round(getP(99) * 100) / 100,
    max: Math.round(sorted[sorted.length - 1] * 100) / 100
  };
}

async function resolveAuthContext(targetUrl) {
  const envEmail = process.env.CONVOLAB_ACCEPTANCE_ADMIN_EMAIL
    || process.env.CONVOLAB_BOOTSTRAP_ADMIN_EMAIL
    || getArgValue('--email', null);
  const envPassword = process.env.CONVOLAB_ACCEPTANCE_ADMIN_PASSWORD
    || process.env.CONVOLAB_BOOTSTRAP_ADMIN_PASSWORD
    || getArgValue('--password', null);

  const candidateCreds = [
    { email: envEmail, password: envPassword },
    { email: 'acceptance-admin@convolab.test', password: 'Acceptance-Only-Alpha12!' },
    { email: 'admin@convolab.test', password: 'Ephemeral-Alpha12!' }
  ].filter(c => Boolean(c.email && c.password));

  for (const cred of candidateCreds) {
    try {
      const loginRes = await fetch(`${targetUrl}/api/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: cred.email, password: cred.password })
      });

      if (!loginRes.ok) continue;

      const extractCookies = (res) => {
        if (typeof res.headers.getSetCookie === 'function') {
          return res.headers.getSetCookie();
        }
        const single = res.headers.get('set-cookie');
        return single ? [single] : [];
      };

      const cookieJar = new Map();
      const addCookies = (rawList) => {
        for (const raw of rawList) {
          if (!raw) continue;
          const firstPart = raw.split(';')[0];
          const eqIdx = firstPart.indexOf('=');
          if (eqIdx !== -1) {
            cookieJar.set(firstPart.substring(0, eqIdx).trim(), firstPart.substring(eqIdx + 1).trim());
          }
        }
      };

      addCookies(extractCookies(loginRes));

      let xsrfToken = null;
      try {
        const cookieHeaderForAnti = Array.from(cookieJar.entries()).map(([k, v]) => `${k}=${v}`).join('; ');
        const antiRes = await fetch(`${targetUrl}/api/auth/antiforgery`, {
          headers: { 'Cookie': cookieHeaderForAnti }
        });
        if (antiRes.ok) {
          addCookies(extractCookies(antiRes));
          const antiData = await antiRes.json();
          xsrfToken = antiData.token;
        }
      } catch {
        // ignore
      }

      const sessionCookie = Array.from(cookieJar.entries()).map(([k, v]) => `${k}=${v}`).join('; ');
      console.log(`[AUTH] Authenticated as ${cred.email}`);
      return { sessionCookie, xsrfToken, isAuthenticated: true };
    } catch {
      // Continue to next
    }
  }

  console.log('[AUTH] No valid credentials found. Running in anonymous client mode.');
  return { isAuthenticated: false };
}

async function runWorkload(name, workloadModule, authContext) {
  console.log(`\n--- Running Workload: ${workloadModule.name} ---`);
  console.log(`Target: ${targetUrl} | Concurrency: ${concurrency} | Duration: ${durationSec}s`);

  const latencies = [];
  const statusCounts = {};
  let totalRequests = 0;
  let successfulRequests = 0;
  let failedRequests = 0;
  let rateLimitedCount = 0;

  const startTime = performance.now();
  const endTimeTarget = startTime + (durationSec * 1000);

  async function worker() {
    while (performance.now() < endTimeTarget) {
      if (maxRequests > 0 && totalRequests >= maxRequests) break;
      totalRequests++;

      const res = await workloadModule.execute(targetUrl, authContext);
      latencies.push(res.latencyMs);

      statusCounts[res.status] = (statusCounts[res.status] || 0) + 1;
      if (res.success) {
        successfulRequests++;
      } else {
        failedRequests++;
      }
      if (res.isRateLimited) {
        rateLimitedCount++;
      }
    }
  }

  // Spawn concurrent worker promises
  const workers = Array.from({ length: concurrency }, () => worker());
  await Promise.all(workers);

  const actualDurationSec = (performance.now() - startTime) / 1000;
  const throughputRps = Math.round((totalRequests / actualDurationSec) * 100) / 100;
  const percentiles = calculatePercentiles(latencies);
  const errorRatePercent = totalRequests > 0 ? Math.round((failedRequests / totalRequests) * 10000) / 100 : 0;

  console.log(`  Requests:    Total: ${totalRequests} | Succeeded: ${successfulRequests} | Failed: ${failedRequests}`);
  console.log(`  Throughput:  ${throughputRps} req/sec (Actual Duration: ${actualDurationSec.toFixed(2)}s)`);
  console.log(`  Latencies:   p50: ${percentiles.p50}ms | p90: ${percentiles.p90}ms | p95: ${percentiles.p95}ms | p99: ${percentiles.p99}ms (max: ${percentiles.max}ms)`);
  console.log(`  Status Dist: ${JSON.stringify(statusCounts)}`);
  if (rateLimitedCount > 0) {
    console.log(`  Rate Limits: ${rateLimitedCount} requests throttled with HTTP 429`);
  }

  const provisionalTarget = workloadModule.provisionalP95TargetMs;
  if (provisionalTarget) {
    const isPassed = percentiles.p95 <= provisionalTarget && failedRequests === 0;
    const targetStatus = isPassed ? 'PASSED' : (failedRequests > 0 ? 'FAILED (ERRORS)' : 'EXCEEDED');
    console.log(`  Target:      Provisional p95 < ${provisionalTarget}ms -> [${targetStatus}] (Observed: ${percentiles.p95}ms)`);
  }

  return {
    workload: name,
    workloadTitle: workloadModule.name,
    totalRequests,
    successfulRequests,
    failedRequests,
    throughputRps,
    durationSec: Math.round(actualDurationSec * 100) / 100,
    percentiles,
    statusCounts,
    errorRatePercent,
    provisionalP95TargetMs: provisionalTarget || null,
    targetMet: provisionalTarget ? (percentiles.p95 <= provisionalTarget && failedRequests === 0) : null
  };
}

async function main() {
  console.log('================================================================');
  console.log(' ConvoLab Alpha.19 Performance & Load Runner');
  console.log('================================================================');
  console.log(`Timestamp:   ${new Date().toISOString()}`);
  console.log(`Target URL:  ${targetUrl}`);
  console.log(`Mode:        ${isSmoke ? 'Smoke (Lightweight CI Gate)' : 'Standard Load Test'}`);

  const authContext = await resolveAuthContext(targetUrl);

  const activeWorkloads = selectedWorkload === 'all'
    ? Object.keys(workloads)
    : [selectedWorkload];

  const results = [];
  for (const w of activeWorkloads) {
    if (!workloads[w]) {
      console.error(`Unknown workload: '${w}'. Available: ${Object.keys(workloads).join(', ')}`);
      process.exit(1);
    }
    const res = await runWorkload(w, workloads[w], authContext);
    results.push(res);
  }

  console.log('\n================================================================');
  console.log(' Performance Run Summary');
  console.log('================================================================');
  for (const r of results) {
    console.log(`${r.workloadTitle.padEnd(35)}: ${r.throughputRps.toString().padStart(6)} RPS | p95: ${r.percentiles.p95.toString().padStart(6)}ms | Errors: ${r.errorRatePercent}%`);
  }

  if (outputFile) {
    const reportData = {
      timestamp: new Date().toISOString(),
      targetUrl,
      concurrency,
      durationSec,
      isSmoke,
      results
    };
    fs.mkdirSync(path.dirname(path.resolve(outputFile)), { recursive: true });
    fs.writeFileSync(path.resolve(outputFile), JSON.stringify(reportData, null, 2), 'utf8');
    console.log(`\nDetailed summary written to: ${outputFile}`);
  }

  // Evaluate gross failure: any workload having > 50% error rate indicates server crash
  const catastrophicFailure = results.some(r => r.errorRatePercent > 50);
  if (catastrophicFailure) {
    console.error('\nFAILURE: Catastrophic error rate detected (>50% failed requests).');
    process.exit(1);
  }

  console.log('\nSUCCESS: Performance execution completed without gross regression.');
  process.exit(0);
}

main().catch(err => {
  console.error('Fatal load runner error:', err);
  process.exit(1);
});
