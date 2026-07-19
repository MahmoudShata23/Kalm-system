import { expect, test } from "@playwright/test";

test("shell switches between English and Arabic direction", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByRole("heading", { name: "Kalm Cafe" })).toBeVisible();
  await expect(page.locator(".shell")).toHaveAttribute("lang", "en");
  await expect(page.locator(".shell")).toHaveAttribute("dir", "ltr");

  await page.getByRole("button", { name: /AR/ }).click();

  await expect(page.getByRole("heading", { name: "كالم كافيه" })).toBeVisible();
  await expect(page.locator(".shell")).toHaveAttribute("lang", "ar");
  await expect(page.locator(".shell")).toHaveAttribute("dir", "rtl");

  await page.getByRole("button", { name: /EN/ }).click();
  await expect(page.locator(".shell")).toHaveAttribute("dir", "ltr");
});

test.describe("PrimeNG test-only accessibility fixture", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto("/__e2e/primeng-accessibility");
    await expect(page.getByTestId("primeng-accessibility-fixture")).toBeVisible();
  });

  test("Select supports an accessible keyboard-only selection", async ({ page }) => {
    const select = page.getByRole("combobox", { name: "Fixture state" });

    await expect(select).toHaveAttribute("aria-expanded", "false");
    await select.focus();
    await page.keyboard.press("Enter");
    await expect(select).toHaveAttribute("aria-expanded", "true");
    await expect(page.getByRole("listbox")).toBeVisible();

    await page.keyboard.press("ArrowDown");
    await page.keyboard.press("Enter");

    await expect(select).toHaveAttribute("aria-expanded", "false");
    await expect(select).toContainText("Pending");
  });

  test("Tabs expose roles and activate with arrow keys", async ({ page }) => {
    const tablist = page.getByRole("tablist", { name: "Fixture sections" });
    const overview = tablist.getByRole("tab", { name: "Overview" });
    const details = tablist.getByRole("tab", { name: "Details" });

    await expect(overview).toHaveAttribute("aria-selected", "true");
    await expect(page.getByRole("tabpanel", { name: "Overview" })).toBeVisible();

    await overview.focus();
    await page.keyboard.press("ArrowRight");

    await expect(details).toBeFocused();
    await expect(details).toHaveAttribute("aria-selected", "false");
    await page.keyboard.press("Enter");
    await expect(details).toHaveAttribute("aria-selected", "true");
    await expect(page.getByRole("tabpanel", { name: "Details" })).toBeVisible();
  });

  test("Table exposes an accessible name, headers, and a reachable row action", async ({ page }) => {
    const table = page.getByRole("table", { name: "PrimeNG component status" });
    const rowAction = table.getByRole("button", { name: "Inspect row" });

    await expect(table).toBeVisible();
    await expect(table.getByRole("columnheader", { name: "Toolkit" })).toHaveAttribute("scope", "col");
    await expect(table.getByRole("columnheader", { name: "Action" })).toHaveAttribute("scope", "col");

    await rowAction.focus();
    await expect(rowAction).toBeFocused();
  });

  test("Toast announces assertively without moving focus", async ({ page }) => {
    const trigger = page.getByRole("button", { name: "Show toast" });

    await trigger.focus();
    await page.keyboard.press("Enter");

    const alert = page.getByRole("alert");
    await expect(alert).toHaveAttribute("aria-live", "assertive");
    await expect(alert).toContainText("Toast announcement");
    await expect(trigger).toBeFocused();
  });

  test("Dialog traps focus, closes with Escape, and restores trigger focus", async ({ page }) => {
    const trigger = page.getByRole("button", { name: "Open dialog" });
    await trigger.focus();
    await page.keyboard.press("Enter");

    const dialog = page.getByRole("dialog", { name: "Overlay verification" });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByRole("button", { name: "Dialog action" })).toBeFocused();

    for (const key of ["Tab", "Tab", "Shift+Tab"]) {
      await page.keyboard.press(key);
      await expect.poll(() => page.evaluate(() => document.activeElement?.closest('[role="dialog"]') !== null))
        .toBe(true);
    }

    await page.keyboard.press("Escape");
    await expect(dialog).toBeHidden();
    await expect(trigger).toBeFocused();
  });

  test("ConfirmDialog has a predictable keyboard flow and restores focus", async ({ page }) => {
    const trigger = page.getByRole("button", { name: "Open confirmation" });
    await trigger.focus();
    await page.keyboard.press("Enter");

    const confirmation = page.getByRole("alertdialog", { name: "Confirm accessibility action" });
    const reject = confirmation.getByRole("button", { name: "Reject" });
    const accept = confirmation.getByRole("button", { name: "Accept" });

    await expect(confirmation).toBeVisible();
    await expect(reject).toBeFocused();
    await page.keyboard.press("Tab");
    await expect(accept).toBeFocused();
    await page.keyboard.press("Shift+Tab");
    await expect(reject).toBeFocused();
    await page.keyboard.press("Enter");

    await expect(confirmation).toBeHidden();
    await expect(trigger).toBeFocused();
  });

  test("Kalm focus ring is visibly applied to a PrimeNG control", async ({ page }) => {
    const trigger = page.getByRole("button", { name: "Open dialog" });

    await trigger.focus();
    await expect(trigger).toBeFocused();
    await expect(trigger).toHaveCSS("outline-style", "solid");
    await expect(trigger).toHaveCSS("outline-width", "3px");
    await expect.poll(() => page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue("--p-focus-ring-width").trim()
    )).toBe("3px");
  });

  test("Dialog remains keyboard usable in Arabic RTL and after returning to English LTR", async ({ page }) => {
    await page.getByRole("button", { name: /AR/ }).click();
    await expect(page.locator(".shell")).toHaveAttribute("dir", "rtl");

    const trigger = page.getByRole("button", { name: "Open dialog" });
    await trigger.focus();
    await page.keyboard.press("Enter");
    const dialog = page.getByRole("dialog", { name: "Overlay verification" });
    await expect(dialog).toHaveCSS("direction", "rtl");
    await expect(dialog.getByText("التحقق من النافذة")).toBeVisible();
    const rtlDialogBox = await dialog.boundingBox();
    const rtlCloseBox = await dialog.getByRole("button", { name: "Close overlay verification" }).boundingBox();
    expect(rtlDialogBox).not.toBeNull();
    expect(rtlCloseBox).not.toBeNull();
    expect(rtlCloseBox!.x).toBeLessThan(rtlDialogBox!.x + rtlDialogBox!.width / 2);
    await page.keyboard.press("Escape");
    await expect(trigger).toBeFocused();

    await page.getByRole("button", { name: /EN/ }).click();
    await expect(page.locator(".shell")).toHaveAttribute("dir", "ltr");
    await trigger.focus();
    await page.keyboard.press("Enter");
    await expect(dialog).toHaveCSS("direction", "ltr");
    await expect(dialog.getByText("Dialog keyboard verification")).toBeVisible();
    const ltrDialogBox = await dialog.boundingBox();
    const ltrCloseBox = await dialog.getByRole("button", { name: "Close overlay verification" }).boundingBox();
    expect(ltrDialogBox).not.toBeNull();
    expect(ltrCloseBox).not.toBeNull();
    expect(ltrCloseBox!.x).toBeGreaterThan(ltrDialogBox!.x + ltrDialogBox!.width / 2);
    await page.keyboard.press("Escape");
    await expect(trigger).toBeFocused();
  });
});
