#!/usr/bin/env node

/**
 * ConvoLab Endurance & Soak Runner
 *
 * Measures platform stability over extended durations under sustained concurrency.
 * Samples latency, throughput, error rates, endpoint-level performance, and
 * resource drift across fixed elapsed-time windows.
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

const targetUrl = (getArgValue('--target', process.env.CONVOLAB_TARGET_URL || 'http://localhost:5000')).replace(/\/$/, '');
const durationSec = parseInt(getArgValue('--duration', '30'), 10);
const concurrency = parseInt(getArgValue('--concurrency', '5'), 10);
const windowIntervalSec = parseInt(getArgValue('--window', '5'), 10);
const outputFile = getArgValue('--output', null);

const workloads = [readWorkload, writeWorkload, executionWorkload, securityWorkload];

function round(value, digits = 1) {
  const factor = 10 ** digits;
  return Math.round(value * factor) / factor;
}

function endpointKey(sample) {
  return `${sample.method || 'UNKNOWN'} ${sample.endpoint || 'UNKNOWN'}`;
}

function calculateStats(samples, intervalSec = windowIntervalSec) {
  const count = samples.length;
  if (count === 0) {
    return {
      count: 0,
      rps: 0,
      meanLatency: 0,
      p95: 0,
      errorRate: 0,
      statusCounts: {}
    };
  }

  const sorted = samples.map(sample => sample.latencyMs).sort((a, b) => a - b);
  const sum = sorted.reduce((total, latency) => total + latency, 0);
  const errors = samples.filter(sample => !sample.success).length;
  const p95Idx = Math.min(Math.floor(0.95 * count), count - 1);
  const statusCounts = {};

  for (const sample of samples) {
    const status = String(sample.status ?? 0);
    statusCounts[status] = (statusCounts[status] || 0) + 1;
  }

  return {
    count,
    rps: round(count / intervalSec),
    meanLatency: round(sum / count),
    p95: round(sorted[p95Idx]),
    errorRate: round((errors / count) * 100, 2),
    statusCounts
  };
}

function calculateWindowStats(windowData) {
  const aggregate = calculateStats(windowData);
  const grouped = new Map();

  for (const sample of windowData) {
    const key = endpointKey(sample);
    if (!grouped.has(key)) grouped.set(key, []);
    grouped.get(key).push(sample);
  }

  const endpointStats = {};
  for (const [key, samples] of grouped) {
    endpointStats[key] = {
      endpoint: samples[0].endpoint || 'UNKNOWN',
      method: samples[0].method || 'UNKNOWN',
      endpointName: samples[0].endpointName || samples[0].name || key,
      ...calculateStats(samples)
    };
  }

  return {
    ...aggregate,
    endpointStats
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

async function main() {
  console.log('================================================================');
  console.log(' ConvoLab Alpha.19 Endurance & Soak Test Runner');
  console.log('================================================================');
  console.log(`Target URL:     ${targetUrl}`);
  console.log(`Duration:       ${durationSec}s`);
  console.log(`Concurrency:    ${concurrency}`);
  console.log(`Window Size:    ${windowIntervalSec}s\n`);

  const authContext = await resolveAuthContext(targetUrl);
  const startTime = performance.now();
  const endTime = startTime + (durationSec * 1000);
  const windowSamples = new Map();
  const allSamples = [];
  let totalRequests = 0;
  let totalErrors = 0;

  function recordSample(sample, requestStartTime) {
    const elapsedMs = requestStartTime - startTime;
    const windowIndex = Math.floor(elapsedMs / (windowIntervalSec * 1000)) + 1;
    if (!windowSamples.has(windowIndex)) windowSamples.set(windowIndex, []);
    windowSamples.get(windowIndex).push(sample);
    allSamples.push(sample);
  }

  async function worker() {
    while (performance.now() < endTime) {
      const mod = workloads[Math.floor(Math.random() * workloads.length)];
      const requestStartTime = performance.now();
      const sample = await mod.execute(targetUrl, authContext);
      totalRequests++;
      if (!sample.success) totalErrors++;
      recordSample(sample, requestStartTime);
    }
  }

  const workers = Array.from({ length: concurrency }, () => worker());
  await Promise.all(workers);

  console.log('\n================================================================');
  console.log(' Endurance Analysis & Drift Assessment');
  console.log('================================================================');

  const windows = Array.from(windowSamples.entries())
    .sort(([left], [right]) => left - right)
    .map(([windowIndex, samples]) => {
      const stats = calculateWindowStats(samples);
      const elapsedSeconds = windowIndex * windowIntervalSec;
      const rssMb = round(process.memoryUsage().rss / (1024 * 1024));

      console.log(`[Window ${windowIndex.toString().padStart(2)}] RPS: ${stats.rps.toString().padStart(5)} | Mean: ${stats.meanLatency.toString().padStart(5)}ms | p95: ${stats.p95.toString().padStart(5)}ms | Errors: ${stats.errorRate}% | Process RSS: ${rssMb}MB`);
      for (const [key, endpoint] of Object.entries(stats.endpointStats)) {
        console.log(`  [${key}] Requests: ${endpoint.count} | RPS: ${endpoint.rps} | Mean: ${endpoint.meanLatency}ms | p95: ${endpoint.p95}ms | Errors: ${endpoint.errorRate}%`);
      }

      return {
        windowIndex,
        elapsedSeconds,
        ...stats,
        runnerRssMb: rssMb
      };
    });

  if (windows.length >= 2) {
    const firstWindow = windows[0];
    const lastWindow = windows[windows.length - 1];
    const latencyDriftMs = round(lastWindow.meanLatency - firstWindow.meanLatency);
    const rpsDrift = round(lastWindow.rps - firstWindow.rps);
    const rssDriftMb = round(lastWindow.runnerRssMb - firstWindow.runnerRssMb);

    console.log(`Initial Window Mean Latency:  ${firstWindow.meanLatency}ms`);
    console.log(`Final Window Mean Latency:    ${lastWindow.meanLatency}ms (Drift: ${latencyDriftMs >= 0 ? '+' : ''}${latencyDriftMs}ms)`);
    console.log(`Throughput Drift:             ${rpsDrift >= 0 ? '+' : ''}${rpsDrift} RPS`);
    console.log(`Runner Memory RSS Drift:      ${rssDriftMb >= 0 ? '+' : ''}${rssDriftMb} MB`);
    console.log(`Overall Error Rate:           ${totalRequests > 0 ? round((totalErrors / totalRequests) * 100, 2) : 0}% (${totalErrors}/${totalRequests})`);

    const degradationDetected = latencyDriftMs > 100 || lastWindow.errorRate > 5;
    console.log(`\nDegradation Verdict: ${degradationDetected ? '⚠️ Degradation Detected' : '🟢 Stable Performance Observed'}`);
  } else {
    console.log('Short test run: insufficient windows to compute longitudinal drift.');
  }

  const endpointTotals = {};
  for (const sample of allSamples) {
    const key = endpointKey(sample);
    if (!endpointTotals[key]) endpointTotals[key] = [];
    endpointTotals[key].push(sample);
  }

  const endpointSummary = {};
  for (const [key, samples] of Object.entries(endpointTotals)) {
    endpointSummary[key] = {
      endpoint: samples[0].endpoint || 'UNKNOWN',
      method: samples[0].method || 'UNKNOWN',
      endpointName: samples[0].endpointName || samples[0].name || key,
      ...calculateStats(samples, durationSec)
    };
  }

  if (outputFile) {
    const report = {
      timestamp: new Date().toISOString(),
      targetUrl,
      durationSec,
      concurrency,
      windowIntervalSec,
      totalRequests,
      totalErrors,
      endpointTotals: endpointSummary,
      windows
    };
    fs.mkdirSync(path.dirname(path.resolve(outputFile)), { recursive: true });
    fs.writeFileSync(path.resolve(outputFile), JSON.stringify(report, null, 2), 'utf8');
    console.log(`\nSoak test report saved to: ${outputFile}`);
  }
}

main().catch(err => {
  console.error('Fatal soak runner error:', err);
  process.exit(1);
});
