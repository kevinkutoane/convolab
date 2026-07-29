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
