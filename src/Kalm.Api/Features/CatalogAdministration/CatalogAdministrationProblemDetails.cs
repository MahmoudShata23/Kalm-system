namespace Kalm.Api.Features.CatalogAdministration;

internal static class CatalogAdministrationProblemDetails
{
    public static IResult Create(
        string code,
        long? currentVersion = null,
        string? currentCollectionEtag = null,
        int? activeProductCount = null)
    {
        (int status, string title, string detail) = code switch
        {
            "catalog.category_not_found" => (404, "Category not found", "The requested category was not found."),
            "catalog.product_not_found" => (404, "Product not found", "The requested product was not found."),
            "catalog.variant_not_found" => (404, "Variant not found", "The requested variant was not found."),
            "catalog.validation_failed" => (422, "Catalog request invalid", "The catalog request contains invalid values."),
            "catalog.invalid_query" => (422, "Catalog query invalid", "The catalog query parameters are invalid."),
            "catalog.invalid_order" => (422, "Catalog order invalid", "Ordering must contain every required identifier exactly once."),
            "catalog.category_name_reserved" => (409, "Category name unavailable", "The normalized category name is already reserved."),
            "catalog.code_reserved" => (409, "Catalog code unavailable", "A normalized SKU, variant code, or barcode is already reserved."),
            "catalog.category_has_active_products" => (409, "Category has active products", "Archive active products before archiving this category."),
            "catalog.active_category_required" => (409, "Active category required", "An active product must belong to an active category."),
            "catalog.active_variant_required" => (409, "Active variant required", "An active product must retain at least one active variant."),
            "catalog.precondition_required" => (428, "Precondition required", "A current strong If-Match value is required."),
            "catalog.invalid_precondition" => (400, "Invalid precondition", "If-Match must contain one exact strong catalog ETag."),
            "catalog.concurrency_conflict" => (412, "Catalog item changed", "The catalog item or ordering changed in another request."),
            "catalog.rate_limited" => (429, "Too many requests", "Wait before trying this catalog mutation again."),
            "auth.csrf_invalid" => (400, "Antiforgery validation failed", "The antiforgery token is missing or invalid."),
            _ => (500, "Catalog administration failed", "The operation could not be completed.")
        };

        Dictionary<string, object?> extensions = new() { ["code"] = code };
        if (currentVersion is not null)
            extensions["currentEtag"] = CatalogAdministrationEndpoints.FormatEtag(currentVersion.Value);
        if (currentCollectionEtag is not null)
            extensions["currentEtag"] = currentCollectionEtag;
        if (activeProductCount is not null)
            extensions["dependencyCounts"] = new { activeProductCount };
        return Results.Problem(statusCode: status, title: title, detail: detail, extensions: extensions);
    }
}
