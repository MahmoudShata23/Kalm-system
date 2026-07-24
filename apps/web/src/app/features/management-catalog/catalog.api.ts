import { HttpClient, HttpHeaders, HttpParams, HttpResponse } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { map, Observable } from "rxjs";
import {
  CategoryDetail,
  CategoryListResponse,
  CategoryWriteRequest,
  ProductDetail,
  ProductListResponse,
  ProductOptions,
  ProductWriteRequest,
  VersionedCategory,
  VersionedProduct
} from "./catalog.models";

@Injectable({ providedIn: "root" })
export class CatalogApi {
  private readonly http = inject(HttpClient);
  private readonly categories = "/api/v1/management/catalog/categories";
  private readonly products = "/api/v1/management/catalog/products";

  listCategories(status: string, search: string, page: number, pageSize = 25): Observable<HttpResponse<CategoryListResponse>> {
    let params = new HttpParams().set("status", status).set("page", page).set("pageSize", pageSize);
    if (search.trim()) params = params.set("search", search.trim());
    return this.http.get<CategoryListResponse>(this.categories, { params, observe: "response" });
  }

  getCategory(id: string): Observable<VersionedCategory> {
    return this.http.get<CategoryDetail>(`${this.categories}/${id}`, { observe: "response" })
      .pipe(map(response => this.versionedCategory(response)));
  }

  createCategory(request: CategoryWriteRequest): Observable<VersionedCategory> {
    return this.http.post<CategoryDetail>(this.categories, request, { observe: "response" })
      .pipe(map(response => this.versionedCategory(response)));
  }

  updateCategory(id: string, etag: string, request: CategoryWriteRequest): Observable<VersionedCategory> {
    return this.http.put<CategoryDetail>(`${this.categories}/${id}`, request, {
      headers: this.match(etag),
      observe: "response"
    }).pipe(map(response => this.versionedCategory(response)));
  }

  activateCategory(id: string, etag: string): Observable<VersionedCategory> {
    return this.categoryStatus(id, etag, "activate");
  }

  archiveCategory(id: string, etag: string): Observable<VersionedCategory> {
    return this.categoryStatus(id, etag, "archive");
  }

  reorderCategories(ids: string[], etag: string): Observable<void> {
    return this.http.put<void>(`${this.categories}/order`, { categoryIds: ids }, { headers: this.match(etag) });
  }

  listProducts(
    status: string,
    search: string,
    categoryId: string,
    productType: string,
    page: number,
    pageSize = 25
  ): Observable<ProductListResponse> {
    let params = new HttpParams().set("status", status).set("page", page).set("pageSize", pageSize);
    if (search.trim()) params = params.set("search", search.trim());
    if (categoryId) params = params.set("categoryId", categoryId);
    if (productType) params = params.set("productType", productType);
    return this.http.get<ProductListResponse>(this.products, { params });
  }

  productOptions(): Observable<ProductOptions> {
    return this.http.get<ProductOptions>(`${this.products}/options`);
  }

  getProduct(id: string): Observable<VersionedProduct> {
    return this.http.get<ProductDetail>(`${this.products}/${id}`, { observe: "response" })
      .pipe(map(response => this.versionedProduct(response)));
  }

  createProduct(request: ProductWriteRequest): Observable<VersionedProduct> {
    return this.http.post<ProductDetail>(this.products, request, { observe: "response" })
      .pipe(map(response => this.versionedProduct(response)));
  }

  updateProduct(id: string, etag: string, request: ProductWriteRequest): Observable<VersionedProduct> {
    return this.http.put<ProductDetail>(`${this.products}/${id}`, request, {
      headers: this.match(etag),
      observe: "response"
    }).pipe(map(response => this.versionedProduct(response)));
  }

  activateProduct(id: string, etag: string): Observable<VersionedProduct> {
    return this.productStatus(id, etag, "activate");
  }

  archiveProduct(id: string, etag: string): Observable<VersionedProduct> {
    return this.productStatus(id, etag, "archive");
  }

  private categoryStatus(id: string, etag: string, action: "activate" | "archive"): Observable<VersionedCategory> {
    return this.http.post<CategoryDetail>(`${this.categories}/${id}/${action}`, null, {
      headers: this.match(etag),
      observe: "response"
    }).pipe(map(response => this.versionedCategory(response)));
  }

  private productStatus(id: string, etag: string, action: "activate" | "archive"): Observable<VersionedProduct> {
    return this.http.post<ProductDetail>(`${this.products}/${id}/${action}`, null, {
      headers: this.match(etag),
      observe: "response"
    }).pipe(map(response => this.versionedProduct(response)));
  }

  private match(etag: string): HttpHeaders {
    return new HttpHeaders({ "If-Match": etag });
  }

  private versionedCategory(response: HttpResponse<CategoryDetail>): VersionedCategory {
    const etag = response.headers.get("ETag");
    if (!response.body || !etag) throw new Error("Category response is incomplete.");
    return { category: response.body, etag };
  }

  private versionedProduct(response: HttpResponse<ProductDetail>): VersionedProduct {
    const etag = response.headers.get("ETag");
    if (!response.body || !etag) throw new Error("Product response is incomplete.");
    return { product: response.body, etag };
  }
}
