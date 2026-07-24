namespace Kalm.Api.Features.CatalogAdministration;

public sealed record CategoryWriteRequest(
    string ArabicName,
    string EnglishName,
    int DisplayOrder,
    string? PosColorToken,
    string? IconCode);

public sealed record CategoryOrderRequest(IReadOnlyList<Guid> CategoryIds);

public sealed record CategorySummaryResponse(
    Guid Id,
    string ArabicName,
    string EnglishName,
    int DisplayOrder,
    string Status,
    string? PosColorToken,
    string? IconCode,
    DateTimeOffset UpdatedAtUtc);

public sealed record CategoryDetailResponse(
    Guid Id,
    string ArabicName,
    string EnglishName,
    int DisplayOrder,
    string Status,
    string? PosColorToken,
    string? IconCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CategoryListResponse(
    IReadOnlyList<CategorySummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record VariantWriteRequest(
    Guid? Id,
    string ArabicName,
    string EnglishName,
    string Code,
    string? Barcode,
    string? SizeCode,
    string? TemperatureCode,
    string? ServingFormatCode,
    int DisplayOrder,
    string? Status);

public sealed record ProductWriteRequest(
    Guid CategoryId,
    string ArabicName,
    string EnglishName,
    string? ArabicDescription,
    string? EnglishDescription,
    string Sku,
    string ProductType,
    int DisplayOrder,
    IReadOnlyList<VariantWriteRequest> Variants,
    IReadOnlyList<Guid>? VariantOrder);

public sealed record ProductVariantResponse(
    Guid Id,
    string ArabicName,
    string EnglishName,
    string Code,
    string? Barcode,
    string? SizeCode,
    string? TemperatureCode,
    string? ServingFormatCode,
    int DisplayOrder,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record ProductSummaryResponse(
    Guid Id,
    Guid CategoryId,
    string CategoryArabicName,
    string CategoryEnglishName,
    string ArabicName,
    string EnglishName,
    string Sku,
    string ProductType,
    int DisplayOrder,
    string Status,
    int VariantCount,
    int ActiveVariantCount,
    DateTimeOffset UpdatedAtUtc);

public sealed record ProductDetailResponse(
    Guid Id,
    Guid CategoryId,
    string CategoryArabicName,
    string CategoryEnglishName,
    string ArabicName,
    string EnglishName,
    string? ArabicDescription,
    string? EnglishDescription,
    string Sku,
    string ProductType,
    int DisplayOrder,
    string Status,
    IReadOnlyList<ProductVariantResponse> Variants,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record ProductListResponse(
    IReadOnlyList<ProductSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record CatalogOptionResponse(
    string Code,
    string EnglishLabel,
    string ArabicLabel);

public sealed record CategoryOptionResponse(
    Guid Id,
    string ArabicName,
    string EnglishName);

public sealed record ProductOptionsResponse(
    IReadOnlyList<CategoryOptionResponse> Categories,
    IReadOnlyList<CatalogOptionResponse> ProductTypes,
    IReadOnlyList<CatalogOptionResponse> SizeCodes,
    IReadOnlyList<CatalogOptionResponse> TemperatureCodes,
    IReadOnlyList<CatalogOptionResponse> ServingFormatCodes);

public sealed record VersionedCategory(CategoryDetailResponse Category, long Version);
public sealed record VersionedProduct(ProductDetailResponse Product, long Version);

public sealed record CatalogOperationResult(
    bool Succeeded,
    Guid EntityId,
    long Version,
    string? ErrorCode,
    long? CurrentVersion,
    string? CollectionEtag,
    int? ActiveProductCount)
{
    public static CatalogOperationResult Success(Guid entityId, long version, string? collectionEtag = null)
        => new(true, entityId, version, null, null, collectionEtag, null);

    public static CatalogOperationResult Failure(
        string code,
        long? currentVersion = null,
        string? collectionEtag = null,
        int? activeProductCount = null)
        => new(false, Guid.Empty, 0, code, currentVersion, collectionEtag, activeProductCount);
}
