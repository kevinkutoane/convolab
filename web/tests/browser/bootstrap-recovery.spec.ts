import { expect, test } from "@playwright/test";

test("session bootstrap recovers from a transient API failure without a page refresh", async ({ page }) => {
  let sessionAttempts = 0;
  let documentRequests = 0;

  page.on("request", request => {
    if (request.isNavigationRequest()) documentRequests += 1;
  });
  await page.route("**/api/auth/session", async route => {
    sessionAttempts += 1;
    if (sessionAttempts === 1) {
      await route.fulfill({
        status: 503,
        contentType: "application/problem+json",
        body: JSON.stringify({ detail: "API startup is still completing." }),
      });
      return;
    }
    await route.continue();
  });

  await page.goto("/");

  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole("heading", { name: "Welcome back" })).toBeVisible();
  expect(sessionAttempts).toBe(2);
  expect(documentRequests).toBe(1);
});
