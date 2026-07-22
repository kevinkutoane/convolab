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
  ["/settings", "Studio Settings"],
  ["/documentation", "ConvoLab documentation"],
] as const;

test("every canonical and compatibility route loads without browser errors", async ({ page }) => {
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
  await expect(page.getByRole("heading", { name: "Immutable baseline", exact: true })).toBeVisible();

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

test("API failures show a recoverable error state", async ({ page }) => {
  await page.route("**/api/traces**", route => route.abort("failed"));
  await page.goto("/traces");
  await expect(page.getByText(/failed|could not|request/i).first()).toBeVisible();
});
