import { expect, test, type APIRequestContext, type Page } from "@playwright/test";

async function operationalStatus(request: APIRequestContext) {
  const response = await request.get("/api/operations/status");
  expect(response.ok()).toBeTruthy();
  return await response.json() as {
    safeMode: { persistedSafeModeEnabled: boolean; revision: number };
  };
}

async function mutateSafeMode(
  request: APIRequestContext,
  enabled: boolean,
  expectedRevision: number,
  reason: string,
) {
  const antiforgery = await request.get("/api/auth/antiforgery");
  const { token } = await antiforgery.json() as { token: string };
  return await request.post("/api/operations/safe-mode", {
    headers: { "X-XSRF-TOKEN": token },
    data: {
      enabled,
      expectedRevision,
      reason,
      confirmation: enabled ? "ACTIVATE SAFE MODE" : "DEACTIVATE SAFE MODE",
    },
  });
}

async function ensureSafeModeDisabled(page: Page) {
  const current = await operationalStatus(page.request);
  if (!current.safeMode.persistedSafeModeEnabled) return;
  const response = await mutateSafeMode(
    page.request,
    false,
    current.safeMode.revision,
    "Operations browser acceptance cleanup",
  );
  expect(response.ok()).toBeTruthy();
}

test.beforeEach(async ({ page }) => {
  await ensureSafeModeDisabled(page);
});

test.afterEach(async ({ page }) => {
  await ensureSafeModeDisabled(page);
});

test("platform administrator can navigate every Operations Center panel", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("link", { name: /Operations/ })).toBeVisible();
  await page.getByRole("link", { name: /Operations/ }).click();
  await expect(page).toHaveURL(/\/operations$/);
  await expect(page.getByRole("heading", { name: "Operations Center" })).toBeVisible();
  await expect(page.getByText("Worker State", { exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Readiness Evidence" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Analytics Maintenance Pipeline" })).toBeVisible();

  const analytics = page.locator("article.panel", { hasText: "Analytics Maintenance Pipeline" });
  await expect(analytics.getByText("Pending Outbox Records", { exact: true })).toBeVisible();
  await expect(analytics.getByText("Failed Outbox Records", { exact: true })).toBeVisible();
  await expect(analytics.getByText("Aggregation Lag", { exact: true })).toBeVisible();

  await page.getByRole("button", { name: "Deployments & Releases" }).click();
  await expect(page.getByRole("heading", { name: "Active Environment State & Promotion Pipeline" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Deployment History & Evidence Records" })).toBeVisible();

  await page.getByRole("button", { name: "Backup & DR" }).click();
  await expect(page.getByRole("heading", { name: "Backup & Recovery Orchestration" })).toBeVisible();
  const backups = page.locator("section.panel", { hasText: "Live Backup Evidence" });
  await expect(backups.getByRole("heading", { name: "Live Backup Evidence" })).toBeVisible();
  await expect(backups).toContainText(/NotConfigured|Configured|Unavailable|Degraded|LiveValidated/);
  await expect(backups).not.toContainText(/database dump|secret key|password/i);

  await page.getByRole("button", { name: "Authentication & IAM" }).click();
  const authentication = page.locator("section.panel", { hasText: "Authentication & Break-Glass Evidence" });
  await expect(authentication.getByRole("heading", { name: "Authentication & Break-Glass Evidence" })).toBeVisible();
  for (const label of [
    "Authentication Mode",
    "Local Login Enabled",
    "Entra SSO Enabled",
    "Active Application Sessions",
    "Linked Active Users",
    "External Identities",
    "Break-Glass Status",
    "Break-Glass Uses (24h)",
    "Last Break-Glass Success",
  ])
    await expect(authentication.getByText(label, { exact: true })).toBeVisible();
  await expect(authentication).not.toContainText(/tenant id|authority|secret reference|account|email|credential|hash|password|subject|failed at/i);

  await page.getByRole("button", { name: "Telemetry & Secrets" }).click();
  const secrets = page.locator("section.panel", { hasText: "Configuration Secrets" });
  await expect(secrets.getByRole("heading", { name: "Configuration Secrets" })).toBeVisible();
  const telemetry = page.locator("section.panel", { hasText: "Telemetry Collectors" });
  await expect(telemetry.getByRole("heading", { name: "Telemetry Collectors" })).toBeVisible();
  await expect(telemetry.getByText("OTLP Exporter State", { exact: true })).toBeVisible();
  await expect(telemetry.getByText("Endpoint Configured", { exact: true })).toBeVisible();
  await expect(telemetry).not.toContainText(/Authorization|header|credential/i);

  await page.getByRole("button", { name: "Build & Manifest" }).click();
  await expect(page.getByRole("heading", { name: "Build & System Information" })).toBeVisible();
});

test("workspace roles do not receive Operations navigation or route access", async ({ page }) => {
  await page.route("**/api/auth/session", route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({
      userId: "50000000-0000-0000-0000-000000000001",
      email: "engineer@convolab.test",
      displayName: "Workspace Engineer",
      isPlatformAdministrator: false,
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
      activeWorkspaceId: "20000000-0000-0000-0000-000000000001",
      workspaces: [{
        id: "20000000-0000-0000-0000-000000000001",
        organisationId: "10000000-0000-0000-0000-000000000001",
        name: "Default Workspace",
        role: "Engineer",
      }],
    }),
  }));
  await page.goto("/operations");
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByRole("link", { name: /Operations/ })).toHaveCount(0);
});

