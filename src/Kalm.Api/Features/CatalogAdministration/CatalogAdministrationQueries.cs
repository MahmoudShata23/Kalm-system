using System.Security.Cryptography;
using System.Text;
using Kalm.Catalog.Domain;
using Kalm.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kalm.Api.Features.CatalogAdministration;

public sealed class CatalogAdministrationQueries(CatalogDbContext catalog)
{
    public async Task<(CategoryListResponse Response, string CollectionEtag)> ListCategoriesAsync(
        Guid organizationId,
        string status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ValidatePaging(page, pageSize);
        ValidateSearch(search);
        IQueryable<Category> query = catalog.Categories.AsNoTracking()
            .Where(category => category.OrganizationId == organizationId);
        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(category => category.Status == ParseStatus(status));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            query = query.Where(category =>
                EF.Functions.ILike(category.ArabicName, pattern)
                || EF.Functions.ILike(category.EnglishName, pattern));
        }

        int total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.EnglishName)
            .ThenBy(category => category.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(category => new
            {
                category.Id,
                category.ArabicName,
                category.EnglishName,
                category.DisplayOrder,
                category.Status,
                category.PosColorToken,
                category.IconCode,
                category.UpdatedAtUtc
            })
            .ToArrayAsync(cancellationToken);
        CategorySummaryResponse[] items = rows
            .Select(category => new CategorySummaryResponse(
                category.Id,
                category.ArabicName,
                category.EnglishName,
                category.DisplayOrder,
                Code(category.Status),
                category.PosColorToken,
                category.IconCode,
                category.UpdatedAtUtc))
            .ToArray();
        string etag = await CategoryCollectionEtagAsync(organizationId, cancellationToken);
        return (new CategoryListResponse(items, page, pageSize, total), etag);
    }

    public async Task<VersionedCategory?> GetCategoryAsync(
        Guid organizationId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var result = await catalog.Categories.AsNoTracking()
            .Where(category => category.OrganizationId == organizationId && category.Id == categoryId)
            .Select(category => new
            {
                category.Id,
                category.ArabicName,
                category.EnglishName,
                category.DisplayOrder,
                category.Status,
                category.PosColorToken,
                category.IconCode,
                category.CreatedAtUtc,
                category.UpdatedAtUtc,
                category.Version
            })
            .SingleOrDefaultAsync(cancellationToken);
        return result is null
            ? null
            : new VersionedCategory(
                new CategoryDetailResponse(
                    result.Id,
                    result.ArabicName,
                    result.EnglishName,
                    result.DisplayOrder,
                    Code(result.Status),
                    result.PosColorToken,
                    result.IconCode,
                    result.CreatedAtUtc,
                    result.UpdatedAtUtc),
                result.Version);
    }

    public async Task<ProductListResponse> ListProductsAsync(
        Guid organizationId,
        string status,
        string? search,
        Guid? categoryId,
        string? productType,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ValidatePaging(page, pageSize);
        ValidateSearch(search);
        CatalogItemStatus? parsedStatus = string.Equals(status, "all", StringComparison.OrdinalIgnoreCase)
            ? null
            : ParseStatus(status);
        ProductType? parsedType = string.IsNullOrWhiteSpace(productType) ? null : ParseProductType(productType);
        IQueryable<Product> query = catalog.Products.AsNoTracking()
            .Where(product => product.OrganizationId == organizationId);
        if (parsedStatus is not null) query = query.Where(product => product.Status == parsedStatus);
        if (categoryId is not null) query = query.Where(product => product.CategoryId == categoryId);
        if (parsedType is not null) query = query.Where(product => product.Type == parsedType);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            query = query.Where(product =>
                EF.Functions.ILike(product.ArabicName, pattern)
                || EF.Functions.ILike(product.EnglishName, pattern)
                || EF.Functions.ILike(product.Sku, pattern)
                || product.Variants.Any(variant => variant.Barcode != null && EF.Functions.ILike(variant.Barcode, pattern)));
        }

        int total = await query.CountAsync(cancellationToken);
        var rows = await (
            from product in query
            join category in catalog.Categories.AsNoTracking()
                on new { product.CategoryId, product.OrganizationId } equals new { CategoryId = category.Id, category.OrganizationId }
            orderby product.DisplayOrder, product.EnglishName, product.Id
            select new
            {
                product.Id,
                CategoryId = category.Id,
                CategoryArabicName = category.ArabicName,
                CategoryEnglishName = category.EnglishName,
                ProductArabicName = product.ArabicName,
                ProductEnglishName = product.EnglishName,
                product.Sku,
                product.Type,
                product.DisplayOrder,
                product.Status,
                VariantCount = product.Variants.Count,
                ActiveVariantCount = product.Variants.Count(variant => variant.Status == CatalogItemStatus.Active),
                product.UpdatedAtUtc
            })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        ProductSummaryResponse[] items = rows
            .Select(product => new ProductSummaryResponse(
                product.Id,
                product.CategoryId,
                product.CategoryArabicName,
                product.CategoryEnglishName,
                product.ProductArabicName,
                product.ProductEnglishName,
                product.Sku,
                Code(product.Type),
                product.DisplayOrder,
                Code(product.Status),
                product.VariantCount,
                product.ActiveVariantCount,
                product.UpdatedAtUtc))
            .ToArray();
        return new ProductListResponse(items, page, pageSize, total);
    }

    public async Task<VersionedProduct?> GetProductAsync(
        Guid organizationId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var product = await (
            from candidate in catalog.Products.AsNoTracking()
            join category in catalog.Categories.AsNoTracking()
                on new { candidate.CategoryId, candidate.OrganizationId } equals new { CategoryId = category.Id, category.OrganizationId }
            where candidate.OrganizationId == organizationId && candidate.Id == productId
            select new
            {
                Product = candidate,
                CategoryArabicName = category.ArabicName,
                CategoryEnglishName = category.EnglishName
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (product is null) return null;
        var variantRows = await catalog.ProductVariants.AsNoTracking()
            .Where(variant => variant.OrganizationId == organizationId && variant.ProductId == productId)
            .OrderBy(variant => variant.DisplayOrder)
            .ThenBy(variant => variant.EnglishName)
            .ThenBy(variant => variant.Id)
            .Select(variant => new
            {
                variant.Id,
                variant.ArabicName,
                variant.EnglishName,
                variant.Code,
                variant.Barcode,
                variant.SizeCode,
                variant.TemperatureCode,
                variant.ServingFormatCode,
                variant.DisplayOrder,
                variant.Status,
                variant.CreatedAtUtc,
                variant.UpdatedAtUtc
            })
            .ToArrayAsync(cancellationToken);
        ProductVariantResponse[] variants = variantRows
            .Select(variant => new ProductVariantResponse(
                variant.Id,
                variant.ArabicName,
                variant.EnglishName,
                variant.Code,
                variant.Barcode,
                variant.SizeCode,
                variant.TemperatureCode,
                variant.ServingFormatCode,
                variant.DisplayOrder,
                Code(variant.Status),
                variant.CreatedAtUtc,
                variant.UpdatedAtUtc))
            .ToArray();
        Product item = product.Product;
        return new VersionedProduct(
            new ProductDetailResponse(
                item.Id,
                item.CategoryId,
                product.CategoryArabicName,
                product.CategoryEnglishName,
                item.ArabicName,
                item.EnglishName,
                item.ArabicDescription,
                item.EnglishDescription,
                item.Sku,
                Code(item.Type),
                item.DisplayOrder,
                Code(item.Status),
                variants,
                item.CreatedAtUtc,
                item.UpdatedAtUtc),
            item.Version);
    }

    public async Task<ProductOptionsResponse> OptionsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        CategoryOptionResponse[] categories = await catalog.Categories.AsNoTracking()
            .Where(category => category.OrganizationId == organizationId && category.Status == CatalogItemStatus.Active)
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.EnglishName)
            .ThenBy(category => category.Id)
            .Take(500)
            .Select(category => new CategoryOptionResponse(category.Id, category.ArabicName, category.EnglishName))
            .ToArrayAsync(cancellationToken);
        return new ProductOptionsResponse(
            categories,
            [
                new("madeToOrder", "Made to order", "يُحضّر عند الطلب"),
                new("purchasedFinishedGood", "Purchased finished good", "منتج نهائي مشتَرى"),
                new("serviceNonStock", "Service / non-stock", "خدمة / غير مخزني")
            ],
            Options(CatalogPresentationOptions.SizeCodes, SizeLabels),
            Options(CatalogPresentationOptions.TemperatureCodes, TemperatureLabels),
            Options(CatalogPresentationOptions.ServingFormatCodes, ServingLabels));
    }

    public async Task<string> CategoryCollectionEtagAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        string[] components = await catalog.Categories.AsNoTracking()
            .Where(category => category.OrganizationId == organizationId)
            .OrderBy(category => category.Id)
            .Select(category => $"{category.Id:D}:{category.Version}")
            .ToArrayAsync(cancellationToken);
        return CollectionEtag(components);
    }

    internal static string CollectionEtag(IEnumerable<string> components)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', components)));
        return $"\"c-{Convert.ToHexString(hash).ToLowerInvariant()}\"";
    }

    internal static CatalogItemStatus ParseStatus(string value)
        => TryCode(value, out CatalogItemStatus status) ? status : throw new CatalogQueryException("catalog.invalid_query");

    internal static ProductType ParseProductType(string value)
        => TryCode(value, out ProductType type) ? type : throw new CatalogQueryException("catalog.invalid_query");

    internal static string Code<T>(T value) where T : struct, Enum
    {
        string name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static bool TryCode<T>(string value, out T result) where T : struct, Enum
    {
        foreach (T candidate in Enum.GetValues<T>())
        {
            if (string.Equals(Code(candidate), value, StringComparison.OrdinalIgnoreCase))
            {
                result = candidate;
                return true;
            }
        }

        result = default;
        return false;
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        if (page is < 1 or > 100_000 || pageSize is < 1 or > 100)
            throw new CatalogQueryException("catalog.invalid_query");
    }

    private static void ValidateSearch(string? search)
    {
        if (search?.Trim().Length > 120)
            throw new CatalogQueryException("catalog.invalid_query");
    }

    private static CatalogOptionResponse[] Options(
        IReadOnlySet<string> values,
        IReadOnlyDictionary<string, (string English, string Arabic)> labels)
        => values.Order(StringComparer.Ordinal)
            .Select(code => new CatalogOptionResponse(code, labels[code].English, labels[code].Arabic))
            .ToArray();

    private static readonly IReadOnlyDictionary<string, (string English, string Arabic)> SizeLabels =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["single"] = ("Single", "مفرد"),
            ["double"] = ("Double", "مزدوج"),
            ["small"] = ("Small", "صغير"),
            ["medium"] = ("Medium", "وسط"),
            ["large"] = ("Large", "كبير")
        };

    private static readonly IReadOnlyDictionary<string, (string English, string Arabic)> TemperatureLabels =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["hot"] = ("Hot", "ساخن"),
            ["iced"] = ("Iced", "مثلج"),
            ["ambient"] = ("Ambient", "درجة الغرفة")
        };

    private static readonly IReadOnlyDictionary<string, (string English, string Arabic)> ServingLabels =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["cup"] = ("Cup", "كوب"),
            ["mug"] = ("Mug", "قدح"),
            ["glass"] = ("Glass", "كأس"),
            ["bottle"] = ("Bottle", "زجاجة"),
            ["can"] = ("Can", "عبوة"),
            ["plate"] = ("Plate", "طبق"),
            ["piece"] = ("Piece", "قطعة")
        };
}

public sealed class CatalogQueryException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
