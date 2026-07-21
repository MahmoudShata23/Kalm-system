import { expect, test } from "@playwright/test";

const roleId = "11111111-1111-4111-8111-111111111111";
const currentUser = {
  isAuthenticated: true,
  username: "manager",
  displayName: "Management User",
  preferredLanguage: "en",
  permissions: ["management.access", "roles.manage"],
  branchAccess: { scope: "assignedBranches", branchIds: ["branch-1"], operationalBranchIds: ["branch-1"] }
};
const role = {
  id: roleId,
  name: "Manager",
  status: "active",
  isProtectedSystemRole: false,
  activeAssignmentCount: 0,
  permissionCodes: ["management.access", "roles.manage"],
  createdAtUtc: "2026-07-21T00:00:00Z",
  updatedAtUtc: "2026-07-21T00:00:00Z",
  archivedAtUtc: null
};
const permissions = {
  catalogueVersion: "2026.07.slice4.v1",
  permissions: [
    {
      code: "management.access", groupCode: "management", groupOrder: 1, itemOrder: 1,
      englishLabel: "Management access", englishDescription: "Enter and use the protected management area.",
      arabicLabel: "دخول الإدارة", arabicDescription: "الدخول إلى منطقة الإدارة المحمية واستخدامها."
    },
    {
      code: "roles.manage", groupCode: "usersRoles", groupOrder: 2, itemOrder: 3,
      englishLabel: "Manage roles", englishDescription: "Create roles and manage their permission sets.",
      arabicLabel: "إدارة الأدوار", arabicDescription: "إنشاء الأدوار وإدارة مجموعات صلاحياتها."
    }
  ]
};

test("roles list and editor preserve a draft when an ETag becomes stale", async ({ page }) => {
  await page.route("**/api/v1/auth/csrf", route => route.fulfill({ json: { requestToken: "e2e-csrf" } }));
  await page.route("**/api/v1/auth/me", route => route.fulfill({ json: currentUser }));
  await page.route("**/api/v1/management/permissions", route => route.fulfill({ json: permissions }));
  await page.route(/\/api\/v1\/management\/roles\?.*/, route => route.fulfill({ json: {
    items: [{ ...role, permissionCount: 2, updatedAtUtc: role.updatedAtUtc }], page: 1, pageSize: 25, totalCount: 1
  } }));
  await page.route(`**/api/v1/management/roles/${roleId}`, async route => {
    if (route.request().method() === "GET") {
      await route.fulfill({ json: role, headers: { ETag: "\"3\"" } });
      return;
    }
    expect(route.request().headers()["if-match"]).toBe("\"3\"");
    expect(route.request().headers()["x-xsrf-token"]).toBe("e2e-csrf");
    await route.fulfill({ status: 412, json: { code: "role.concurrency_conflict", currentEtag: "\"4\"" } });
  });

  await page.goto("/management/roles");
  await expect(page.getByRole("heading", { name: "Roles" })).toBeVisible();
  await expect(page.getByRole("cell", { name: "Manager" })).toBeVisible();
  await page.getByRole("link", { name: "Edit" }).click();
  const name = page.getByLabel("Role name");
  await name.fill("Updated Manager");
  await expect(name).toHaveValue("Updated Manager");
  const staleResponse = page.waitForResponse(response =>
    response.request().method() === "PUT" && response.url().endsWith(`/api/v1/management/roles/${roleId}`));
  await page.getByRole("button", { name: "Save role" }).click();
  expect((await staleResponse).status()).toBe(412);

  await expect(page.getByRole("heading", { name: "A newer version is available" })).toBeVisible();
  await expect(name).toHaveValue("Updated Manager");
  await page.getByRole("button", { name: /AR/ }).click();
  await expect(page.locator(".shell")).toHaveAttribute("dir", "rtl");
  await expect(page.getByText("إدارة الأدوار", { exact: true }).first()).toBeVisible();
});

test("roles.manage guard denies direct navigation while keeping the authenticated session", async ({ page }) => {
  await page.route("**/api/v1/auth/csrf", route => route.fulfill({ json: { requestToken: "e2e-csrf" } }));
  await page.route("**/api/v1/auth/me", route => route.fulfill({ json: {
    ...currentUser,
    permissions: ["management.access"]
  } }));

  await page.goto("/management/roles");

  await expect(page).toHaveURL(/\/management\/access-denied/);
  await expect(page.getByRole("heading", { name: "Access denied" })).toBeVisible();
});
