import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawn } from "node:child_process";

const baseUrl = process.env.CONVOLAB_BROWSER_BASE_URL ?? "http://127.0.0.1:3000";
const debugPort = Number(process.env.CONVOLAB_BROWSER_DEBUG_PORT ?? 9333);
const browserCandidates = [
  process.env.CONVOLAB_BROWSER_PATH,
  "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
  "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
  "C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe",
].filter(Boolean);
const browserPath = browserCandidates.find(candidate => fs.existsSync(candidate));
assert.ok(browserPath, "Chrome or Edge is required for the browser smoke test.");

const tempPrefix = path.join(os.tmpdir(), "convolab-browser-");
const userDataDirectory = fs.mkdtempSync(tempPrefix);
const browser = spawn(browserPath, [
  "--headless=new",
  "--disable-gpu",
  "--disable-background-networking",
  "--no-first-run",
  "--no-default-browser-check",
  `--remote-debugging-port=${debugPort}`,
  `--user-data-dir=${userDataDirectory}`,
  `${baseUrl}/`,
], { stdio: "ignore" });

let socket;
let nextCommandId = 0;
const pendingCommands = new Map();
const browserErrors = [];

function delay(milliseconds) {
  return new Promise(resolve => setTimeout(resolve, milliseconds));
}

async function waitFor(description, assertion, attempts = 50) {
  let lastError;
  for (let attempt = 0; attempt < attempts; attempt += 1) {
    try {
      const result = await assertion();
      if (result) return result;
    } catch (error) {
      lastError = error;
    }
    await delay(100);
  }
  throw new Error(`Timed out waiting for ${description}.${lastError ? ` ${lastError.message}` : ""}`);
}

async function connect() {
  const target = await waitFor("the browser debugging target", async () => {
    const response = await fetch(`http://127.0.0.1:${debugPort}/json/list`);
    if (!response.ok) return undefined;
    const targets = await response.json();
    return targets.find(item => item.type === "page" && item.webSocketDebuggerUrl);
  });

  socket = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => {
    socket.addEventListener("open", resolve, { once: true });
    socket.addEventListener("error", reject, { once: true });
  });
  socket.addEventListener("message", event => {
    const message = JSON.parse(event.data);
    if (message.id) {
      const pending = pendingCommands.get(message.id);
      if (!pending) return;
      pendingCommands.delete(message.id);
      if (message.error) pending.reject(new Error(message.error.message));
      else pending.resolve(message.result);
      return;
    }
    if (message.method === "Runtime.exceptionThrown") {
      browserErrors.push(message.params.exceptionDetails.text);
    }
    if (message.method === "Log.entryAdded" && message.params.entry.level === "error") {
      browserErrors.push(message.params.entry.text);
    }
  });
}

function command(method, params = {}) {
  const id = ++nextCommandId;
  return new Promise((resolve, reject) => {
    pendingCommands.set(id, { resolve, reject });
    socket.send(JSON.stringify({ id, method, params }));
  });
}

async function evaluate(expression) {
  const result = await command("Runtime.evaluate", {
    expression,
    returnByValue: true,
    awaitPromise: true,
  });
  if (result.exceptionDetails) throw new Error(result.exceptionDetails.text);
  return result.result.value;
}

async function navigate(route, expectedText) {
  const routeSelector = route.startsWith("/documentation/")
    ? ".documentation-page"
    : route === "/evaluation" || route === "/evaluations"
      ? ".evaluation-page"
      : route === "/traces"
        ? ".trace-explorer-page"
        : route === "/replay"
          ? ".replay-studio-page"
          : route === "/policies"
            ? ".policy-center-page"
            : route === "/plugins"
              ? ".plugin-center-page"
              : ".studio-content";
  await command("Page.navigate", { url: `${baseUrl}${route}` });
  await waitFor(`${route} to render`, async () => evaluate(`
    location.pathname === ${JSON.stringify(route)}
      && document.readyState === "complete"
      && document.querySelector(${JSON.stringify(routeSelector)})?.textContent?.includes(${JSON.stringify(expectedText)})
  `));
  const title = await evaluate("document.title");
  assert.match(title, /ConvoLab/i);
}

