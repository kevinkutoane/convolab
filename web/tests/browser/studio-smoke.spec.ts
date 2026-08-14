import { expect, test } from "@playwright/test";

const routes = [
  ["/", "ConvoLab Studio"],
  ["/conversations", "Conversation Simulator"],
  ["/workflows", "Workflow Designer"],
  ["/prompts", "Prompt Studio"],
  ["/knowledge", "Knowledge Studio"],
  ["/intelligence", "Intelligence Center"],
  ["/evaluation", "Evaluation Studio"],
  ["/evaluations", "Evaluation Studio"],
  ["/traces", "Trace Explorer"],
  ["/replay", "Replay Studio"],
  ["/policies", "Policy Center"],
  ["/plugins", "Plugin Center"],
  ["/analytics", "Platform Analytics"],
  ["/settings", "Settings"],
  ["/settings/provider", "AI Provider"],
  ["/settings/governance", "Policies & Plugins"],
  ["/documentation", "ConvoLab documentation"],
  ["/workspace", "Default Workspace"],
] as const;

const adminEmail = process.env.CONVOLAB_ACCEPTANCE_ADMIN_EMAIL ?? process.env.CONVOLAB_BOOTSTRAP_ADMIN_EMAIL;
const stableRoutes = [
  "/conversations", "/workflows", "/prompts", "/knowledge", "/intelligence",
  "/evaluation", "/evaluations", "/traces", "/replay", "/policies", "/plugins",
] as const;

test.beforeEach(async ({ page }) => {
  await page.goto("/");
  await expect(page).toHaveURL(/\/$/);
});

test("every canonical and compatibility route loads without browser errors", async ({ page }) => {
  test.setTimeout(60_000);
  const errors: string[] = [];
  page.on("pageerror", error => errors.push(error.message));
  page.on("console", message => { if (message.type() === "error") errors.push(message.text()); });

  for (const [route, heading] of routes) {
    await page.goto(route);
    await expect(page.locator("#root")).toContainText(heading);
    await expect(page.locator(".async-loading")).toHaveCount(0);
  }
  expect(errors).toEqual([]);
});

test("stable screens and API connectivity are visible in the global shell", async ({ page }) => {
  await expect(page.getByTestId("api-connectivity")).toHaveClass(/api-online/);
  await expect(page.getByTestId("api-connectivity")).toContainText("API online");

  for (const route of stableRoutes) {
    await page.goto(route);
    await expect(page.locator(".topbar .status-pill.status-stable")).toBeVisible();
    await expect(page.locator(".topbar .status-pill.status-stable")).toContainText("Stable");
  }
});

test("global API notification reports an outage and recovery", async ({ page }) => {
  await page.route("**/api/platform/status", route => route.fulfill({
    status: 503,
    contentType: "application/problem+json",
    body: JSON.stringify({ detail: "Readiness is temporarily unavailable." }),
  }));
  await page.reload();
  await expect(page.getByTestId("api-connectivity")).toHaveClass(/api-offline/);
  await expect(page.getByTestId("api-connectivity")).toContainText("API offline");

  await page.unroute("**/api/platform/status");
  await page.evaluate(() => window.dispatchEvent(new Event("convolab:platform-status")));
  await expect(page.getByTestId("api-connectivity")).toHaveClass(/api-online/);
  await expect(page.getByTestId("api-connectivity")).toContainText("API online");
});

