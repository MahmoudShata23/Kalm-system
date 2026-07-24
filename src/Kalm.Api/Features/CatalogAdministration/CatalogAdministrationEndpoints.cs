using Kalm.Api.Features.Authentication;
using Kalm.Api.Features.Authorization;
using Kalm.Api.Transactions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Kalm.Api.Features.CatalogAdministration;

public static class CatalogAdministrationEndpoints
{
    public const string WriteRateLimitPolicy = "catalog-administration-write";

    public static IEndpointRouteBuilder MapCatalogAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder categories = endpoints.MapGroup("/api/v1/management/catalog/categories")
            .WithTags("Catalog Administration");
        categories.MapGet("", ListCategoriesAsync)
            .WithName("ListManagementCatalogCategories")
            .RequireAuthorization(KalmPolicies.CatalogAdministrationView)
            .Produces<CategoryListResponse>()
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(422);
        categories.MapGet("/{categoryId:guid}", GetCategoryAsync)
            .WithName("GetManagementCatalogCategory")
            .RequireAuthorization(KalmPolicies.CatalogAdministrationView)
            .Produces<CategoryDetailResponse>()
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        categories.MapPost("", CreateCategoryAsync)
            .WithName("CreateManagementCatalogCategory")
            .RequireAuthorization(KalmPolicies.CatalogAdministrationManage)
            .RequireRateLimiting(WriteRateLimitPolicy)
            .Produces<CategoryDetailResponse>(201)
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(422).ProducesProblem(429);
        categories.MapPut("/{categoryId:guid}", UpdateCategoryAsync)
            .WithName("UpdateManagementCatalogCategory")
            .RequireAuthorization(KalmPolicies.CatalogAdministrationManage)
            .RequireRateLimiting(WriteRateLimitPolicy)
            .Produces<CategoryDetailResponse>()
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409)
            .ProducesProblem(412).ProducesProblem(422).ProducesProblem(428).ProducesProblem(429);
        categories.MapPost("/{categoryId:guid}/activate", ActivateCategoryAsync)
            .WithName("ActivateManagementCatalogCategory")
            .RequireAuthorization(KalmPolicies.CatalogAdministrationManage)
            .RequireRateLimiting(WriteRateLimitPolicy)
            .Produces<CategoryDetailResponse>()
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(412).ProducesProblem(428).ProducesProblem(429);
        categories.MapPost("/{categoryId:guid}/archive", ArchiveCategoryAsync)
            .WithName("ArchiveManagementCatalogCategory")
            .RequireAuthorization(KalmPolicies.CatalogAdministrationManage)
            .RequireRateLimiting(WriteRateLimitPolicy)
            .Produces<CategoryDetailResponse>()
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409)
            .ProducesProblem(412).ProducesProblem(428).ProducesProblem(429);
        categories.MapPut("/order", ReorderCategoriesAsync)
            .WithName("ReorderManagementCatalogCategories")
            .RequireAuthorization(KalmPolicies.CatalogAdministrationManage)
            .RequireRateLimiting(WriteRateLimitPolicy)
            .Produces(204)
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(412).ProducesProblem(422).ProducesProblem(428).ProducesProblem(429);

        RouteGroupBuilder products = endpoints.MapGroup("/api/v1/management/catalog/products")
            .WithTags("Catalog Administration");
        products.MapGet("", ListProductsAsync)
            .WithName("ListManagementCatalogProducts")
            .RequireAuthorization(KalmPolicies.CatalogAdministrationView)
            .Produces<ProductListResponse>()
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(422);
        products.MapGet("/options", ProductOptionsAsync)
            .WithName("GetManagementCatalogProductOptions")
            .RequireAuthorization(KalmPolicies.CatalogAdministrationView)
            .Produces<ProductOptionsResponse>()
            .ProducesProblem(401).ProducesProblem(403);
        products.MapGet("/{productId:guid}", GetProductAsync)
            .WithName("GetManagementCatalogProduct")
            .RequireAuthorization(KalmPolicies.CatalogAdministrationView)
            .Produces<ProductDetailResponse>()
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        products.MapPost("", CreateProductAsync)
            .WithName("CreateManagementCatalogProduct")
            .RequireAuthorization(KalmPolicies.CatalogAdministrationManage)
            .RequireRateLimiting(WriteRateLimitPolicy)
            .Produces<ProductDetailResponse>(201)
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409).ProducesProblem(422).ProducesProblem(429);
        products.MapPut("/{productId:guid}", UpdateProductAsync)
            .WithName("UpdateManagementCatalogProduct")
            .RequireAuthorization(KalmPolicies.CatalogAdministrationManage)
            .RequireRateLimiting(WriteRateLimitPolicy)
            .Produces<ProductDetailResponse>()
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409)
            .ProducesProblem(412).ProducesProblem(422).ProducesProblem(428).ProducesProblem(429);
        products.MapPost("/{productId:guid}/activate", ActivateProductAsync)
            .WithName("ActivateManagementCatalogProduct")
            .RequireAuthorization(KalmPolicies.CatalogAdministrationManage)
            .RequireRateLimiting(WriteRateLimitPolicy)
            .Produces<ProductDetailResponse>()
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409)
            .ProducesProblem(412).ProducesProblem(428).ProducesProblem(429);
        products.MapPost("/{productId:guid}/archive", ArchiveProductAsync)
            .WithName("ArchiveManagementCatalogProduct")
            .RequireAuthorization(KalmPolicies.CatalogAdministrationManage)
            .RequireRateLimiting(WriteRateLimitPolicy)
            .Produces<ProductDetailResponse>()
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404)
            .ProducesProblem(412).ProducesProblem(428).ProducesProblem(429);

        return endpoints;
    }

    private static async Task<IResult> ListCategoriesAsync(
        HttpContext context,
        CatalogAdministrationQueries queries,
        string status = "all",
        string? search = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            (CategoryListResponse response, string etag) = await queries.ListCategoriesAsync(
                Session(context).OrganizationId, status, search, page, pageSize, cancellationToken);
            SetEtag(context, etag);
            return Results.Ok(response);
        }
        catch (CatalogQueryException exception)
        {
            return CatalogAdministrationProblemDetails.Create(exception.Code);
        }
    }

    private static async Task<IResult> GetCategoryAsync(
        Guid categoryId,
        HttpContext context,
        CatalogAdministrationQueries queries,
        CancellationToken cancellationToken)
    {
        VersionedCategory? detail = await queries.GetCategoryAsync(
            Session(context).OrganizationId, categoryId, cancellationToken);
        return detail is null
            ? CatalogAdministrationProblemDetails.Create("catalog.category_not_found")
            : VersionedCategoryResponse(context, detail);
    }

    private static async Task<IResult> CreateCategoryAsync(
        CategoryWriteRequest request,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrf,
        HttpContext context,
        IAntiforgery antiforgery,
        CatalogAdministrationAuditTransactionCoordinator coordinator,
        CatalogAdministrationQueries queries,
        CancellationToken cancellationToken)
    {
        _ = csrf;
        if (!await ValidCsrfAsync(context, antiforgery))
            return CatalogAdministrationProblemDetails.Create("auth.csrf_invalid");
        ManagementSessionSnapshot session = Session(context);
        CatalogOperationResult result = await coordinator.CreateCategoryAsync(
            session.OrganizationId, session.UserId, request, context.TraceIdentifier, cancellationToken);
        if (!result.Succeeded) return Problem(result);
        VersionedCategory detail = (await queries.GetCategoryAsync(session.OrganizationId, result.EntityId, cancellationToken))!;
        string location = $"/api/v1/management/catalog/categories/{result.EntityId:D}";
        SetEtag(context, FormatEtag(detail.Version));
        context.Response.Headers.Location = location;
        return Results.Created(location, detail.Category);
    }

    private static async Task<IResult> UpdateCategoryAsync(
        Guid categoryId,
        CategoryWriteRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrf,
        HttpContext context,
        IAntiforgery antiforgery,
        CatalogAdministrationAuditTransactionCoordinator coordinator,
        CatalogAdministrationQueries queries,
        CancellationToken cancellationToken)
    {
        _ = csrf;
        IResult? invalid = ParseVersionEtag(ifMatch, out long version);
        if (invalid is not null) return invalid;
        if (!await ValidCsrfAsync(context, antiforgery))
            return CatalogAdministrationProblemDetails.Create("auth.csrf_invalid");
        ManagementSessionSnapshot session = Session(context);
        CatalogOperationResult result = await coordinator.UpdateCategoryAsync(
            session.OrganizationId, session.UserId, categoryId, version, request, context.TraceIdentifier, cancellationToken);
        return await CategoryMutationResponseAsync(result, session.OrganizationId, categoryId, context, queries, cancellationToken);
    }

    private static Task<IResult> ActivateCategoryAsync(
        Guid categoryId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrf,
        HttpContext context,
        IAntiforgery antiforgery,
        CatalogAdministrationAuditTransactionCoordinator coordinator,
        CatalogAdministrationQueries queries,
        CancellationToken cancellationToken)
        => ChangeCategoryStatusAsync(categoryId, ifMatch, csrf, context, antiforgery, coordinator, queries, true, cancellationToken);

    private static Task<IResult> ArchiveCategoryAsync(
        Guid categoryId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrf,
        HttpContext context,
        IAntiforgery antiforgery,
        CatalogAdministrationAuditTransactionCoordinator coordinator,
        CatalogAdministrationQueries queries,
        CancellationToken cancellationToken)
        => ChangeCategoryStatusAsync(categoryId, ifMatch, csrf, context, antiforgery, coordinator, queries, false, cancellationToken);

    private static async Task<IResult> ChangeCategoryStatusAsync(
        Guid categoryId,
        string? ifMatch,
        string? csrf,
        HttpContext context,
        IAntiforgery antiforgery,
        CatalogAdministrationAuditTransactionCoordinator coordinator,
        CatalogAdministrationQueries queries,
        bool activate,
        CancellationToken cancellationToken)
    {
        _ = csrf;
        IResult? invalid = ParseVersionEtag(ifMatch, out long version);
        if (invalid is not null) return invalid;
        if (!await ValidCsrfAsync(context, antiforgery))
            return CatalogAdministrationProblemDetails.Create("auth.csrf_invalid");
        ManagementSessionSnapshot session = Session(context);
        CatalogOperationResult result = activate
            ? await coordinator.ActivateCategoryAsync(session.OrganizationId, session.UserId, categoryId, version, context.TraceIdentifier, cancellationToken)
            : await coordinator.ArchiveCategoryAsync(session.OrganizationId, session.UserId, categoryId, version, context.TraceIdentifier, cancellationToken);
        return await CategoryMutationResponseAsync(result, session.OrganizationId, categoryId, context, queries, cancellationToken);
    }

    private static async Task<IResult> ReorderCategoriesAsync(
        CategoryOrderRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrf,
        HttpContext context,
        IAntiforgery antiforgery,
        CatalogAdministrationAuditTransactionCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        _ = csrf;
        IResult? invalid = ParseCollectionEtag(ifMatch, out string etag);
        if (invalid is not null) return invalid;
        if (!await ValidCsrfAsync(context, antiforgery))
            return CatalogAdministrationProblemDetails.Create("auth.csrf_invalid");
        ManagementSessionSnapshot session = Session(context);
        CatalogOperationResult result = await coordinator.ReorderCategoriesAsync(
            session.OrganizationId, session.UserId, etag, request, context.TraceIdentifier, cancellationToken);
        if (!result.Succeeded) return Problem(result);
        if (result.CollectionEtag is not null) SetEtag(context, result.CollectionEtag);
        return Results.NoContent();
    }

    private static async Task<IResult> ListProductsAsync(
        HttpContext context,
        CatalogAdministrationQueries queries,
        string status = "all",
        string? search = null,
        Guid? categoryId = null,
        string? productType = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Results.Ok(await queries.ListProductsAsync(
                Session(context).OrganizationId, status, search, categoryId, productType, page, pageSize, cancellationToken));
        }
        catch (CatalogQueryException exception)
        {
            return CatalogAdministrationProblemDetails.Create(exception.Code);
        }
    }

    private static async Task<IResult> ProductOptionsAsync(
        HttpContext context,
        CatalogAdministrationQueries queries,
        CancellationToken cancellationToken)
        => Results.Ok(await queries.OptionsAsync(Session(context).OrganizationId, cancellationToken));

    private static async Task<IResult> GetProductAsync(
        Guid productId,
        HttpContext context,
        CatalogAdministrationQueries queries,
        CancellationToken cancellationToken)
    {
        VersionedProduct? detail = await queries.GetProductAsync(Session(context).OrganizationId, productId, cancellationToken);
        return detail is null
            ? CatalogAdministrationProblemDetails.Create("catalog.product_not_found")
            : VersionedProductResponse(context, detail);
    }

    private static async Task<IResult> CreateProductAsync(
        ProductWriteRequest request,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrf,
        HttpContext context,
        IAntiforgery antiforgery,
        CatalogAdministrationAuditTransactionCoordinator coordinator,
        CatalogAdministrationQueries queries,
        CancellationToken cancellationToken)
    {
        _ = csrf;
        if (!await ValidCsrfAsync(context, antiforgery))
            return CatalogAdministrationProblemDetails.Create("auth.csrf_invalid");
        ManagementSessionSnapshot session = Session(context);
        CatalogOperationResult result = await coordinator.CreateProductAsync(
            session.OrganizationId, session.UserId, request, context.TraceIdentifier, cancellationToken);
        if (!result.Succeeded) return Problem(result);
        VersionedProduct detail = (await queries.GetProductAsync(session.OrganizationId, result.EntityId, cancellationToken))!;
        string location = $"/api/v1/management/catalog/products/{result.EntityId:D}";
        SetEtag(context, FormatEtag(detail.Version));
        context.Response.Headers.Location = location;
        return Results.Created(location, detail.Product);
    }

    private static async Task<IResult> UpdateProductAsync(
        Guid productId,
        ProductWriteRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrf,
        HttpContext context,
        IAntiforgery antiforgery,
        CatalogAdministrationAuditTransactionCoordinator coordinator,
        CatalogAdministrationQueries queries,
        CancellationToken cancellationToken)
    {
        _ = csrf;
        IResult? invalid = ParseVersionEtag(ifMatch, out long version);
        if (invalid is not null) return invalid;
        if (!await ValidCsrfAsync(context, antiforgery))
            return CatalogAdministrationProblemDetails.Create("auth.csrf_invalid");
        ManagementSessionSnapshot session = Session(context);
        CatalogOperationResult result = await coordinator.UpdateProductAsync(
            session.OrganizationId, session.UserId, productId, version, request, context.TraceIdentifier, cancellationToken);
        return await ProductMutationResponseAsync(result, session.OrganizationId, productId, context, queries, cancellationToken);
    }

    private static Task<IResult> ActivateProductAsync(
        Guid productId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrf,
        HttpContext context,
        IAntiforgery antiforgery,
        CatalogAdministrationAuditTransactionCoordinator coordinator,
        CatalogAdministrationQueries queries,
        CancellationToken cancellationToken)
        => ChangeProductStatusAsync(productId, ifMatch, csrf, context, antiforgery, coordinator, queries, true, cancellationToken);

    private static Task<IResult> ArchiveProductAsync(
        Guid productId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrf,
        HttpContext context,
        IAntiforgery antiforgery,
        CatalogAdministrationAuditTransactionCoordinator coordinator,
        CatalogAdministrationQueries queries,
        CancellationToken cancellationToken)
        => ChangeProductStatusAsync(productId, ifMatch, csrf, context, antiforgery, coordinator, queries, false, cancellationToken);

    private static async Task<IResult> ChangeProductStatusAsync(
        Guid productId,
        string? ifMatch,
        string? csrf,
        HttpContext context,
        IAntiforgery antiforgery,
        CatalogAdministrationAuditTransactionCoordinator coordinator,
        CatalogAdministrationQueries queries,
        bool activate,
        CancellationToken cancellationToken)
    {
        _ = csrf;
        IResult? invalid = ParseVersionEtag(ifMatch, out long version);
        if (invalid is not null) return invalid;
        if (!await ValidCsrfAsync(context, antiforgery))
            return CatalogAdministrationProblemDetails.Create("auth.csrf_invalid");
        ManagementSessionSnapshot session = Session(context);
        CatalogOperationResult result = activate
            ? await coordinator.ActivateProductAsync(session.OrganizationId, session.UserId, productId, version, context.TraceIdentifier, cancellationToken)
            : await coordinator.ArchiveProductAsync(session.OrganizationId, session.UserId, productId, version, context.TraceIdentifier, cancellationToken);
        return await ProductMutationResponseAsync(result, session.OrganizationId, productId, context, queries, cancellationToken);
    }

    private static async Task<IResult> CategoryMutationResponseAsync(
        CatalogOperationResult result,
        Guid organizationId,
        Guid categoryId,
        HttpContext context,
        CatalogAdministrationQueries queries,
        CancellationToken cancellationToken)
    {
        if (!result.Succeeded) return Problem(result);
        VersionedCategory detail = (await queries.GetCategoryAsync(organizationId, categoryId, cancellationToken))!;
        return VersionedCategoryResponse(context, detail);
    }

    private static async Task<IResult> ProductMutationResponseAsync(
        CatalogOperationResult result,
        Guid organizationId,
        Guid productId,
        HttpContext context,
        CatalogAdministrationQueries queries,
        CancellationToken cancellationToken)
    {
        if (!result.Succeeded) return Problem(result);
        VersionedProduct detail = (await queries.GetProductAsync(organizationId, productId, cancellationToken))!;
        return VersionedProductResponse(context, detail);
    }

    private static IResult VersionedCategoryResponse(HttpContext context, VersionedCategory detail)
    {
        SetEtag(context, FormatEtag(detail.Version));
        return Results.Ok(detail.Category);
    }

    private static IResult VersionedProductResponse(HttpContext context, VersionedProduct detail)
    {
        SetEtag(context, FormatEtag(detail.Version));
        return Results.Ok(detail.Product);
    }

    private static IResult Problem(CatalogOperationResult result)
        => CatalogAdministrationProblemDetails.Create(
            result.ErrorCode!, result.CurrentVersion, result.CollectionEtag, result.ActiveProductCount);

    private static ManagementSessionSnapshot Session(HttpContext context)
        => context.Items[ManagementAuthenticationConstants.SessionItemKey] as ManagementSessionSnapshot
            ?? throw new InvalidOperationException("Authoritative management session unavailable.");

    private static void SetEtag(HttpContext context, string etag)
    {
        context.Response.Headers.ETag = etag;
        context.Response.Headers.CacheControl = "no-store";
    }

    internal static string FormatEtag(long version) => $"\"{version}\"";

    private static IResult? ParseVersionEtag(string? value, out long version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(value))
            return CatalogAdministrationProblemDetails.Create("catalog.precondition_required");
        if (value.Length < 3
            || value[0] != '"'
            || value[^1] != '"'
            || value.Contains(',', StringComparison.Ordinal)
            || value == "*"
            || value.StartsWith("W/", StringComparison.OrdinalIgnoreCase)
            || !long.TryParse(value.AsSpan(1, value.Length - 2), out version)
            || version < 1
            || value != FormatEtag(version))
            return CatalogAdministrationProblemDetails.Create("catalog.invalid_precondition");
        return null;
    }

    private static IResult? ParseCollectionEtag(string? value, out string etag)
    {
        etag = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return CatalogAdministrationProblemDetails.Create("catalog.precondition_required");
        if (value.Length != 68
            || !value.StartsWith("\"c-", StringComparison.Ordinal)
            || value[^1] != '"'
            || !IsLowerHex(value.AsSpan(3, 64)))
            return CatalogAdministrationProblemDetails.Create("catalog.invalid_precondition");
        etag = value;
        return null;
    }

    private static bool IsLowerHex(ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
                return false;
        }

        return true;
    }

    private static async Task<bool> ValidCsrfAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }
}