test("dependency-state labels remain distinct and stub evidence is not live", async ({ page }) => {
  await page.route("**/api/operations/secret-providers", route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({
      providers: [
        { provider: "environment", state: "Configured" },
        { provider: "stub", state: "StubValidated" },
        { provider: "live", state: "LiveValidated" },
        { provider: "missing", state: "NotConfigured" },
        { provider: "offline", state: "Unavailable" },
        { provider: "slow", state: "Degraded" },
      ],
      requiredEnvironments: [],
    }),
  }));
  await page.goto("/operations");
  await page.getByRole("button", { name: "Telemetry & Secrets" }).click();
  const secrets = page.locator("section.panel", { hasText: "Configuration Secrets" });
  await expect(secrets.getByRole("heading", { name: "Configuration Secrets" })).toBeVisible();
  for (const state of [
    "Configured",
    "StubValidated",
    "LiveValidated",
    "NotConfigured",
    "Unavailable",
    "Degraded",
  ])
    await expect(secrets.getByText(state, { exact: true })).toBeVisible();
  const stub = secrets.locator('[data-dependency="stub"]');
  await expect(stub.getByText("StubValidated", { exact: true })).toBeVisible();
  await expect(stub.getByText("LiveValidated", { exact: true })).toHaveCount(0);
});

test("safe-mode mutation refreshes the global banner and rejects stale revisions", async ({ page }) => {
  await page.goto("/operations");
  const initial = await operationalStatus(page.request);
  await page.getByLabel("Reason").fill("Operations browser safe-mode acceptance");
  await page.getByLabel(/Type ACTIVATE SAFE MODE/).fill("ACTIVATE SAFE MODE");
  await page.getByRole("button", { name: "Activate safe mode" }).click();
  await expect(page.getByRole("alert")).toContainText("Platform safe mode is active");

  const conflict = await mutateSafeMode(
    page.request,
    true,
    initial.safeMode.revision,
    "Stale administrator browser acceptance",
  );
  expect(conflict.status()).toBe(409);
  const problem = await conflict.json() as { code: string };
  expect(problem.code).toBe("revision.conflict");

  await page.getByLabel("Reason").fill("Operations browser safe-mode acceptance complete");
  await page.getByLabel(/Type DEACTIVATE SAFE MODE/).fill("DEACTIVATE SAFE MODE");
  await page.getByRole("button", { name: "Deactivate safe mode" }).click();
  await expect(page.getByRole("alert")).toHaveCount(0);
});

test("safe-mode banner refreshes across browser sessions and preserves last-known active state", async ({ page, context }) => {
  const administrator = await context.newPage();
  await page.goto("/");
  await administrator.goto("/operations");
  const current = await operationalStatus(administrator.request);
  const activated = await mutateSafeMode(
    administrator.request,
    true,
    current.safeMode.revision,
    "Cross-session safe-mode browser acceptance",
  );
  expect(activated.ok()).toBeTruthy();

  await page.bringToFront();
  await page.evaluate(() => window.dispatchEvent(new Event("focus")));
  await expect(page.getByRole("alert")).toContainText("Platform safe mode is active");
  await page.route("**/api/platform/status", route => route.abort("failed"));
  await page.evaluate(() => window.dispatchEvent(new Event("focus")));
  await expect(page.getByRole("alert")).toContainText("last known state");
  await page.unroute("**/api/platform/status");

  const active = await operationalStatus(administrator.request);
  const deactivated = await mutateSafeMode(
    administrator.request,
    false,
    active.safeMode.revision,
    "Cross-session safe-mode browser cleanup",
  );
  expect(deactivated.ok()).toBeTruthy();
  await administrator.close();
});

for (const viewport of [
  { name: "mobile", width: 390, height: 844 },
  { name: "tablet", width: 820, height: 1180 },
  { name: "desktop", width: 1440, height: 1000 },
]) {
  test(`Operations Center remains usable at ${viewport.name} width`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await page.goto("/operations");
    await expect(page.getByRole("heading", { name: "Operations Center" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Safe mode" })).toBeVisible();
  });
}
