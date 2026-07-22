import { expect, test } from "@playwright/test";

const branchId = "11111111-1111-4111-8111-111111111111";
const currentUser = {
  isAuthenticated: true,
  username: "manager",
  displayName: "Management User",
  preferredLanguage: "en",
  permissions: ["management.access", "branches.view", "branches.manage"],
  branchAccess: { scope: "allOrganizationBranches", branchIds: [], operationalBranchIds: [branchId] }
};
const branch = {
  id: branchId, name: "Cairo", code: "CAI", localeCode: "en", timeZoneId: "Africa/Cairo",
  businessDayRollover: "04:00", status: "active", createdAtUtc: "2026-07-22T00:00:00Z", updatedAtUtc: "2026-07-22T00:00:00Z"
};

test("branch list and editor preserve the draft and present safe dependency conflicts in LTR and RTL", async ({ page }) => {
  await page.route("**/api/v1/auth/csrf", route => route.fulfill({ json: { requestToken: "e2e-csrf" } }));
  await page.route("**/api/v1/auth/me", route => route.fulfill({ json: currentUser }));
  await page.route(/\/api\/v1\/management\/branches\?.*/, route => route.fulfill({ json: {
    items: [branch], page: 1, pageSize: 25, totalCount: 1
  } }));
  await page.route(`**/api/v1/management/branches/${branchId}`, async route => {
    if (route.request().method() === "GET") {
      await route.fulfill({ json: branch, headers: { ETag: '"3"' } });
      return;
    }
    expect(route.request().headers()["if-match"]).toBe('"3"');
    expect(route.request().headers()["x-xsrf-token"]).toBe("e2e-csrf");
    await route.fulfill({ status: 412, json: { code: "branch.concurrency_conflict", currentEtag: '"4"' } });
  });
  await page.route(`**/api/v1/management/branches/${branchId}/deactivate`, async route => {
    expect(route.request().headers()["if-match"]).toBe('"3"');
    expect(route.request().headers()["x-xsrf-token"]).toBe("e2e-csrf");
    await route.fulfill({ status: 409, json: { code: "branch.dependencies_active", dependencyCounts: {
      registeredDeviceCount: 1, activeDeviceCount: 0, activeCredentialCount: 0, activeSessionCount: 0, activeUserAssignmentCount: 2
    } } });
  });

  await page.goto("/management/branches");
  await expect(page.getByRole("heading", { name: "Branches" })).toBeVisible();
  await expect(page.getByRole("cell", { name: "Cairo", exact: true })).toBeVisible();
  await page.getByRole("link", { name: "Open" }).click();
  const name = page.getByLabel("Branch name");
  await name.fill("Cairo Draft");
  await page.getByRole("button", { name: "Save branch" }).click();
  await expect(page.getByRole("heading", { name: "A newer branch version is available." })).toBeVisible();
  await expect(name).toHaveValue("Cairo Draft");
  await page.getByRole("button", { name: "Load latest version and keep my draft" }).click();
  await expect(name).toHaveValue("Cairo Draft");

  await page.getByRole("button", { name: "Deactivate branch" }).click();
  await page.getByRole("button", { name: "Confirm" }).click();
  await expect(page.getByRole("heading", { name: "Deactivation is blocked" })).toBeVisible();
  await expect(page.getByText("Explicit user assignments: 2")).toBeVisible();

  await page.getByRole("button", { name: /AR/ }).click();
  await expect(page.locator(".shell")).toHaveAttribute("dir", "rtl");
  await expect(page.getByLabel("اسم الفرع")).toHaveValue("Cairo Draft");
});

test("branch route ordering opens new before the parameter route and view permission guards navigation", async ({ page }) => {
  await page.route("**/api/v1/auth/csrf", route => route.fulfill({ json: { requestToken: "e2e-csrf" } }));
  await page.route("**/api/v1/auth/me", route => route.fulfill({ json: currentUser }));
  let unexpectedGet = false;
  await page.route("**/api/v1/management/branches/new", route => { unexpectedGet = true; return route.abort(); });
  await page.goto("/management/branches/new");
  await expect(page.getByRole("heading", { name: "Create branch" })).toBeVisible();
  expect(unexpectedGet).toBe(false);

  await page.unroute("**/api/v1/auth/me");
  await page.route("**/api/v1/auth/me", route => route.fulfill({ json: { ...currentUser, permissions: ["management.access"] } }));
  await page.goto("/management/branches");
  await expect(page).toHaveURL(/\/management\/access-denied/);
  await expect(page.getByRole("heading", { name: "Access denied" })).toBeVisible();
});
