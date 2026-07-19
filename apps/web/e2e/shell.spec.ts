import { expect, test } from "@playwright/test";

test("shell switches between English and Arabic direction", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByRole("heading", { name: "Kalm Cafe" })).toBeVisible();
  await expect(page.locator(".shell")).toHaveAttribute("lang", "en");
  await expect(page.locator(".shell")).toHaveAttribute("dir", "ltr");

  await page.getByRole("button", { name: "AR" }).click();

  await expect(page.getByRole("heading", { name: "كالم كافيه" })).toBeVisible();
  await expect(page.locator(".shell")).toHaveAttribute("lang", "ar");
  await expect(page.locator(".shell")).toHaveAttribute("dir", "rtl");
});