async function clickText(text, selector = "button, a") {
  const clicked = await evaluate(`(() => {
    const normalize = value => value?.replace(/\\s+/g, " ").trim();
    const target = [...document.querySelectorAll(${JSON.stringify(selector)})]
      .find(item => normalize(item.textContent)?.includes(${JSON.stringify(text)}));
    if (!target) return false;
    target.click();
    return true;
  })()`);
  if (!clicked) {
    const available = await evaluate(`
      [...document.querySelectorAll(${JSON.stringify(selector)})]
        .map(item => item.textContent?.replace(/\\s+/g, " ").trim())
        .filter(Boolean)
    `);
    assert.fail(`Could not find '${text}' in ${selector}. Available: ${available.join(" | ")}`);
  }
}

async function clickSelector(selector) {
  const clicked = await evaluate(`(() => {
    const target = document.querySelector(${JSON.stringify(selector)});
    if (!target) return false;
    target.click();
    return true;
  })()`);
  assert.equal(clicked, true, `Could not find ${selector}.`);
}

async function waitForText(text) {
  await waitFor(`text '${text}'`, async () => evaluate(
    `document.querySelector("#root")?.textContent?.includes(${JSON.stringify(text)})`,
  ));
}

async function waitForSelector(selector, present = true) {
  await waitFor(`${selector} to be ${present ? "present" : "absent"}`, async () => evaluate(
    `Boolean(document.querySelector(${JSON.stringify(selector)})) === ${present}`,
  ));
}

async function chooseSecondOption(selector) {
  return evaluate(`(() => {
    const target = document.querySelector(${JSON.stringify(selector)});
    if (!target || target.options.length < 2) return false;
    target.selectedIndex = 1;
    target.dispatchEvent(new Event("change", { bubbles: true }));
    return true;
  })()`);
}

