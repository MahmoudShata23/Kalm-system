import { expect, test } from "@playwright/test";

const auditId = "11111111-1111-4111-8111-111111111111";
const branchId = "22222222-2222-4222-8222-222222222222";
const actorId = "33333333-3333-4333-8333-333333333333";
const currentUser = { isAuthenticated: true, username: "manager", displayName: "Management User", preferredLanguage: "en", permissions: ["management.access", "audit.view"], branchAccess: { scope: "allOrganizationBranches", branchIds: [], operationalBranchIds: [branchId] } };
const item = { id: auditId, occurredAtUtc: "2026-07-23T01:00:00Z", action: "branchDeactivated", result: "succeeded", actorId, actorDisplayName: "Management User", targetType: "Branch", targetId: branchId, branch: { id: branchId, code: "CAI", name: "Cairo" }, correlationId: "corr-safe-1", summary: "branchDeactivated Branch" };

test.beforeEach(async ({ page }) => {
  await page.route("**/api/v1/auth/csrf", route => route.fulfill({ json: { requestToken: "e2e-csrf" } }));
  await page.route("**/api/v1/auth/me", route => route.fulfill({ json: currentUser }));
  await page.route("**/api/v1/management/audit-logs/options", route => route.fulfill({ json: { actions: [{ code: "branchDeactivated", presentationKey: "audit.action.branchDeactivated", category: "organization" }], results: [{ code: "succeeded", presentationKey: "audit.result.succeeded", category: "result" }], branches: [{ id: branchId, code: "CAI", name: "Cairo" }] } }));
});

test("audit list defaults to seven days, pages by opaque cursor, and persists no audit data", async ({ page }) => {
  let requestCount = 0;
  await page.route(/\/api\/v1\/management\/audit-logs\?.*/, async route => {
    const url = new URL(route.request().url());
    const from = Date.parse(url.searchParams.get("fromUtc") ?? ""); const to = Date.parse(url.searchParams.get("toUtc") ?? "");
    expect(to - from).toBeGreaterThanOrEqual(7 * 24 * 60 * 60 * 1000 - 2000);
    expect(to - from).toBeLessThanOrEqual(7 * 24 * 60 * 60 * 1000 + 2000);
    expect(url.searchParams.get("pageSize")).toBe("25");
    requestCount++;
    if (url.searchParams.get("cursor")) await route.fulfill({ json: { items: [], pageSize: 25, nextCursor: null, previousCursor: "opaque-previous" } });
    else await route.fulfill({ json: { items: [item], pageSize: 25, nextCursor: "opaque-next", previousCursor: null } });
  });
  await page.goto("/management/audit-logs");
  await expect(page.getByRole("heading", { name: "Audit log" })).toBeVisible();
  await expect(page.getByRole("cell", { name: "Branch Deactivated", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Next" }).click();
  expect(requestCount).toBe(2);
  const persisted = await page.evaluate(() => JSON.stringify({ local: { ...localStorage }, session: { ...sessionStorage } }));
  expect(persisted).not.toContain(auditId); expect(persisted).not.toContain("corr-safe-1"); expect(persisted).not.toContain("opaque-next");
  await page.unroute("**/api/v1/auth/me");
  await page.route("**/api/v1/auth/me", route => route.fulfill({ json: { ...currentUser, permissions: ["management.access"] } }));
  await page.goto("/management/audit-logs");
  await expect(page).toHaveURL(/\/management\/access-denied/);
  await expect(page.getByRole("heading", { name: "Access denied" })).toBeVisible();
});

test("deep-linked safe detail is keyboard accessible and renders Arabic RTL", async ({ page }) => {
  await page.route(`**/api/v1/management/audit-logs/${auditId}`, route => route.fulfill({ json: { ...item, reasonCode: null, metadata: { changedFields: [], previousStatus: "active", newStatus: "suspended", registeredDeviceCount: null, activeDeviceCount: null, activeCredentialCount: null, activeSessionCount: null, activeUserAssignmentCount: null, activeRoleAssignmentCount: null, sessionsRevokedCount: null, relatedUserId: null, relatedBranchId: null, relatedDeviceId: null } } }));
  await page.goto(`/management/audit-logs/${auditId}`);
  await expect(page.getByRole("heading", { name: "Audit detail" })).toBeVisible();
  const back = page.getByRole("link", { name: /Back to audit log/ });
  for (let index = 0; index < 12 && !await back.evaluate(element => element === document.activeElement); index++) await page.keyboard.press("Tab");
  await expect(back).toBeFocused();
  await page.getByRole("button", { name: /AR/ }).click();
  await expect(page.locator(".shell")).toHaveAttribute("dir", "rtl");
  await expect(page.getByRole("heading", { name: "تفاصيل التدقيق" })).toBeVisible();
  await expect(page.getByText("إيقاف فرع")).toBeVisible();
});
