using System.Text.Json;
using Kalm.Api.Configuration;
using Kalm.Api.Features.CatalogAdministration;
using Kalm.Audit.Application;
using Kalm.Audit.Domain;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Catalog.Domain;
using Kalm.Catalog.Domain.ValueObjects;
using Kalm.Catalog.Infrastructure.Persistence;
using Kalm.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Kalm.Api.Transactions;

public sealed class CatalogAdministrationAuditTransactionCoordinator
{
    private readonly string _connectionString;
    private readonly IClock _clock;

    public CatalogAdministrationAuditTransactionCoordinator(IOptions<DatabaseOptions> database, IClock clock)
    {
        _connectionString = database.Value.ConnectionString;
        _clock = clock;
    }

    public async Task<CatalogOperationResult> CreateCategoryAsync(
        Guid organizationId,
        Guid actorId,
        CategoryWriteRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        CategoryInput? input = ParseCategory(request);
        if (input is null) return CatalogOperationResult.Failure("catalog.validation_failed");
        try
        {
            return await ExecuteAsync(async (catalog, audit, now, token) =>
            {
                await AcquireLockAsync(catalog, $"catalog-category-names:{organizationId:D}", token);
                if (await CategoryNameReservedAsync(catalog, organizationId, null, input.Name, token))
                    return CatalogOperationResult.Failure("catalog.category_name_reserved");
                Category category = Category.Create(
                    Guid.NewGuid(),
                    organizationId,
                    input.Name,
                    input.DisplayOrder,
                    input.PosColorToken,
                    input.IconCode,
                    now);
                catalog.Categories.Add(category);
                await AppendAuditAsync(
                    audit, now, organizationId, actorId, AuditAction.CategoryCreated,
                    "Category", category.Id, null, CategoryState(category), null, correlationId, token);
                return CatalogOperationResult.Success(category.Id, category.Version);
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return CatalogOperationResult.Failure("catalog.category_name_reserved");
        }
    }

    public async Task<CatalogOperationResult> UpdateCategoryAsync(
        Guid organizationId,
        Guid actorId,
        Guid categoryId,
        long expectedVersion,
        CategoryWriteRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        CategoryInput? input = ParseCategory(request);
        if (input is null) return CatalogOperationResult.Failure("catalog.validation_failed");
        try
        {
            return await ExecuteAsync(async (catalog, audit, now, token) =>
            {
                await AcquireCategoryLockAsync(catalog, organizationId, categoryId, token);
                Category? category = await FindCategoryAsync(catalog, organizationId, categoryId, token);
                if (category is null) return CatalogOperationResult.Failure("catalog.category_not_found");
                if (category.Version != expectedVersion)
                    return CatalogOperationResult.Failure("catalog.concurrency_conflict", category.Version);
                await AcquireLockAsync(catalog, $"catalog-category-names:{organizationId:D}", token);
                if (await CategoryNameReservedAsync(catalog, organizationId, categoryId, input.Name, token))
                    return CatalogOperationResult.Failure("catalog.category_name_reserved");
                string before = CategoryState(category);
                string[] changedFields = CategoryChangedFields(category, input);
                if (!category.Update(
                    input.Name,
                    input.DisplayOrder,
                    input.PosColorToken,
                    input.IconCode,
                    now))
                    return CatalogOperationResult.Success(category.Id, category.Version);
                await AppendAuditAsync(
                    audit, now, organizationId, actorId, AuditAction.CategoryUpdated,
                    "Category", category.Id, before,
                    JsonSerializer.Serialize(new { category = CategoryStateObject(category), changedFields }),
                    null, correlationId, token);
                return CatalogOperationResult.Success(category.Id, category.Version);
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return CatalogOperationResult.Failure("catalog.category_name_reserved");
        }
    }

    public Task<CatalogOperationResult> ActivateCategoryAsync(
        Guid organizationId,
        Guid actorId,
        Guid categoryId,
        long expectedVersion,
        string correlationId,
        CancellationToken cancellationToken)
        => ChangeCategoryStatusAsync(
            organizationId, actorId, categoryId, expectedVersion, CatalogItemStatus.Active, correlationId, cancellationToken);

    public Task<CatalogOperationResult> ArchiveCategoryAsync(
        Guid organizationId,
        Guid actorId,
        Guid categoryId,
        long expectedVersion,
        string correlationId,
        CancellationToken cancellationToken)
        => ChangeCategoryStatusAsync(
            organizationId, actorId, categoryId, expectedVersion, CatalogItemStatus.Archived, correlationId, cancellationToken);

    public async Task<CatalogOperationResult> ReorderCategoriesAsync(
        Guid organizationId,
        Guid actorId,
        string expectedCollectionEtag,
        CategoryOrderRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (request.CategoryIds is null
            || request.CategoryIds.Count == 0
            || request.CategoryIds.Count > 500
            || request.CategoryIds.Distinct().Count() != request.CategoryIds.Count)
            return CatalogOperationResult.Failure("catalog.invalid_order");
        return await ExecuteAsync(async (catalog, audit, now, token) =>
        {
            await AcquireLockAsync(catalog, $"catalog-category-order:{organizationId:D}", token);
            Category[] categories = await catalog.Categories
                .Where(category => category.OrganizationId == organizationId)
                .OrderBy(category => category.Id)
                .ToArrayAsync(token);
            string currentEtag = CatalogAdministrationQueries.CollectionEtag(
                categories.Select(category => $"{category.Id:D}:{category.Version}"));
            if (!string.Equals(expectedCollectionEtag, currentEtag, StringComparison.Ordinal))
                return CatalogOperationResult.Failure("catalog.concurrency_conflict", collectionEtag: currentEtag);
            if (categories.Length != request.CategoryIds.Count
                || request.CategoryIds.Any(id => categories.All(category => category.Id != id)))
                return CatalogOperationResult.Failure("catalog.invalid_order");
            bool changed = false;
            int order = 0;
            foreach (Guid id in request.CategoryIds)
            {
                changed |= categories.Single(category => category.Id == id).SetDisplayOrder(order++, now);
            }

            if (!changed)
                return CatalogOperationResult.Success(Guid.Empty, 0, currentEtag);
            string newEtag = CatalogAdministrationQueries.CollectionEtag(
                categories.OrderBy(category => category.Id).Select(category => $"{category.Id:D}:{category.Version}"));
            await AppendAuditAsync(
                audit, now, organizationId, actorId, AuditAction.CategoriesReordered,
                "Category", null, null, JsonSerializer.Serialize(new { affectedCount = categories.Length }),
                null, correlationId, token);
            return CatalogOperationResult.Success(Guid.Empty, 0, newEtag);
        }, cancellationToken);
    }

    public async Task<CatalogOperationResult> CreateProductAsync(
        Guid organizationId,
        Guid actorId,
        ProductWriteRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ProductInput? input = ParseProduct(request, creating: true);
        if (input is null) return CatalogOperationResult.Failure("catalog.validation_failed");
        try
        {
            return await ExecuteAsync(async (catalog, audit, now, token) =>
            {
                await AcquireCategoryLockAsync(catalog, organizationId, input.CategoryId, token);
                await AcquireLockAsync(catalog, $"catalog-codes:{organizationId:D}", token);
                Category? category = await FindCategoryAsync(catalog, organizationId, input.CategoryId, token);
                if (category is null) return CatalogOperationResult.Failure("catalog.category_not_found");
                if (category.Status != CatalogItemStatus.Active)
                    return CatalogOperationResult.Failure("catalog.active_category_required");
                if (await ProductCodesReservedAsync(catalog, organizationId, null, input, token))
                    return CatalogOperationResult.Failure("catalog.code_reserved");
                Product product = Product.Create(
                    Guid.NewGuid(),
                    organizationId,
                    input.CategoryId,
                    input.Name,
                    input.ArabicDescription,
                    input.EnglishDescription,
                    input.Sku,
                    input.Type,
                    input.DisplayOrder,
                    input.Variants.Select(variant => variant.Draft with { Id = Guid.NewGuid() }).ToArray(),
                    now);
                catalog.Products.Add(product);
                await AppendAuditAsync(
                    audit, now, organizationId, actorId, AuditAction.ProductCreated,
                    "Product", product.Id, null, ProductState(product), null, correlationId, token);
                foreach (ProductVariant variant in product.Variants)
                {
                    await AppendAuditAsync(
                        audit, now, organizationId, actorId, AuditAction.VariantCreated,
                        "ProductVariant", variant.Id, null, VariantState(variant), null, correlationId, token);
                }

                return CatalogOperationResult.Success(product.Id, product.Version);
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return CatalogOperationResult.Failure("catalog.code_reserved");
        }
    }

    public async Task<CatalogOperationResult> UpdateProductAsync(
        Guid organizationId,
        Guid actorId,
        Guid productId,
        long expectedVersion,
        ProductWriteRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ProductInput? input = ParseProduct(request, creating: false);
        if (input is null) return CatalogOperationResult.Failure("catalog.validation_failed");
        try
        {
            return await ExecuteAsync(async (catalog, audit, now, token) =>
            {
                await AcquireProductLockAsync(catalog, organizationId, productId, token);
                Product? product = await catalog.Products
                    .Include(candidate => candidate.Variants)
                    .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == productId, token);
                if (product is null) return CatalogOperationResult.Failure("catalog.product_not_found");
                if (product.Version != expectedVersion)
                    return CatalogOperationResult.Failure("catalog.concurrency_conflict", product.Version);
                foreach (Guid categoryId in new[] { product.CategoryId, input.CategoryId }.Distinct().Order())
                    await AcquireCategoryLockAsync(catalog, organizationId, categoryId, token);
                Category? category = await FindCategoryAsync(catalog, organizationId, input.CategoryId, token);
                if (category is null) return CatalogOperationResult.Failure("catalog.category_not_found");
                if (product.Status == CatalogItemStatus.Active && category.Status != CatalogItemStatus.Active)
                    return CatalogOperationResult.Failure("catalog.active_category_required");
                if (input.Variants.Any(variant => variant.Id is Guid id && product.Variants.All(candidate => candidate.Id != id)))
                    return CatalogOperationResult.Failure("catalog.variant_not_found");
                if (input.VariantOrder is not null && input.Variants.Any(variant => variant.Id is null))
                    return CatalogOperationResult.Failure("catalog.invalid_order");
                if (input.VariantOrder is not null
                    && (input.VariantOrder.Count != product.Variants.Count
                        || input.VariantOrder.Distinct().Count() != product.Variants.Count
                        || input.VariantOrder.Any(id => product.Variants.All(variant => variant.Id != id))))
                {
                    return CatalogOperationResult.Failure("catalog.invalid_order");
                }

                if (product.Status == CatalogItemStatus.Active)
                {
                    int projectedActiveVariantCount = product.Variants.Count(variant =>
                        (input.Variants.SingleOrDefault(candidate => candidate.Id == variant.Id)?.Status
                            ?? variant.Status) == CatalogItemStatus.Active);
                    projectedActiveVariantCount += input.Variants.Count(variant =>
                        variant.Id is null && variant.Status == CatalogItemStatus.Active);
                    if (projectedActiveVariantCount == 0)
                    {
                        await AppendRejectedAsync(
                            audit, now, organizationId, actorId, "Product", product.Id,
                            "catalog.active_variant_required", correlationId, token);
                        return CatalogOperationResult.Failure("catalog.active_variant_required");
                    }
                }

                await AcquireLockAsync(catalog, $"catalog-codes:{organizationId:D}", token);
                if (await ProductCodesReservedAsync(catalog, organizationId, productId, input, token))
                    return CatalogOperationResult.Failure("catalog.code_reserved");

                string before = ProductState(product);
                string[] changedFields = ProductChangedFields(product, input);
                bool detailsChanged = product.UpdateDetails(
                    input.CategoryId,
                    input.Name,
                    input.ArabicDescription,
                    input.EnglishDescription,
                    input.Sku,
                    input.Type,
                    input.DisplayOrder,
                    now);
                if (detailsChanged)
                {
                    await AppendAuditAsync(
                        audit, now, organizationId, actorId, AuditAction.ProductUpdated,
                        "Product", product.Id, before,
                        JsonSerializer.Serialize(new { product = ProductStateObject(product), changedFields }),
                        null, correlationId, token);
                }

                foreach (VariantInput variantInput in input.Variants)
                {
                    if (variantInput.Id is null)
                    {
                        ProductVariant created = product.AddVariant(
                            variantInput.Draft with { Id = Guid.NewGuid() },
                            now);
                        catalog.ProductVariants.Add(created);
                        await AppendAuditAsync(
                            audit, now, organizationId, actorId, AuditAction.VariantCreated,
                            "ProductVariant", created.Id, null, VariantState(created), null, correlationId, token);
                        continue;
                    }

                    ProductVariant variant = product.Variants.Single(candidate => candidate.Id == variantInput.Id);
                    string variantBefore = VariantState(variant);
                    string[] variantChangedFields = VariantChangedFields(variant, variantInput);
                    bool variantUpdated = product.UpdateVariant(variant.Id, variantInput.Draft with { Id = variant.Id }, now);
                    if (variantUpdated)
                    {
                        await AppendAuditAsync(
                            audit, now, organizationId, actorId, AuditAction.VariantUpdated,
                            "ProductVariant", variant.Id, variantBefore,
                            JsonSerializer.Serialize(new { variant = VariantStateObject(variant), changedFields = variantChangedFields }),
                            null, correlationId, token);
                    }

                    if (variant.Status != variantInput.Status)
                    {
                        string previousStatus = CatalogAdministrationQueries.Code(variant.Status);
                        try
                        {
                            product.ChangeVariantStatus(variant.Id, variantInput.Status, now);
                        }
                        catch (InvalidOperationException)
                        {
                            await AppendRejectedAsync(
                                audit, now, organizationId, actorId, "Product", product.Id,
                                "catalog.active_variant_required", correlationId, token);
                            return CatalogOperationResult.Failure("catalog.active_variant_required");
                        }

                        await AppendAuditAsync(
                            audit, now, organizationId, actorId,
                            variantInput.Status == CatalogItemStatus.Active ? AuditAction.VariantActivated : AuditAction.VariantArchived,
                            "ProductVariant", variant.Id,
                            JsonSerializer.Serialize(new { status = previousStatus }),
                            JsonSerializer.Serialize(new { status = CatalogAdministrationQueries.Code(variant.Status), productId = product.Id }),
                            null, correlationId, token);
                    }
                }

                if (input.VariantOrder is not null)
                {
                    try
                    {
                        if (product.ReorderVariants(input.VariantOrder, now))
                        {
                            await AppendAuditAsync(
                                audit, now, organizationId, actorId, AuditAction.VariantsReordered,
                                "Product", product.Id, null,
                                JsonSerializer.Serialize(new { affectedCount = product.Variants.Count }),
                                null, correlationId, token);
                        }
                    }
                    catch (ArgumentException)
                    {
                        return CatalogOperationResult.Failure("catalog.invalid_order");
                    }
                }

                return CatalogOperationResult.Success(product.Id, product.Version);
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return CatalogOperationResult.Failure("catalog.code_reserved");
        }
    }

    public Task<CatalogOperationResult> ActivateProductAsync(
        Guid organizationId,
        Guid actorId,
        Guid productId,
        long expectedVersion,
        string correlationId,
        CancellationToken cancellationToken)
        => ChangeProductStatusAsync(
            organizationId, actorId, productId, expectedVersion, CatalogItemStatus.Active, correlationId, cancellationToken);

    public Task<CatalogOperationResult> ArchiveProductAsync(
        Guid organizationId,
        Guid actorId,
        Guid productId,
        long expectedVersion,
        string correlationId,
        CancellationToken cancellationToken)
        => ChangeProductStatusAsync(
            organizationId, actorId, productId, expectedVersion, CatalogItemStatus.Archived, correlationId, cancellationToken);

    private Task<CatalogOperationResult> ChangeCategoryStatusAsync(
        Guid organizationId,
        Guid actorId,
        Guid categoryId,
        long expectedVersion,
        CatalogItemStatus status,
        string correlationId,
        CancellationToken cancellationToken)
        => ExecuteAsync(async (catalog, audit, now, token) =>
        {
            await AcquireCategoryLockAsync(catalog, organizationId, categoryId, token);
            Category? category = await FindCategoryAsync(catalog, organizationId, categoryId, token);
            if (category is null) return CatalogOperationResult.Failure("catalog.category_not_found");
            if (category.Version != expectedVersion)
                return CatalogOperationResult.Failure("catalog.concurrency_conflict", category.Version);
            if (category.Status == status) return CatalogOperationResult.Success(category.Id, category.Version);
            if (status == CatalogItemStatus.Archived)
            {
                int activeProducts = await catalog.Products.CountAsync(
                    product => product.OrganizationId == organizationId
                        && product.CategoryId == categoryId
                        && product.Status == CatalogItemStatus.Active,
                    token);
                if (activeProducts > 0)
                {
                    await AppendRejectedAsync(
                        audit, now, organizationId, actorId, "Category", category.Id,
                        "catalog.category_has_active_products", correlationId, token,
                        new { activeProductCount = activeProducts });
                    return CatalogOperationResult.Failure(
                        "catalog.category_has_active_products", activeProductCount: activeProducts);
                }
            }

            string previousStatus = CatalogAdministrationQueries.Code(category.Status);
            _ = status == CatalogItemStatus.Active ? category.Activate(now) : category.Archive(now);
            await AppendAuditAsync(
                audit, now, organizationId, actorId,
                status == CatalogItemStatus.Active ? AuditAction.CategoryActivated : AuditAction.CategoryArchived,
                "Category", category.Id,
                JsonSerializer.Serialize(new { status = previousStatus }),
                JsonSerializer.Serialize(new { status = CatalogAdministrationQueries.Code(category.Status) }),
                null, correlationId, token);
            return CatalogOperationResult.Success(category.Id, category.Version);
        }, cancellationToken);

    private Task<CatalogOperationResult> ChangeProductStatusAsync(
        Guid organizationId,
        Guid actorId,
        Guid productId,
        long expectedVersion,
        CatalogItemStatus status,
        string correlationId,
        CancellationToken cancellationToken)
        => ExecuteAsync(async (catalog, audit, now, token) =>
        {
            await AcquireProductLockAsync(catalog, organizationId, productId, token);
            Product? product = await catalog.Products
                .Include(candidate => candidate.Variants)
                .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId && candidate.Id == productId, token);
            if (product is null) return CatalogOperationResult.Failure("catalog.product_not_found");
            if (product.Version != expectedVersion)
                return CatalogOperationResult.Failure("catalog.concurrency_conflict", product.Version);
            if (product.Status == status) return CatalogOperationResult.Success(product.Id, product.Version);
            await AcquireCategoryLockAsync(catalog, organizationId, product.CategoryId, token);
            Category? category = await FindCategoryAsync(catalog, organizationId, product.CategoryId, token);
            if (category is null) return CatalogOperationResult.Failure("catalog.category_not_found");
            string previousStatus = CatalogAdministrationQueries.Code(product.Status);
            if (status == CatalogItemStatus.Active)
            {
                try
                {
                    product.Activate(category.Status == CatalogItemStatus.Active, now);
                }
                catch (InvalidOperationException)
                {
                    string code = category.Status == CatalogItemStatus.Active
                        ? "catalog.active_variant_required"
                        : "catalog.active_category_required";
                    await AppendRejectedAsync(
                        audit, now, organizationId, actorId, "Product", product.Id, code, correlationId, token);
                    return CatalogOperationResult.Failure(code);
                }
            }
            else
            {
                product.Archive(now);
            }

            await AppendAuditAsync(
                audit, now, organizationId, actorId,
                status == CatalogItemStatus.Active ? AuditAction.ProductActivated : AuditAction.ProductArchived,
                "Product", product.Id,
                JsonSerializer.Serialize(new { status = previousStatus }),
                JsonSerializer.Serialize(new { status = CatalogAdministrationQueries.Code(product.Status) }),
                null, correlationId, token);
            return CatalogOperationResult.Success(product.Id, product.Version);
        }, cancellationToken);

    private async Task<T> ExecuteAsync<T>(
        Func<CatalogDbContext, IAuditWriter, DateTimeOffset, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var catalog = new CatalogDbContext(
            new DbContextOptionsBuilder<CatalogDbContext>()
                .UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "catalog"))
                .Options);
        await using var auditContext = new AuditDbContext(
            new DbContextOptionsBuilder<AuditDbContext>()
                .UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "audit"))
                .Options);
        await catalog.Database.UseTransactionAsync(transaction, cancellationToken);
        await auditContext.Database.UseTransactionAsync(transaction, cancellationToken);
        try
        {
            T result = await operation(catalog, new AuditWriter(auditContext), _clock.UtcNow, cancellationToken);
            await catalog.SaveChangesAsync(cancellationToken);
            await auditContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static CategoryInput? ParseCategory(CategoryWriteRequest request)
    {
        try
        {
            return new CategoryInput(
                new LocalizedCatalogName(request.ArabicName, request.EnglishName),
                request.DisplayOrder,
                CatalogPresentationOptions.OptionalCode(request.PosColorToken, CatalogPresentationOptions.ColorTokens, nameof(request.PosColorToken)),
                CatalogPresentationOptions.OptionalCode(request.IconCode, CatalogPresentationOptions.IconCodes, nameof(request.IconCode)));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static ProductInput? ParseProduct(ProductWriteRequest request, bool creating)
    {
        try
        {
            if (request.Variants is null || request.Variants.Count > 100 || (creating && request.Variants.Count == 0))
                return null;
            if (request.Variants.Where(variant => variant.Id.HasValue).Select(variant => variant.Id).Distinct().Count()
                != request.Variants.Count(variant => variant.Id.HasValue))
                return null;
            if (creating && request.Variants.Any(variant => variant.Id is not null || ParseStatus(variant.Status) != CatalogItemStatus.Active))
                return null;
            var variants = new List<VariantInput>(request.Variants.Count);
            foreach (VariantWriteRequest variant in request.Variants)
            {
                variants.Add(new VariantInput(
                    variant.Id,
                    new ProductVariantDraft(
                        variant.Id ?? Guid.Empty,
                        new LocalizedCatalogName(variant.ArabicName, variant.EnglishName),
                        new CatalogCode(variant.Code),
                        string.IsNullOrWhiteSpace(variant.Barcode) ? null : new BarcodeValue(variant.Barcode),
                        variant.SizeCode,
                        variant.TemperatureCode,
                        variant.ServingFormatCode,
                        variant.DisplayOrder),
                    ParseStatus(variant.Status)));
            }

            if (variants.Select(variant => variant.Draft.Code.Value).Distinct(StringComparer.Ordinal).Count() != variants.Count)
                return null;
            string?[] barcodes = variants.Select(variant => variant.Draft.Barcode?.Value).Where(value => value is not null).ToArray();
            if (barcodes.Distinct(StringComparer.Ordinal).Count() != barcodes.Length)
                return null;
            if (request.VariantOrder is not null
                && (request.VariantOrder.Count > 100
                    || request.VariantOrder.Distinct().Count() != request.VariantOrder.Count))
                return null;
            return new ProductInput(
                request.CategoryId,
                new LocalizedCatalogName(request.ArabicName, request.EnglishName),
                request.ArabicDescription,
                request.EnglishDescription,
                new CatalogCode(request.Sku),
                CatalogAdministrationQueries.ParseProductType(request.ProductType),
                request.DisplayOrder,
                variants,
                request.VariantOrder);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (CatalogQueryException)
        {
            return null;
        }
    }

    private static CatalogItemStatus ParseStatus(string? value)
        => string.IsNullOrWhiteSpace(value) ? CatalogItemStatus.Active : CatalogAdministrationQueries.ParseStatus(value);

    private static Task<Category?> FindCategoryAsync(
        CatalogDbContext catalog,
        Guid organizationId,
        Guid categoryId,
        CancellationToken cancellationToken)
        => catalog.Categories.SingleOrDefaultAsync(
            category => category.OrganizationId == organizationId && category.Id == categoryId,
            cancellationToken);

    private static async Task<bool> CategoryNameReservedAsync(
        CatalogDbContext catalog,
        Guid organizationId,
        Guid? excludedId,
        LocalizedCatalogName name,
        CancellationToken cancellationToken)
        => await catalog.Categories.AnyAsync(
            category => category.OrganizationId == organizationId
                && category.Id != excludedId
                && (category.NormalizedArabicName == name.NormalizedArabic
                    || category.NormalizedEnglishName == name.NormalizedEnglish),
            cancellationToken);

    private static async Task<bool> ProductCodesReservedAsync(
        CatalogDbContext catalog,
        Guid organizationId,
        Guid? productId,
        ProductInput input,
        CancellationToken cancellationToken)
    {
        if (await catalog.Products.AnyAsync(
            product => product.OrganizationId == organizationId
                && product.Id != productId
                && product.Sku == input.Sku.Value,
            cancellationToken))
            return true;
        string[] codes = input.Variants.Select(variant => variant.Draft.Code.Value).Distinct().ToArray();
        var codeOwners = await catalog.ProductVariants
            .Where(variant => variant.OrganizationId == organizationId && codes.Contains(variant.Code))
            .Select(variant => new { variant.Id, variant.Code })
            .ToArrayAsync(cancellationToken);
        if (input.Variants.Any(inputVariant =>
            codeOwners.Any(owner => owner.Code == inputVariant.Draft.Code.Value && owner.Id != inputVariant.Id)))
            return true;
        string[] barcodes = input.Variants
            .Where(variant => variant.Draft.Barcode is not null)
            .Select(variant => variant.Draft.Barcode!.Value)
            .Distinct()
            .ToArray();
        var barcodeOwners = await catalog.ProductVariants
            .Where(variant => variant.OrganizationId == organizationId
                && variant.Barcode != null
                && barcodes.Contains(variant.Barcode))
            .Select(variant => new { variant.Id, variant.Barcode })
            .ToArrayAsync(cancellationToken);
        return input.Variants.Any(inputVariant =>
            inputVariant.Draft.Barcode is not null
            && barcodeOwners.Any(owner =>
                owner.Barcode == inputVariant.Draft.Barcode.Value && owner.Id != inputVariant.Id));
    }

    private static Task AcquireCategoryLockAsync(
        CatalogDbContext catalog,
        Guid organizationId,
        Guid categoryId,
        CancellationToken cancellationToken)
        => AcquireLockAsync(catalog, $"catalog-category:{organizationId:D}:{categoryId:D}", cancellationToken);

    private static Task AcquireProductLockAsync(
        CatalogDbContext catalog,
        Guid organizationId,
        Guid productId,
        CancellationToken cancellationToken)
        => AcquireLockAsync(catalog, $"catalog-product:{organizationId:D}:{productId:D}", cancellationToken);

    private static async Task AcquireLockAsync(
        CatalogDbContext catalog,
        string key,
        CancellationToken cancellationToken)
    {
        await catalog.Database.ExecuteSqlInterpolatedAsync(
            $"select pg_advisory_xact_lock(hashtextextended({key}, 0))",
            cancellationToken);
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static string[] CategoryChangedFields(Category category, CategoryInput input)
    {
        var fields = new List<string>();
        if (category.ArabicName != input.Name.Arabic) fields.Add("arabicName");
        if (category.EnglishName != input.Name.English) fields.Add("englishName");
        if (category.DisplayOrder != input.DisplayOrder) fields.Add("displayOrder");
        if (category.PosColorToken != input.PosColorToken) fields.Add("posColorToken");
        if (category.IconCode != input.IconCode) fields.Add("iconCode");
        return fields.ToArray();
    }

    private static string[] ProductChangedFields(Product product, ProductInput input)
    {
        var fields = new List<string>();
        if (product.CategoryId != input.CategoryId) fields.Add("categoryId");
        if (product.ArabicName != input.Name.Arabic) fields.Add("arabicName");
        if (product.EnglishName != input.Name.English) fields.Add("englishName");
        if (product.ArabicDescription != OptionalDescription(input.ArabicDescription)) fields.Add("arabicDescription");
        if (product.EnglishDescription != OptionalDescription(input.EnglishDescription)) fields.Add("englishDescription");
        if (product.Sku != input.Sku.Value) fields.Add("sku");
        if (product.Type != input.Type) fields.Add("productType");
        if (product.DisplayOrder != input.DisplayOrder) fields.Add("displayOrder");
        return fields.ToArray();
    }

    private static string[] VariantChangedFields(ProductVariant variant, VariantInput input)
    {
        var fields = new List<string>();
        if (variant.ArabicName != input.Draft.Name.Arabic) fields.Add("arabicName");
        if (variant.EnglishName != input.Draft.Name.English) fields.Add("englishName");
        if (variant.Code != input.Draft.Code.Value) fields.Add("code");
        if (variant.Barcode != input.Draft.Barcode?.Value) fields.Add("barcode");
        if (variant.SizeCode != input.Draft.SizeCode) fields.Add("sizeCode");
        if (variant.TemperatureCode != input.Draft.TemperatureCode) fields.Add("temperatureCode");
        if (variant.ServingFormatCode != input.Draft.ServingFormatCode) fields.Add("servingFormatCode");
        if (variant.DisplayOrder != input.Draft.DisplayOrder) fields.Add("displayOrder");
        return fields.ToArray();
    }

    private static string? OptionalDescription(string? value)
    {
        string normalized = CatalogText.Display(value, nameof(value), 1000, required: false);
        return normalized.Length == 0 ? null : normalized;
    }

    private static string CategoryState(Category category) => JsonSerializer.Serialize(CategoryStateObject(category));
    private static object CategoryStateObject(Category category) => new
    {
        categoryId = category.Id,
        category.ArabicName,
        category.EnglishName,
        category.DisplayOrder,
        category.PosColorToken,
        category.IconCode,
        status = CatalogAdministrationQueries.Code(category.Status)
    };

    private static string ProductState(Product product) => JsonSerializer.Serialize(ProductStateObject(product));
    private static object ProductStateObject(Product product) => new
    {
        productId = product.Id,
        product.CategoryId,
        product.ArabicName,
        product.EnglishName,
        product.Sku,
        productType = CatalogAdministrationQueries.Code(product.Type),
        product.DisplayOrder,
        status = CatalogAdministrationQueries.Code(product.Status),
        variantCount = product.Variants.Count
    };

    private static string VariantState(ProductVariant variant) => JsonSerializer.Serialize(VariantStateObject(variant));
    private static object VariantStateObject(ProductVariant variant) => new
    {
        variantId = variant.Id,
        variant.ProductId,
        variant.ArabicName,
        variant.EnglishName,
        variant.Code,
        variant.Barcode,
        variant.SizeCode,
        variant.TemperatureCode,
        variant.ServingFormatCode,
        variant.DisplayOrder,
        status = CatalogAdministrationQueries.Code(variant.Status)
    };

    private static Task AppendRejectedAsync(
        IAuditWriter audit,
        DateTimeOffset now,
        Guid organizationId,
        Guid actorId,
        string entityType,
        Guid? entityId,
        string reasonCode,
        string correlationId,
        CancellationToken cancellationToken,
        object? safeMetadata = null)
        => AppendAuditAsync(
            audit, now, organizationId, actorId, AuditAction.CatalogMutationRejected,
            entityType, entityId, null,
            safeMetadata is null ? null : JsonSerializer.Serialize(safeMetadata),
            reasonCode, correlationId, cancellationToken);

    private static Task AppendAuditAsync(
        IAuditWriter audit,
        DateTimeOffset now,
        Guid organizationId,
        Guid actorId,
        AuditAction action,
        string entityType,
        Guid? entityId,
        string? before,
        string? after,
        string? reasonCode,
        string correlationId,
        CancellationToken cancellationToken)
        => audit.AppendAsync(
            new AuditWriteRequest(
                Guid.NewGuid(),
                now,
                organizationId,
                null,
                null,
                actorId,
                AuditActorType.User,
                null,
                action,
                entityType,
                entityId,
                reasonCode is null ? AuditResult.Succeeded : AuditResult.Denied,
                reasonCode,
                correlationId,
                before,
                after,
                null,
                null),
            cancellationToken);

    private sealed record CategoryInput(
        LocalizedCatalogName Name,
        int DisplayOrder,
        string? PosColorToken,
        string? IconCode);

    private sealed record ProductInput(
        Guid CategoryId,
        LocalizedCatalogName Name,
        string? ArabicDescription,
        string? EnglishDescription,
        CatalogCode Sku,
        ProductType Type,
        int DisplayOrder,
        IReadOnlyList<VariantInput> Variants,
        IReadOnlyList<Guid>? VariantOrder);

    private sealed record VariantInput(
        Guid? Id,
        ProductVariantDraft Draft,
        CatalogItemStatus Status);
}