test("governance workspaces expose functional dialogs, tabs and documentation", async ({ page }) => {
  await page.goto("/evaluation");
  await page.getByRole("button", { name: /new scorecard/i }).click();
  await expect(page.getByText("Create scorecard")).toBeVisible();
  await page.getByRole("button", { name: /new scorecard/i }).click();

  await page.goto("/traces");
  const trace = page.locator(".trace-table tbody tr").first();
  if (await trace.count()) {
    await trace.click();
    for (const name of ["spans", "events", "artifacts", "context"]) {
      await page.locator(".trace-inspector-tabs").getByRole("button", { name }).click();
    }
  }

  await page.goto("/replay");
  await page.getByRole("button", { name: /new experiment/i }).click();
  await expect(
    page.getByRole("heading", { name: "Create from an immutable baseline", exact: true }),
  ).toBeVisible();

  await page.goto("/policies");
  await page.getByRole("button", { name: /new policy/i }).click();
  await expect(page.getByText(/create governance policy/i)).toBeVisible();
  await page.getByRole("button", { name: /cancel/i }).click();

  await page.goto("/plugins");
  await page.getByRole("link", { name: /documentation/i }).click();
  await expect(page).toHaveURL(/\/documentation\/plugins/);
  await page.goto("/plugins");
  await page.getByRole("button", { name: /register plugin/i }).click();
  await expect(page.getByText(/new extension/i)).toBeVisible();
});

test("responsive navigation remains keyboard and touch reachable", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/evaluation");
  await page.getByRole("button", { name: /open navigation/i }).click();
  await page.getByRole("link", { name: /trace explorer/i }).click();
  await expect(page).toHaveURL(/\/traces/);
  await expect(page.locator("#root")).toContainText("Trace Explorer");
});

