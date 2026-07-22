import { expect, test } from "@playwright/test";

const deviceId = "11111111-1111-4111-8111-111111111111";
const branchId = "22222222-2222-4222-8222-222222222222";
const userId = "33333333-3333-4333-8333-333333333333";
const currentUser = {
  isAuthenticated: true,
  username: "manager",
  displayName: "Management User",
  preferredLanguage: "en",
  permissions: ["management.access", "devices.manage"],
  branchAccess: {
    scope: "assignedBranches",
    branchIds: [branchId],
    operationalBranchIds: [branchId],
  },
};

test("device administrator sees a one-time pairing value without exposing it on detail", async ({
  page,
}) => {
  await page.route("**/api/v1/auth/csrf", (route) =>
    route.fulfill({ json: { requestToken: "e2e-csrf" } }),
  );
  await page.route("**/api/v1/auth/me", (route) =>
    route.fulfill({ json: currentUser }),
  );
  await page.route("**/api/v1/management/devices/options", (route) =>
    route.fulfill({
      json: {
        branches: [{ id: branchId, name: "Cairo", code: "CAI" }],
        types: [
          { code: "posTerminal", nameEn: "POS terminal", nameAr: "نقطة بيع" },
        ],
      },
    }),
  );
  await page.route(`**/api/v1/management/devices/${deviceId}`, (route) =>
    route.fulfill({
      json: {
        id: deviceId,
        branchId,
        branchName: "Cairo",
        name: "Counter POS",
        type: "posTerminal",
        platform: "Windows",
        status: "active",
        pairedAtUtc: "2026-07-22T00:00:00Z",
        lastSeenAtUtc: null,
        createdAtUtc: "2026-07-22T00:00:00Z",
        updatedAtUtc: "2026-07-22T00:00:00Z",
      },
      headers: { ETag: '"3"' },
    }),
  );
  await page.route(
    `**/api/v1/management/devices/${deviceId}/pairing-challenge`,
    async (route) => {
      expect(route.request().headers()["x-xsrf-token"]).toBe("e2e-csrf");
      await route.fulfill({
        json: {
          deviceId,
          pairingChallenge: "one-time-pairing-value",
          expiresAtUtc: "2099-07-22T00:10:00Z",
        },
      });
    },
  );

  await page.goto(`/management/devices/${deviceId}`);
  await expect(
    page.getByRole("heading", { name: "Edit device" }),
  ).toBeVisible();
  await expect(page.getByText("one-time-pairing-value")).toHaveCount(0);
  await page.getByRole("button", { name: "Create pairing challenge" }).click();
  await expect(page.getByText("one-time-pairing-value")).toBeVisible();
  await page.getByRole("button", { name: "Dismiss" }).click();
  await expect(page.getByText("one-time-pairing-value")).toHaveCount(0);
  const persisted = await page.evaluate(async () => ({
    local: Object.values(localStorage),
    session: Object.values(sessionStorage),
    databases: (await indexedDB.databases()).map((database) => database.name),
  }));
  expect(JSON.stringify(persisted)).not.toContain("one-time-pairing-value");
});

test("paired workstation selects an eligible employee, signs in, then locks without re-pairing", async ({
  page,
}) => {
  let signedIn = false;
  await page.route("**/api/v1/auth/csrf", (route) =>
    route.fulfill({ json: { requestToken: "device-csrf" } }),
  );
  await page.route("**/api/v1/auth/me", (route) =>
    route.fulfill(
      signedIn
        ? {
            json: {
              ...currentUser,
              username: "employee",
              displayName: "Employee",
              permissions: [],
            },
          }
        : { status: 401, json: { code: "auth.required" } },
    ),
  );
  await page.route("**/api/v1/devices/pair", async (route) => {
    expect(route.request().postDataJSON()).toEqual({
      deviceId,
      pairingChallenge: "one-time-value",
    });
    await route.fulfill({ status: 204 });
  });
  await page.route("**/api/v1/devices/eligible-users", (route) =>
    route.fulfill({
      json: {
        items: [{ id: userId, displayName: "Employee" }],
      },
    }),
  );
  await page.route("**/api/v1/auth/pin-login", async (route) => {
    expect(route.request().postDataJSON()).toEqual({ userId, pin: "123456" });
    signedIn = true;
    await route.fulfill({
      json: { displayName: "Employee", preferredLanguage: "en" },
    });
  });
  await page.route("**/api/v1/auth/lock", async (route) => {
    expect(route.request().headers()["x-xsrf-token"]).toBe("device-csrf");
    await route.fulfill({ status: 204 });
  });

  await page.goto("/device/pair");
  await page.getByLabel("Device ID").fill(deviceId);
  await page.getByLabel("Pairing value").fill("one-time-value");
  await page.getByRole("button", { name: "Pair", exact: true }).click();
  await expect(page).toHaveURL(/\/workstation\/login/);
  expect(
    await page.evaluate(() =>
      JSON.stringify({
        local: Object.values(localStorage),
        session: Object.values(sessionStorage),
      }),
    ),
  ).not.toContain("one-time-value");
  await page.getByRole("button", { name: "Employee" }).click();
  const pin = page.getByLabel("Employee PIN");
  await expect(pin).toHaveAttribute("type", "password");
  await pin.fill("123456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByText("Workstation ready, Employee")).toBeVisible();
  await page.getByRole("button", { name: "Lock and switch employee" }).click();
  await expect(page).toHaveURL(/\/workstation\/locked/);
  await expect(page.getByRole("button", { name: "Employee" })).toBeVisible();
});
