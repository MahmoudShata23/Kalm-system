import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "e2e",
  timeout: 30_000,
  expect: {
    timeout: 5_000
  },
  use: { trace: "on-first-retry" },
  projects: [
    {
      name: "chromium-accessibility",
      testMatch: ["shell.spec.ts", "roles.spec.ts", "users.spec.ts"],
      use: {
        ...devices["Desktop Chrome"],
        baseURL: process.env["PLAYWRIGHT_BASE_URL"] ?? "http://127.0.0.1:4200"
      }
    },
    {
      name: "chromium-production",
      testMatch: "production.spec.ts",
      use: {
        ...devices["Desktop Chrome"],
        baseURL: "http://127.0.0.1:4201"
      }
    }
  ],
  webServer: [
    {
      command: process.platform === "win32" ? "npm.cmd run start:e2e" : "npm run start:e2e",
      url: "http://127.0.0.1:4200",
      reuseExistingServer: false,
      timeout: 120_000
    },
    {
      command: process.platform === "win32" ? "npm.cmd run start:e2e:production" : "npm run start:e2e:production",
      url: "http://127.0.0.1:4201",
      reuseExistingServer: false,
      timeout: 120_000
    }
  ]
});
