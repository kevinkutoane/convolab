import { chromium, type FullConfig } from "@playwright/test";
import { mkdir } from "node:fs/promises";
import { dirname, resolve } from "node:path";

export const acceptanceAuthState = resolve("test-results/.auth/admin.json");

export default async function globalSetup(config: FullConfig) {
  const email = process.env.CONVOLAB_ACCEPTANCE_ADMIN_EMAIL ?? process.env.CONVOLAB_BOOTSTRAP_ADMIN_EMAIL;
  const password = process.env.CONVOLAB_ACCEPTANCE_ADMIN_PASSWORD ?? process.env.CONVOLAB_BOOTSTRAP_ADMIN_PASSWORD;
  if (!email || !password) throw new Error("Browser acceptance requires the configured bootstrap administrator credentials.");

  const baseURL = config.projects[0]?.use.baseURL;
  if (typeof baseURL !== "string") throw new Error("Browser acceptance requires a string baseURL.");

  await mkdir(dirname(acceptanceAuthState), { recursive: true });
  const browser = await chromium.launch();
  const context = await browser.newContext({ baseURL });
  const page = await context.newPage();
  await page.goto("/login");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: /sign in/i }).click();
  await page.waitForURL(/\/$/);
  await context.storageState({ path: acceptanceAuthState });
  await browser.close();
}
