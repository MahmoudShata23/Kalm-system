import { expect, test } from "@playwright/test";

const categoryId = "11111111-1111-4111-8111-111111111111";
const productId = "22222222-2222-4222-8222-222222222222";
const variantId = "33333333-3333-4333-8333-333333333333";
const now = "2026-07-23T00:00:00Z";
const currentUser = {
  isAuthenticated: true,
  username: "manager",
  displayName: "Management User",
  preferredLanguage: "en",
  permissions: ["management.access", "catalog.view", "catalog.manage"],
  branchAccess: { scope: "allOrganizationBranches", branchIds: [], operationalBranchIds: [] }
};
const category = {
  id: categoryId, arabicName: "قهوة", englishName: "Coffee", displayOrder: 0, status: "active",
  posColorToken: "coffee", iconCode: "coffee", createdAtUtc: now, updatedAtUtc: now
};
const product = {
  id: productId, categoryId, categoryArabicName: "قهوة", categoryEnglishName: "Coffee",
  arabicName: "لاتيه", englishName: "Latte", arabicDescription: null, englishDescription: null,
  sku: "LATTE", productType: "madeToOrder", displayOrder: 0, status: "active", createdAtUtc: now, updatedAtUtc: now,
  variants: [{
    id: variantId, arabicName: "وسط", englishName: "Medium", code: "LATTE-M", barcode: "622100000001",
    sizeCode: "medium", temperatureCode: "hot", servingFormatCode: "cup", displayOrder: 0,
    status: "active", createdAtUtc: now, updatedAtUtc: now
  }]
};
const options = {
  categories: [{ id: categoryId, arabicName: "قهوة", englishName: "Coffee" }],
  productTypes: [{ code: "madeToOrder", englishLabel: "Made to order", arabicLabel: "يُحضّر عند الطلب" }],
  sizeCodes: [{ code: "medium", englishLabel: "Medium", arabicLabel: "وسط" }],
  temperatureCodes: [{ code: "hot", englishLabel: "Hot", arabicLabel: "ساخن" }],
  servingFormatCodes: [{ code: "cup", englishLabel: "Cup", arabicLabel: "كوب" }]
};

test.beforeEach(async ({ page }) => {
  await page.route("**/api/v1/auth/csrf", route => route.fulfill({ json: { requestToken: "e2e-csrf" } }));
  await page.route("**/api/v1/auth/me", route => route.fulfill({ json: currentUser }));
});

test("category ordering is keyboard accessible and category conflicts are safely presented", async ({ page }) => {
  const second = { ...category, id: "44444444-4444-4444-8444-444444444444", englishName: "Desserts", arabicName: "حلويات", displayOrder: 1 };
  let reordered = false;
  await page.route(/\/api\/v1\/management\/catalog\/categories\?.*/, route => route.fulfill({
    json: { items: reordered ? [second, category] : [category, second], page: 1, pageSize: 25, totalCount: 2 },
    headers: { ETag: reordered ? '"c-2"' : '"c-1"' }
  }));
  await page.route("**/api/v1/management/catalog/categories/order", async route => {
    expect(route.request().headers()["if-match"]).toBe('"c-1"');
    expect(route.request().headers()["x-xsrf-token"]).toBe("e2e-csrf");
    expect((await route.request().postDataJSON()).categoryIds).toEqual([second.id, category.id]);
    reordered = true;
    await route.fulfill({ status: 204 });
  });
  await page.goto("/management/catalog/categories");
  await expect(page.getByRole("heading", { name: "Categories" })).toBeVisible();
  await page.getByRole("button", { name: "Move down" }).first().focus();
  await page.keyboard.press("Enter");
  await expect(page.getByRole("row").nth(1)).toContainText("Desserts");

  await page.route(`**/api/v1/management/catalog/categories/${categoryId}`, route => route.fulfill({ json: category, headers: { ETag: '"3"' } }));
  await page.route(`**/api/v1/management/catalog/categories/${categoryId}/archive`, route => route.fulfill({
    status: 409, json: { code: "catalog.category_has_active_products", activeProductCount: 2 }
  }));
  await page.goto(`/management/catalog/categories/${categoryId}`);
  await page.getByRole("button", { name: "Archive" }).click();
  await page.getByRole("button", { name: "Confirm" }).click();
  await expect(page.getByText("Active products: 2")).toBeVisible();
});

test("product aggregate preserves a bilingual draft after stale ETag and stores no catalogue draft", async ({ page }) => {
  await page.route("**/api/v1/management/catalog/products/options", route => route.fulfill({ json: options }));
  await page.route(`**/api/v1/management/catalog/products/${productId}`, async route => {
    if (route.request().method() === "GET") {
      await route.fulfill({ json: product, headers: { ETag: '"8"' } });
      return;
    }
    expect(route.request().headers()["if-match"]).toBe('"8"');
    expect(route.request().headers()["x-xsrf-token"]).toBe("e2e-csrf");
    const body = await route.request().postDataJSON();
    expect(body.variants).toHaveLength(1);
    expect(body.variantOrder).toEqual([variantId]);
    await route.fulfill({ status: 412, json: { code: "catalog.concurrency_conflict", currentEtag: '"9"' } });
  });
  await page.goto(`/management/catalog/products/${productId}`);
  const englishName = page.getByLabel("English name").first();
  await englishName.fill("Draft Latte");
  await page.getByRole("button", { name: "Save" }).click();
  await expect(page.getByText("A newer version is available.")).toBeVisible();
  await expect(englishName).toHaveValue("Draft Latte");
  await page.getByRole("button", { name: "Load latest version and keep my draft" }).click();
  await expect(englishName).toHaveValue("Draft Latte");

  await page.getByRole("button", { name: /AR/ }).click();
  await expect(page.locator(".shell")).toHaveAttribute("dir", "rtl");
  await expect(page.getByRole("heading", { name: "تعديل المنتج" })).toBeVisible();
  expect(await page.evaluate(() => Object.keys(localStorage).concat(Object.keys(sessionStorage))
    .some(key => /catalog|product|variant|draft/i.test(key)))).toBe(false);
});

test("catalog route ordering opens new before parameter routes and exact view permission guards access", async ({ page }) => {
  await page.route("**/api/v1/management/catalog/products/options", route => route.fulfill({ json: options }));
  let unexpectedGet = false;
  await page.route("**/api/v1/management/catalog/products/new", route => { unexpectedGet = true; return route.abort(); });
  await page.goto("/management/catalog/products/new");
  await expect(page.getByRole("heading", { name: "New product" })).toBeVisible();
  expect(unexpectedGet).toBe(false);

  await page.unroute("**/api/v1/auth/me");
  await page.route("**/api/v1/auth/me", route => route.fulfill({ json: { ...currentUser, permissions: ["management.access"] } }));
  await page.goto("/management/catalog/categories");
  await expect(page).toHaveURL(/\/management\/access-denied/);
});
