import { expect, test } from "@playwright/test";

const userId = "22222222-2222-4222-8222-222222222222";
const roleId = "11111111-1111-4111-8111-111111111111";
const branchId = "33333333-3333-4333-8333-333333333333";
const currentUser = {
  isAuthenticated: true,
  username: "manager",
  displayName: "Management User",
  preferredLanguage: "en",
  permissions: ["management.access", "users.view", "users.manage"],
  branchAccess: { scope: "assignedBranches", branchIds: [branchId], operationalBranchIds: [branchId] }
};
const user = {
  id: userId,
  username: "employee",
  email: null,
  displayName: "Employee",
  preferredLanguage: "en",
  status: "suspended",
  credentialStatus: "active",
  roleIds: [roleId],
  branchAccessScope: "assignedBranches",
  branchIds: [branchId],
  createdAtUtc: "2026-07-22T00:00:00Z",
  updatedAtUtc: "2026-07-22T00:00:00Z",
  activatedAtUtc: null
};

test("users editor preserves a draft after a stale ETag and supports RTL", async ({ page }) => {
  await page.route("**/api/v1/auth/csrf", route => route.fulfill({ json: { requestToken: "e2e-csrf" } }));
  await page.route("**/api/v1/auth/me", route => route.fulfill({ json: currentUser }));
  await page.route("**/api/v1/management/users/options", route => route.fulfill({ json: {
    roles: [{ id: roleId, name: "Cashier" }], branches: [{ id: branchId, name: "Cairo", code: "CAI-01" }]
  } }));
  await page.route(`**/api/v1/management/users/${userId}`, async route => {
    if (route.request().method() === "GET") {
      await route.fulfill({ json: user, headers: { ETag: "\"7\"" } });
      return;
    }
    expect(route.request().headers()["if-match"]).toBe("\"7\"");
    expect(route.request().headers()["x-xsrf-token"]).toBe("e2e-csrf");
    await route.fulfill({ status: 412, json: { code: "user.concurrency_conflict", currentEtag: "\"8\"" } });
  });

  await page.goto(`/management/users/${userId}`);
  const displayName = page.getByLabel("Display name");
  await displayName.fill("Updated Employee");
  await page.getByRole("button", { name: "Save user" }).click();
  await expect(page.getByRole("heading", { name: "A newer version is available" })).toBeVisible();
  await expect(displayName).toHaveValue("Updated Employee");

  await page.getByRole("button", { name: /AR/ }).click();
  await expect(page.locator(".shell")).toHaveAttribute("dir", "rtl");
  await expect(page.getByRole("heading", { name: "تفاصيل المستخدم" })).toBeVisible();
});

test("users.view guard denies direct navigation without ending the session", async ({ page }) => {
  await page.route("**/api/v1/auth/csrf", route => route.fulfill({ json: { requestToken: "e2e-csrf" } }));
  await page.route("**/api/v1/auth/me", route => route.fulfill({ json: {
    ...currentUser,
    permissions: ["management.access", "users.manage"]
  } }));

  await page.goto("/management/users");
  await expect(page).toHaveURL(/\/management\/access-denied/);
  await expect(page.getByRole("heading", { name: "Access denied" })).toBeVisible();
});
