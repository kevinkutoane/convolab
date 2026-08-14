import { defineConfig, devices } from "@playwright/test";
import { acceptanceAuthState } from "./tests/browser/global-setup";

export default defineConfig({
  testDir: "./tests/browser",
  globalSetup: "./tests/browser/global-setup.ts",
  timeout: 30_000,
  expect: { timeout: 8_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: process.env.CI ? [["line"], ["html", { open: "never" }]] : "line",
  snapshotPathTemplate: "{testDir}/visual-baselines/{arg}{ext}",
  use: {
    baseURL: process.env.CONVOLAB_BROWSER_BASE_URL ?? "http://127.0.0.1:3000",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    storageState: acceptanceAuthState,
    ...devices["Desktop Chrome"],
  },
});