try {
  await connect();
  await command("Page.enable");
  await command("Runtime.enable");
  await command("Log.enable");
  await command("Emulation.setDeviceMetricsOverride", {
    width: 1440,
    height: 1000,
    deviceScaleFactor: 1,
    mobile: false,
  });

  const routes = [
    ["/evaluation", "Evaluation Studio"],
    ["/evaluations", "Evaluation Studio"],
    ["/traces", "Trace Explorer"],
    ["/replay", "Replay Studio"],
    ["/policies", "Policy Center"],
    ["/plugins", "Plugin Center"],
    ["/documentation/evaluation", "Evaluation Studio"],
    ["/documentation/traces", "Trace Explorer"],
    ["/documentation/replay", "Replay Studio"],
    ["/documentation/policies", "Policy Center"],
    ["/documentation/plugins", "Plugin Center"],
  ];
  for (const [route, heading] of routes) await navigate(route, heading);

  await navigate("/evaluation", "Evaluation Studio");
  await clickText("Documentation", "a");
  await waitFor("Evaluation documentation navigation", async () => evaluate(
    `location.pathname === "/documentation/evaluation"`,
  ));
  await navigate("/evaluation", "Evaluation Studio");
  await clickText("New scorecard", "button");
  await waitForText("Create scorecard");
  await clickText("New scorecard", "button");
  await waitForSelector(".evaluation-create-panel", false);

  await navigate("/traces", "Trace Explorer");
  await waitForSelector(".trace-table tbody tr");
  await clickSelector(".trace-table tbody tr");
  await waitForSelector(".trace-inspector-tabs");
  for (const tab of ["events", "artifacts", "context", "spans"]) {
    await clickText(tab, ".trace-inspector-tabs button");
    await waitFor(`${tab} tab activation`, async () => evaluate(`
      [...document.querySelectorAll(".trace-inspector-tabs button")]
        .some(item => item.textContent.trim() === ${JSON.stringify(tab)} && item.classList.contains("active"))
    `));
  }
  const hasCapability = await evaluate("Boolean(document.querySelector('.trace-capability-strip button'))");
  if (hasCapability) {
    await clickSelector(".trace-capability-strip button");
    await clickSelector(".trace-capability-strip button");
  }
  await chooseSecondOption(".trace-filter-grid select");

  await navigate("/replay", "Replay Studio");
  await clickText("New experiment", "button");
  await waitForText("Create from an immutable baseline");
  const hasCancel = await evaluate(`
    [...document.querySelectorAll("button")].some(item => item.textContent.trim() === "Cancel")
  `);
  if (hasCancel) {
    await clickText("Cancel", "button");
    await waitForSelector(".replay-create-panel", false);
  }

  await navigate("/policies", "Policy Center");
  await clickText("New policy", "button");
  await waitForText("Create governance policy");
  await clickText("Cancel", "button");
  await waitForSelector(".policy-editor", false);
  await chooseSecondOption(".policy-filter-row select");
  await clickSelector(".policy-list-item");

  await navigate("/plugins", "Plugin Center");
  await clickText("Documentation", "a");
  await waitFor("Plugin documentation navigation", async () => evaluate(
    `location.pathname === "/documentation/plugins"`,
  ));
  await navigate("/plugins", "Plugin Center");
  await clickText("Register plugin", "button");
  await waitForText("New extension");
  await clickText("Cancel", "button");
  await waitForSelector(".plugin-editor-panel", false);
  await chooseSecondOption('.plugin-filter-grid select[aria-label="Filter plugins by category"]');
  await chooseSecondOption('.plugin-filter-grid select[aria-label="Filter plugins by lifecycle status"]');
  await chooseSecondOption('.plugin-filter-grid select[aria-label="Filter plugins by health"]');
  await navigate("/plugins", "Plugin Center");
  await waitForSelector(".plugin-list-item");
  await clickSelector(".plugin-list-item");
  await clickText("Check health", "button");
  await waitFor("Plugin health check completion", async () => evaluate(
    `![...document.querySelectorAll("button")].find(item => item.textContent.includes("Check health"))?.disabled`,
  ));
  await clickText("New version", "button");
  await waitForText("Immutable successor");
  await clickText("Cancel", "button");
  await waitForSelector(".plugin-editor-panel", false);
  await clickText("Deprecate this version", "button");
  await waitForSelector('.plugin-deprecation-confirm[role="dialog"]');
  await clickText("Cancel", ".plugin-deprecation-confirm button");
  await waitForSelector(".plugin-deprecation-confirm", false);

  await clickSelector('button[aria-label="Notifications"]');
  await waitForSelector('.notification-popover[role="dialog"]');
  await clickText("Mark read", "button");
  await clickSelector('button[aria-label="Notifications"]');
  await waitForSelector('.notification-popover[role="dialog"]', false);
  await clickSelector(".global-search");
  await waitForSelector('[role="dialog"]');
  await command("Input.dispatchKeyEvent", { type: "keyDown", key: "Escape", code: "Escape" });
  await command("Input.dispatchKeyEvent", { type: "keyUp", key: "Escape", code: "Escape" });

  await command("Emulation.setDeviceMetricsOverride", {
    width: 390,
    height: 844,
    deviceScaleFactor: 1,
    mobile: true,
  });
  await navigate("/evaluation", "Evaluation Studio");
  await clickSelector('button[aria-label="Open navigation"]');
  await waitForSelector(".sidebar-mobile-open");
  await clickText("Trace Explorer", ".sidebar-nav a");
  await waitFor("responsive navigation", async () => evaluate(
    `location.pathname === "/traces" && !document.querySelector(".sidebar-mobile-open")`,
  ));

  assert.deepEqual(browserErrors, [], `Browser console errors: ${browserErrors.join(" | ")}`);
  console.log(`Browser smoke passed (${routes.length} routes plus desktop and responsive interactions).`);
} finally {
  try {
    if (socket?.readyState === WebSocket.OPEN) await command("Browser.close");
  } catch {
    browser.kill();
  }
  await Promise.race([
    new Promise(resolve => browser.once("exit", resolve)),
    delay(5_000),
  ]);
  if (browser.exitCode === null) browser.kill();
  await delay(500);
  const resolvedTemp = path.resolve(userDataDirectory);
  assert.ok(resolvedTemp.startsWith(path.resolve(tempPrefix)), "Refusing to remove an unexpected browser profile path.");
  fs.rmSync(resolvedTemp, { recursive: true, force: true, maxRetries: 10, retryDelay: 250 });
}
