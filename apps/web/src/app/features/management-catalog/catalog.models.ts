export type CatalogStatus = "active" | "archived";
export type ProductType = "madeToOrder" | "purchasedFinishedGood" | "serviceNonStock";

export interface CategorySummary {
  id: string;
  arabicName: string;
  englishName: string;
  displayOrder: number;
  status: CatalogStatus;
  posColorToken: string | null;
  iconCode: string | null;
  updatedAtUtc: string;
}

export interface CategoryDetail extends CategorySummary {
  createdAtUtc: string;
}

export interface CategoryListResponse {
  items: CategorySummary[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CategoryWriteRequest {
  arabicName: string;
  englishName: string;
  displayOrder: number;
  posColorToken: string | null;
  iconCode: string | null;
}

export interface VersionedCategory {
  category: CategoryDetail;
  etag: string;
}

export interface ProductVariant {
  id: string;
  arabicName: string;
  englishName: string;
  code: string;
  barcode: string | null;
  sizeCode: string | null;
  temperatureCode: string | null;
  servingFormatCode: string | null;
  displayOrder: number;
  status: CatalogStatus;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface VariantWriteRequest {
  id: string | null;
  arabicName: string;
  englishName: string;
  code: string;
  barcode: string | null;
  sizeCode: string | null;
  temperatureCode: string | null;
  servingFormatCode: string | null;
  displayOrder: number;
  status: CatalogStatus;
}

export interface ProductSummary {
  id: string;
  categoryId: string;
  categoryArabicName: string;
  categoryEnglishName: string;
  arabicName: string;
  englishName: string;
  sku: string;
  productType: ProductType;
  displayOrder: number;
  status: CatalogStatus;
  variantCount: number;
  activeVariantCount: number;
  updatedAtUtc: string;
}

export interface ProductDetail extends Omit<ProductSummary, "variantCount" | "activeVariantCount"> {
  arabicDescription: string | null;
  englishDescription: string | null;
  variants: ProductVariant[];
  createdAtUtc: string;
}

export interface ProductListResponse {
  items: ProductSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ProductWriteRequest {
  categoryId: string;
  arabicName: string;
  englishName: string;
  arabicDescription: string | null;
  englishDescription: string | null;
  sku: string;
  productType: ProductType;
  displayOrder: number;
  variants: VariantWriteRequest[];
  variantOrder: string[] | null;
}

export interface VersionedProduct {
  product: ProductDetail;
  etag: string;
}

export interface CatalogOption {
  code: string;
  englishLabel: string;
  arabicLabel: string;
}

export interface CategoryOption {
  id: string;
  arabicName: string;
  englishName: string;
}

export interface ProductOptions {
  categories: CategoryOption[];
  productTypes: CatalogOption[];
  sizeCodes: CatalogOption[];
  temperatureCodes: CatalogOption[];
  servingFormatCodes: CatalogOption[];
}

export interface CatalogProblem {
  code?: string;
  currentEtag?: string;
  activeProductCount?: number;
}
