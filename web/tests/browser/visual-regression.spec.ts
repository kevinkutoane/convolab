import { expect, test } from "@playwright/test";

const views = [
  { name: "dashboard", route: "/", ready: ".hero-panel" },
  { name: "workflow", route: "/workflows", ready: ".workflow-studio-layout" },
  { name: "settings", route: "/settings", ready: ".settings-page" },
  { name: "analytics", route: "/analytics", ready: ".analytics-page" },
  { name: "policy", route: "/policies", ready: ".policy-center-page" },
  { name: "workspace", route: "/workspace", ready: ".workspace-admin-page" },
] as const;

test.beforeEach(async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.route("**/api/workflows", async route => {
    const request = route.request();
    if (request.method() === "GET" && new URL(request.url()).pathname === "/api/workflows") {
      await route.fulfill({ status: 200, contentType: "application/json", body: "[]" });
      return;
    }
    await route.continue();
  });
  await page.route("**/api/platform/status", route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        platformName: "ConvoLab Platform",
        productName: "ConvoLab Studio",
        version: "1.0.0-alpha.17",
        workstream: "development",
        safeMode: false,
        environment: "Development",
        architectureHealth: "Hardened Alpha",
        apiHealth: "Responding",
        capabilities: [
          { id: "conversation", name: "Conversation Engine", description: "Lifecycle, sessions, participants, memory, and timeline.", status: "stable", version: "1.0", domainEvents: 16 },
          { id: "workflow", name: "Workflow Engine", description: "Versioned workflow definitions and governed executions.", status: "stable", version: "1.0", domainEvents: 12 },
          { id: "prompt", name: "Prompt Engine", description: "Governed prompt assets, versions, rendering, and experiments.", status: "stable", version: "1.0", domainEvents: 10 },
          { id: "knowledge", name: "Knowledge Engine", description: "Governed retrieval, packages, citations, and connectors.", status: "stable", version: "1.0", domainEvents: 13 },
          { id: "intelligence", name: "Intelligence Engine", description: "Provider-neutral planning, budgets, tools, and fallback.", status: "stable", version: "1.0", domainEvents: 14 },
          { id: "policy", name: "Policy", description: "Versioned governance, scoped runtime decisions, and enforced execution constraints.", status: "stable", version: "1.0", domainEvents: 8 },
          { id: "evaluation", name: "Evaluation", description: "Persisted scorecards, quality gates, safety, relevance, and groundedness telemetry.", status: "stable", version: "1.0", domainEvents: 5 },
          { id: "tracing", name: "Tracing", description: "Persisted traces, spans, events, correlations, and redacted artifacts.", status: "stable", version: "1.0", domainEvents: 7 },
          { id: "replay", name: "Replay Studio", description: "Controlled re-execution, immutable baselines, candidate comparisons, and findings.", status: "stable", version: "1.0", domainEvents: 3 },
          { id: "plugins", name: "Plugin Engine", description: "Persistent extension registry, immutable versions, compatibility, lifecycle, health, and capability contracts.", status: "stable", version: "1.0", domainEvents: 4 },
          { id: "workspace-identity", name: "Workspace, Identity and Access", description: "Secure local authentication, workspace isolation, RBAC, service identities, and attributable audit.", status: "active", version: "1.0", domainEvents: 8 },
          { id: "analytics", name: "Platform Analytics", description: "Trusted workspace and environment usage, cost, quality, governance, performance, adoption, and safe event evidence.", status: "active", version: "1.0", domainEvents: 12 },
          { id: "studio", name: "ConvoLab Studio", description: "Functional engineering workspace with simulation, governance, analytics, evaluation, trace inspection, replay, plugin governance, and workspace isolation.", status: "active", version: "0.14", domainEvents: 0 }
        ],
        generatedAt: "2026-09-02T12:00:00Z",
        source: "api"
      })
    })
  );
  await page.route("**/api/workspaces/*/analytics/filter-options?**", route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        providers: [],
        models: [],
        capabilities: [],
        outcomes: [],
        prompts: [],
        workflows: [],
        knowledgeCollections: [],
        configurationRevisions: [],
        eventTypes: [],
        costTypes: [],
      }),
    }),
  );
  await page.route("**/api/workspaces/*/analytics/overview?**", route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        category: "overview",
        scope: {
          workspaceId: "00000000-0000-0000-0000-000000000001",
          from: "2026-07-01T00:00:00Z",
          to: "2026-07-30T00:00:00Z",
          granularity: "day",
          filters: {},
        },
        metrics: [{ key: "executionCount", label: "Executions", value: 0, unit: "count" }],
        series: [],
        indicators: [],
        isPartial: false,
        generatedAt: "2026-07-30T00:00:00Z",
      }),
    }),
  );
  await page.goto("/");
  await expect(page).toHaveURL(/\/$/);
});

for (const theme of ["dark", "light"] as const) {
  test(`representative Studio surfaces match the ${theme} visual baseline`, async ({ page }) => {
    test.setTimeout(120_000);
    await page.evaluate(value => localStorage.setItem("convolab-theme", value), theme);

    for (const view of views) {
      await page.goto(view.route);
      await expect(page.locator(view.ready)).toBeVisible();
      await expect(page.locator(".async-loading")).toHaveCount(0);
      await expect(page.locator("html")).toHaveAttribute("data-theme", theme);
      await expect(page).toHaveScreenshot(`${view.name}-${theme}.png`, {
        animations: "disabled",
        caret: "hide",
        mask: [page.locator(".statusbar")],
        maxDiffPixelRatio: 0.02,
      });
    }
  });
}