test("administrator can inspect workspace identity and access controls", async ({ page }) => {
  await page.goto("/workspace");
  for (const tab of ["Overview", "Members", "Roles", "Service Accounts", "Audit", "Settings"])
    await expect(page.getByRole("button", { name: tab, exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Members", exact: true }).click();
  await expect(page.getByText(adminEmail!)).toBeVisible();
  await page.getByRole("button", { name: "Audit", exact: true }).click();
  await expect(page.locator("main")).toContainText(/Authentication\.Login|audit activity/i);
});

test("API failures show a recoverable error state", async ({ page }) => {
  await page.route("**/api/traces**", route => route.abort("failed"));
  await page.goto("/traces");
  await expect(page.getByText(/failed|could not|request/i).first()).toBeVisible();
});

test("settings overview supports direct nested routes and metadata-driven controls", async ({ page }) => {
  await page.goto("/settings");
  await expect(page.getByRole("main").getByRole("heading", { name: "Settings", exact: true })).toBeVisible();
  await page.locator('.settings-overview-card[href="/settings/evaluation"]').click();
  await expect(page).toHaveURL(/\/settings\/evaluation$/);
  await expect(page.getByRole("heading", { name: "Evaluation", exact: true })).toBeVisible();

  const failureAction = page.locator(".setting-row").filter({ hasText: "Failure Action" });
  await failureAction.getByRole("button", { name: "Edit" }).click();
  await expect(failureAction.getByRole("combobox", { name: /value for failure action/i })).toBeVisible();

  await page.goto("/settings/not-a-section");
  await expect(page).toHaveURL(/\/settings$/);
  await expect(page.getByRole("main").getByRole("heading", { name: "Settings", exact: true })).toBeVisible();
});

test("adaptive workspaces persist desktop panes and expose dismissible mobile drawers", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto("/workflows");
  const definitions = page.locator(".workflow-library");
  const workspace = page.locator(".workflow-workspace");
  const inspector = page.locator(".workflow-inspector");
  await expect(definitions).toBeVisible();
  await expect(workspace).toBeVisible();
  await expect(inspector).toBeVisible();
  const [definitionsBox, workspaceBox, inspectorBox] = await Promise.all([
    definitions.boundingBox(), workspace.boundingBox(), inspector.boundingBox(),
  ]);
  if (!definitionsBox || !workspaceBox || !inspectorBox) throw new Error("Workflow panes must have measurable desktop bounds.");
  expect(definitionsBox.x + definitionsBox.width).toBeLessThanOrEqual(workspaceBox.x + 1);
  expect(workspaceBox.x + workspaceBox.width).toBeLessThanOrEqual(inspectorBox.x + 1);
  expect(inspectorBox.x + inspectorBox.width).toBeLessThanOrEqual(1440);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);

  const hideDefinitions = page.getByRole("button", { name: "Hide Definitions" });
  await expect(hideDefinitions).toBeVisible();
  await hideDefinitions.click();
  await expect(page.locator(".workflow-library")).toBeHidden();
  await page.reload();
  await expect(page.getByRole("button", { name: "Show Definitions" })).toBeVisible();
  await expect(page.locator(".workflow-library")).toBeHidden();

  await page.setViewportSize({ width: 1200, height: 850 });
  await page.goto("/workflows");
  await expect(page.getByRole("button", { name: "Show Inspector" })).toBeVisible();
  await expect(page.locator(".workflow-inspector")).toBeHidden();
  await page.getByRole("button", { name: "Show Inspector" }).click();
  await expect(page.locator(".workflow-inspector")).toBeVisible();
  const compactInspectorBox = await page.locator(".workflow-inspector").boundingBox();
  if (!compactInspectorBox) throw new Error("Workflow Inspector drawer must have measurable compact bounds.");
  expect(compactInspectorBox.x + compactInspectorBox.width).toBeLessThanOrEqual(1200);
  await page.getByRole("button", { name: "Close workspace panel", exact: true }).click();

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/prompts");
  await page.getByRole("button", { name: "Show Prompt library" }).click();
  await expect(page.locator(".prompt-library")).toBeVisible();
  await page.getByRole("button", { name: "Close workspace panel", exact: true }).click();
  await expect(page.locator(".prompt-library")).toBeHidden();
});

test("analytics collapses singleton dimensions and clears stale selections", async ({ page }) => {
  let singleton = false;
  await page.route("**/api/workspaces/*/analytics/filter-options?**", async route => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(singleton ? {
        providers: ["ConvoLab Deterministic"],
        models: ["convolab-deterministic-primary"],
        capabilities: ["Simulation"],
        outcomes: ["Succeeded"],
        prompts: [],
        workflows: [],
        configurationRevisions: [],
        eventTypes: ["simulation.execution.completed"],
      } : {
        providers: ["ConvoLab Deterministic", "Gemini"],
        models: ["convolab-deterministic-primary", "gemini-2.5-flash"],
        capabilities: ["Simulation"],
        outcomes: ["Succeeded"],
        prompts: [],
        workflows: [],
        configurationRevisions: [],
        eventTypes: ["simulation.execution.completed"],
      }),
    });
  });

  await page.goto("/analytics");
  const provider = page.getByLabel("Provider");
  await provider.selectOption("Gemini");
  singleton = true;
  await page.getByLabel("Period").selectOption("7");
  await expect(page.locator(".singleton-context").filter({ hasText: "ConvoLab Deterministic" })).toBeVisible();
  await expect(page.getByLabel("Provider")).toHaveCount(0);
  await expect(
    page.getByLabel("Analytics filters").getByText("simulation.execution.completed"),
  ).toBeVisible();
});

test("dark and light themes keep the premium shell readable", async ({ page }) => {
  await page.goto("/");
  const darkPanel = await page.locator(".hero-panel").evaluate(element => {
    const style = getComputedStyle(element);
    return { background: style.backgroundImage, border: style.borderColor };
  });
  expect(darkPanel.background).not.toBe("none");
  expect(darkPanel.border).not.toBe("rgba(0, 0, 0, 0)");

  const initialTheme = await page.locator("html").getAttribute("data-theme");
  await page.getByRole("button", { name: /Switch to (light|dark) theme/ }).click();
  await expect(page.locator("html")).toHaveAttribute("data-theme", initialTheme === "light" ? "dark" : "light");
  await expect(page.getByRole("button", { name: `Switch to ${initialTheme} theme` })).toBeVisible();
});

test("deterministic provider is explained as a local test provider", async ({ page }) => {
  await page.goto("/conversations");
  await expect(page.getByText("Local test provider.", { exact: true })).toBeVisible();
  await expect(page.getByText(/repeatable rule-based responses with synthetic tokens/i)).toBeVisible();
});
