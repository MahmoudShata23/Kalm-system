import { provideHttpClient } from "@angular/common/http";
import { HttpTestingController, provideHttpClientTesting } from "@angular/common/http/testing";
import { TestBed } from "@angular/core/testing";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { CatalogApi } from "./catalog.api";
import { CategoryDetail, ProductDetail, ProductWriteRequest } from "./catalog.models";

describe("CatalogApi", () => {
  let api: CatalogApi;
  let http: HttpTestingController;
  const category: CategoryDetail = {
    id: "11111111-1111-4111-8111-111111111111", arabicName: "قهوة", englishName: "Coffee",
    displayOrder: 0, status: "active", posColorToken: "coffee", iconCode: "coffee",
    createdAtUtc: "2026-07-23T00:00:00Z", updatedAtUtc: "2026-07-23T00:00:00Z"
  };
  const product: ProductDetail = {
    id: "22222222-2222-4222-8222-222222222222", categoryId: category.id,
    categoryArabicName: category.arabicName, categoryEnglishName: category.englishName,
    arabicName: "لاتيه", englishName: "Latte", arabicDescription: null, englishDescription: null,
    sku: "LATTE", productType: "madeToOrder", displayOrder: 0, status: "active", variants: [],
    createdAtUtc: category.createdAtUtc, updatedAtUtc: category.updatedAtUtc
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(CatalogApi);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it("uses bounded filters and strong ETags for category writes and exact ordering", () => {
    api.listCategories("active", "Coffee", 2).subscribe();
    const list = http.expectOne(request => request.url === "/api/v1/management/catalog/categories");
    expect(list.request.params.get("pageSize")).toBe("25");
    expect(list.request.params.get("search")).toBe("Coffee");
    list.flush({ items: [], page: 2, pageSize: 25, totalCount: 0 }, { headers: { ETag: '"c-list"' } });

    api.updateCategory(category.id, '"3"', { arabicName: category.arabicName, englishName: category.englishName, displayOrder: 0, posColorToken: null, iconCode: null }).subscribe();
    const update = http.expectOne(`/api/v1/management/catalog/categories/${category.id}`);
    expect(update.request.headers.get("If-Match")).toBe('"3"');
    update.flush(category, { headers: { ETag: '"4"' } });

    api.reorderCategories([category.id], '"c-list"').subscribe();
    const reorder = http.expectOne("/api/v1/management/catalog/categories/order");
    expect(reorder.request.headers.get("If-Match")).toBe('"c-list"');
    expect(reorder.request.body).toEqual({ categoryIds: [category.id] });
    reorder.flush(null);
  });

  it("uses the product aggregate only and carries its ETag across variants", () => {
    const request: ProductWriteRequest = {
      categoryId: category.id, arabicName: product.arabicName, englishName: product.englishName,
      arabicDescription: null, englishDescription: null, sku: product.sku, productType: "madeToOrder",
      displayOrder: 0, variants: [{
        id: null, arabicName: "افتراضي", englishName: "Default", code: "LATTE-M", barcode: null,
        sizeCode: "medium", temperatureCode: "hot", servingFormatCode: "cup", displayOrder: 0, status: "active"
      }], variantOrder: null
    };
    api.updateProduct(product.id, '"8"', request).subscribe(result => expect(result.etag).toBe('"9"'));
    const update = http.expectOne(`/api/v1/management/catalog/products/${product.id}`);
    expect(update.request.headers.get("If-Match")).toBe('"8"');
    expect(update.request.body.variants).toHaveLength(1);
    update.flush(product, { headers: { ETag: '"9"' } });
  });
});
