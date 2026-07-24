import { HttpErrorResponse, HttpHeaders, HttpResponse } from "@angular/common/http";
import { TestBed } from "@angular/core/testing";
import { of, throwError } from "rxjs";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CatalogApi } from "./catalog.api";
import { CatalogFacade } from "./catalog.facade";
import { VersionedCategory, VersionedProduct } from "./catalog.models";

describe("CatalogFacade", () => {
  const category: VersionedCategory = { category: {
    id: "11111111-1111-4111-8111-111111111111", arabicName: "قهوة", englishName: "Coffee",
    displayOrder: 0, status: "active", posColorToken: null, iconCode: null,
    createdAtUtc: "2026-07-23T00:00:00Z", updatedAtUtc: "2026-07-23T00:00:00Z"
  }, etag: '"3"' };
  const product: VersionedProduct = { product: {
    id: "22222222-2222-4222-8222-222222222222", categoryId: category.category.id,
    categoryArabicName: "قهوة", categoryEnglishName: "Coffee", arabicName: "لاتيه", englishName: "Latte",
    arabicDescription: null, englishDescription: null, sku: "LATTE", productType: "madeToOrder",
    displayOrder: 0, status: "active", variants: [], createdAtUtc: "2026-07-23T00:00:00Z",
    updatedAtUtc: "2026-07-23T00:00:00Z"
  }, etag: '"5"' };
  const api = {
    listCategories: vi.fn(), getCategory: vi.fn(), createCategory: vi.fn(), updateCategory: vi.fn(),
    activateCategory: vi.fn(), archiveCategory: vi.fn(), reorderCategories: vi.fn(),
    listProducts: vi.fn(), productOptions: vi.fn(), getProduct: vi.fn(), createProduct: vi.fn(),
    updateProduct: vi.fn(), activateProduct: vi.fn(), archiveProduct: vi.fn()
  };

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({ providers: [CatalogFacade, { provide: CatalogApi, useValue: api }] });
  });

  it("keeps safe archive conflict counts and refreshes only the category ETag", async () => {
    api.getCategory.mockReturnValueOnce(of(category));
    const facade = TestBed.inject(CatalogFacade);
    await facade.loadCategory(category.category.id);
    api.archiveCategory.mockReturnValue(throwError(() => new HttpErrorResponse({
      status: 409, error: { code: "catalog.category_has_active_products", activeProductCount: 2 }
    })));
    expect(await facade.changeCategoryStatus("archive")).toBe(false);
    expect(facade.activeProductCount()).toBe(2);
    api.getCategory.mockReturnValueOnce(of({ ...category, etag: '"4"' }));
    await facade.refreshCategoryVersionPreservingDraft();
    expect(facade.category()?.etag).toBe('"4"');
  });

  it("retains authoritative product aggregate state after a stale write", async () => {
    api.productOptions.mockReturnValue(of({ categories: [], productTypes: [], sizeCodes: [], temperatureCodes: [], servingFormatCodes: [] }));
    api.getProduct.mockReturnValueOnce(of(product));
    const facade = TestBed.inject(CatalogFacade);
    await facade.loadProduct(product.product.id);
    api.updateProduct.mockReturnValue(throwError(() => new HttpErrorResponse({
      status: 412, error: { code: "catalog.concurrency_conflict", currentEtag: '"6"' }
    })));
    expect(await facade.saveProduct({
      categoryId: product.product.categoryId, arabicName: "مسودة", englishName: "Draft",
      arabicDescription: null, englishDescription: null, sku: "LATTE", productType: "madeToOrder",
      displayOrder: 0, variants: [], variantOrder: []
    })).toBeNull();
    expect(facade.product()?.etag).toBe('"5"');
    expect(facade.errorCode()).toBe("catalog.concurrency_conflict");
  });

  it("uses the complete collection ETag when reordering", async () => {
    api.listCategories.mockReturnValue(of(new HttpResponse({
      body: { items: [category.category], page: 1, pageSize: 25, totalCount: 1 },
      headers: new HttpHeaders({ ETag: '"c-one"' })
    })));
    api.reorderCategories.mockReturnValue(of(undefined));
    const facade = TestBed.inject(CatalogFacade);
    await facade.loadCategories("all", "", 1);
    expect(await facade.moveCategory(0, 1)).toBe(false);
    expect(api.reorderCategories).not.toHaveBeenCalled();
  });
});
