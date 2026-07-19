import { expect, test } from "@playwright/test";

test("production exposes neither the removed showcase nor the test-only fixture", async ({ page }) => {
  await page.goto("/foundation/primeng-showcase");

  await expect(page.getByRole("heading", { name: "Kalm Cafe" })).toBeVisible();
  await expect(page.getByText("PrimeNG component verification")).toHaveCount(0);
  await expect(page.getByTestId("primeng-accessibility-fixture")).toHaveCount(0);

  await page.goto("/__e2e/primeng-accessibility");

  await expect(page.getByRole("heading", { name: "Kalm Cafe" })).toBeVisible();
  await expect(page.getByTestId("primeng-accessibility-fixture")).toHaveCount(0);
});
