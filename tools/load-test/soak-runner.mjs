#!/usr/bin/env node

/**
 * ConvoLab Endurance & Soak Runner
 * 
 * Measures platform stability over extended durations under sustained concurrency.
 * Samples latency, throughput, error rates, and resource drift across time windows
 * to detect socket exhaustion, memory leaks, or connection pool degradation.
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

function calculateWindowStats(windowData) {
  const count = windowData.length;
  if (count === 0) return { count: 0, rps: 0, meanLatency: 0, p95: 0, errorRate: 0 };
  const sorted = windowData.map(d => d.latencyMs).sort((a, b) => a - b);
  const sum = sorted.reduce((a, b) => a + b, 0);
  const errors = windowData.filter(d => !d.success).length;
  const p95Idx = Math.min(Math.floor(0.95 * count), count - 1);

  return {
    count,
    rps: Math.round((count / windowIntervalSec) * 10) / 10,
    meanLatency: Math.round((sum / count) * 10) / 10,
    p95: Math.round(sorted[p95Idx] * 10) / 10,
    errorRate: Math.round((errors / count) * 10000) / 100
  };
}

async function main() {
  console.log('================================================================');
  console.log(' ConvoLab Alpha.19 Endurance & Soak Test Runner');
  console.log('================================================================');
  console.log(`Target URL:     ${targetUrl}`);
  console.log(`Duration:       ${durationSec}s`);
  console.log(`Concurrency:    ${concurrency}`);
  console.log(`Window Size:    ${windowIntervalSec}s\n`);

  const startTime = performance.now();
  const endTime = startTime + (durationSec * 1000);
  const windows = [];
  let currentWindowSamples = [];
  let currentWindowIndex = 1;
  let nextWindowEnd = startTime + (windowIntervalSec * 1000);

  let totalRequests = 0;
  let totalErrors = 0;

  async function worker() {
    while (performance.now() < endTime) {
      // Pick random workload module
      const mod = workloads[Math.floor(Math.random() * workloads.length)];
      const sample = await mod.execute(targetUrl);
      totalRequests++;
      if (!sample.success) totalErrors++;

      currentWindowSamples.push(sample);

      if (performance.now() >= nextWindowEnd) {
        const stats = calculateWindowStats(currentWindowSamples);
        const mem = process.memoryUsage();
        const rssMb = Math.round((mem.rss / (1024 * 1024)) * 10) / 10;
        
        console.log(`[Window ${currentWindowIndex.toString().padStart(2)}] RPS: ${stats.rps.toString().padStart(5)} | Mean: ${stats.meanLatency.toString().padStart(5)}ms | p95: ${stats.p95.toString().padStart(5)}ms | Errors: ${stats.errorRate}% | Process RSS: ${rssMb}MB`);
        
        windows.push({
          windowIndex: currentWindowIndex,
          elapsedSeconds: currentWindowIndex * windowIntervalSec,
          ...stats,
          runnerRssMb: rssMb
        });

        currentWindowSamples = [];
        currentWindowIndex++;
        nextWindowEnd = performance.now() + (windowIntervalSec * 1000);
      }
    }
  }

  const workers = Array.from({ length: concurrency }, () => worker());
  await Promise.all(workers);

  console.log('\n================================================================');
  console.log(' Endurance Analysis & Drift Assessment');
  console.log('================================================================');

  if (windows.length >= 2) {
    const firstWindow = windows[0];
    const lastWindow = windows[windows.length - 1];

    const latencyDriftMs = Math.round((lastWindow.meanLatency - firstWindow.meanLatency) * 10) / 10;
    const rpsDrift = Math.round((lastWindow.rps - firstWindow.rps) * 10) / 10;
    const rssDriftMb = Math.round((lastWindow.runnerRssMb - firstWindow.runnerRssMb) * 10) / 10;

    console.log(`Initial Window Mean Latency:  ${firstWindow.meanLatency}ms`);
    console.log(`Final Window Mean Latency:    ${lastWindow.meanLatency}ms (Drift: ${latencyDriftMs >= 0 ? '+' : ''}${latencyDriftMs}ms)`);
    console.log(`Throughput Drift:             ${rpsDrift >= 0 ? '+' : ''}${rpsDrift} RPS`);
    console.log(`Runner Memory RSS Drift:      ${rssDriftMb >= 0 ? '+' : ''}${rssDriftMb} MB`);
    console.log(`Overall Error Rate:           ${totalRequests > 0 ? (totalErrors / totalRequests * 100).toFixed(2) : 0}% (${totalErrors}/${totalRequests})`);

    const degradationDetected = latencyDriftMs > 100 || lastWindow.errorRate > 5;
    console.log(`\nDegradation Verdict: ${degradationDetected ? '⚠️ Degradation Detected' : '🟢 Stable Performance Observed'}`);
  } else {
    console.log('Short test run: insufficient windows to compute longitudinal drift.');
  }

  if (outputFile) {
    const report = {
      timestamp: new Date().toISOString(),
      targetUrl,
      durationSec,
      concurrency,
      totalRequests,
      totalErrors,
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
