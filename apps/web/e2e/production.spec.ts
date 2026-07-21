import { expect, test } from "@playwright/test";

test("production exposes only the real login route and no test or bypass routes", async ({ page }) => {
  await page.goto("/foundation/primeng-showcase");

  await expect(page.getByRole("heading", { name: "Welcome back" })).toBeVisible();
  await expect(page.getByText("PrimeNG component verification")).toHaveCount(0);
  await expect(page.getByTestId("primeng-accessibility-fixture")).toHaveCount(0);

  await page.goto("/__e2e/primeng-accessibility");

  await expect(page.getByRole("heading", { name: "Welcome back" })).toBeVisible();
  await expect(page.getByTestId("primeng-accessibility-fixture")).toHaveCount(0);

  for (const path of [
    "/__test/clock",
    "/api/v1/bootstrap",
    "/api/v1/auth/provision-first-administrator",
    "/api/v1/roles",
    "/api/v1/permissions",
    "/api/v1/users",
    "/api/v1/branches",
    "/auth/bypass",
    "/test-credentials"
  ]) {
    await page.goto(path);
    await expect(page.getByRole("heading", { name: "Welcome back" })).toBeVisible();
  }
});
