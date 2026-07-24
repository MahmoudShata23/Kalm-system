import { HttpErrorResponse } from "@angular/common/http";
import { Injectable, inject, signal } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { CatalogApi } from "./catalog.api";
import {
  CatalogProblem,
  CategoryListResponse,
  CategoryWriteRequest,
  ProductListResponse,
  ProductOptions,
  ProductWriteRequest,
  VersionedCategory,
  VersionedProduct
} from "./catalog.models";

@Injectable({ providedIn: "root" })
export class CatalogFacade {
  private readonly api = inject(CatalogApi);
  private readonly categoriesState = signal<CategoryListResponse | null>(null);
  private readonly categoryEtagState = signal<string | null>(null);
  private readonly categoryState = signal<VersionedCategory | null>(null);
  private readonly productsState = signal<ProductListResponse | null>(null);
  private readonly productState = signal<VersionedProduct | null>(null);
  private readonly optionsState = signal<ProductOptions | null>(null);
  private readonly loadingState = signal(false);
  private readonly savingState = signal(false);
  private readonly errorState = signal<string | null>(null);
  private readonly activeProductsState = signal<number | null>(null);

  readonly categories = this.categoriesState.asReadonly();
  readonly category = this.categoryState.asReadonly();
  readonly products = this.productsState.asReadonly();
  readonly product = this.productState.asReadonly();
  readonly options = this.optionsState.asReadonly();
  readonly loading = this.loadingState.asReadonly();
  readonly saving = this.savingState.asReadonly();
  readonly errorCode = this.errorState.asReadonly();
  readonly activeProductCount = this.activeProductsState.asReadonly();

  async loadCategories(status: string, search: string, page: number): Promise<void> {
    await this.load(async () => {
      const response = await firstValueFrom(this.api.listCategories(status, search, page, 100));
      this.categoriesState.set(response.body);
      this.categoryEtagState.set(response.headers.get("ETag"));
    });
  }

  async loadCategory(id?: string): Promise<void> {
    await this.load(async () => this.categoryState.set(id ? await firstValueFrom(this.api.getCategory(id)) : null));
  }

  async saveCategory(request: CategoryWriteRequest): Promise<VersionedCategory | null> {
    const current = this.categoryState();
    return this.save(async () => {
      const result = await firstValueFrom(current
        ? this.api.updateCategory(current.category.id, current.etag, request)
        : this.api.createCategory(request));
      this.categoryState.set(result);
      return result;
    });
  }

  async changeCategoryStatus(action: "activate" | "archive"): Promise<boolean> {
    const current = this.categoryState();
    if (!current) return false;
    return (await this.save(async () => {
      const result = await firstValueFrom(action === "activate"
        ? this.api.activateCategory(current.category.id, current.etag)
        : this.api.archiveCategory(current.category.id, current.etag));
      this.categoryState.set(result);
      return result;
    })) !== null;
  }

  async moveCategory(index: number, delta: number): Promise<boolean> {
    const list = this.categoriesState();
    const etag = this.categoryEtagState();
    const destination = index + delta;
    if (!list || !etag || destination < 0 || destination >= list.items.length) return false;
    const items = [...list.items];
    [items[index], items[destination]] = [items[destination], items[index]];
    return (await this.save(async () => {
      await firstValueFrom(this.api.reorderCategories(items.map(item => item.id), etag));
      this.categoriesState.set({ ...list, items });
      return true;
    })) !== null;
  }

  async refreshCategoryVersionPreservingDraft(): Promise<void> {
    const current = this.categoryState();
    if (!current) return;
    await this.load(async () => this.categoryState.set(await firstValueFrom(this.api.getCategory(current.category.id))));
  }

  async loadProducts(status: string, search: string, categoryId: string, productType: string, page: number): Promise<void> {
    await this.load(async () => this.productsState.set(
      await firstValueFrom(this.api.listProducts(status, search, categoryId, productType, page))));
  }

  async loadProduct(id?: string): Promise<void> {
    await this.load(async () => {
      const [product, options] = await Promise.all([
        id ? firstValueFrom(this.api.getProduct(id)) : Promise.resolve(null),
        firstValueFrom(this.api.productOptions())
      ]);
      this.productState.set(product);
      this.optionsState.set(options);
    });
  }

  async loadProductOptions(): Promise<void> {
    if (this.optionsState()) return;
    await this.load(async () => this.optionsState.set(await firstValueFrom(this.api.productOptions())));
  }

  async saveProduct(request: ProductWriteRequest): Promise<VersionedProduct | null> {
    const current = this.productState();
    return this.save(async () => {
      const result = await firstValueFrom(current
        ? this.api.updateProduct(current.product.id, current.etag, request)
        : this.api.createProduct(request));
      this.productState.set(result);
      return result;
    });
  }

  async changeProductStatus(action: "activate" | "archive"): Promise<boolean> {
    const current = this.productState();
    if (!current) return false;
    return (await this.save(async () => {
      const result = await firstValueFrom(action === "activate"
        ? this.api.activateProduct(current.product.id, current.etag)
        : this.api.archiveProduct(current.product.id, current.etag));
      this.productState.set(result);
      return result;
    })) !== null;
  }

  async refreshProductVersionPreservingDraft(): Promise<void> {
    const current = this.productState();
    if (!current) return;
    await this.load(async () => this.productState.set(await firstValueFrom(this.api.getProduct(current.product.id))));
  }

  clearError(): void {
    this.errorState.set(null);
    this.activeProductsState.set(null);
  }

  private async load(operation: () => Promise<void>): Promise<void> {
    this.loadingState.set(true);
    this.clearError();
    try {
      await operation();
    } catch (error) {
      this.capture(error);
    } finally {
      this.loadingState.set(false);
    }
  }

  private async save<T>(operation: () => Promise<T>): Promise<T | null> {
    this.savingState.set(true);
    this.clearError();
    try {
      return await operation();
    } catch (error) {
      this.capture(error);
      return null;
    } finally {
      this.savingState.set(false);
    }
  }

  private capture(error: unknown): void {
    if (error instanceof HttpErrorResponse) {
      const problem = error.error as CatalogProblem | null;
      this.errorState.set(problem?.code ?? `http.${error.status}`);
      this.activeProductsState.set(problem?.activeProductCount ?? null);
      return;
    }
    this.errorState.set("client.unexpected");
  }
}
