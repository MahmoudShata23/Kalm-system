using Kalm.Api.Configuration;
using Kalm.Api.Features.CatalogAdministration;
using Kalm.Api.Transactions;
using Kalm.Audit.Domain;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Catalog.Domain;
using Kalm.Catalog.Infrastructure.Persistence;
using Kalm.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Kalm.Api.IntegrationTests;

public sealed class CatalogAdministrationTransactionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CategoryLifecycleOrderingNoOpAndOrganizationIsolation_AreEnforced()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        Guid organizationId = Guid.NewGuid();
        Guid otherOrganizationId = Guid.NewGuid();
        CatalogAdministrationAuditTransactionCoordinator coordinator = Coordinator(database.ConnectionString);

        CatalogOperationResult drinks = await coordinator.CreateCategoryAsync(
            organizationId, Guid.NewGuid(), Category(" مشروبات ", " Drinks ", 4), "category-drinks", CancellationToken.None);
        CatalogOperationResult food = await coordinator.CreateCategoryAsync(
            organizationId, Guid.NewGuid(), Category("طعام", "Food", 8), "category-food", CancellationToken.None);
        CatalogOperationResult isolatedName = await coordinator.CreateCategoryAsync(
            otherOrganizationId, Guid.NewGuid(), Category("مشروبات", "drinks", 0), "category-isolated", CancellationToken.None);
        Assert.True(drinks.Succeeded);
        Assert.True(food.Succeeded);
        Assert.True(isolatedName.Succeeded);

        CatalogOperationResult duplicate = await coordinator.CreateCategoryAsync(
            organizationId, Guid.NewGuid(), Category("أخرى", "DRINKS", 10), "category-duplicate", CancellationToken.None);
        Assert.Equal("catalog.category_name_reserved", duplicate.ErrorCode);

        CatalogOperationResult noOp = await coordinator.UpdateCategoryAsync(
            organizationId, Guid.NewGuid(), drinks.EntityId, drinks.Version,
            Category("مشروبات", "Drinks", 4), "category-no-op", CancellationToken.None);
        Assert.Equal(drinks.Version, noOp.Version);

        var queries = new CatalogAdministrationQueries(Catalog(database.ConnectionString));
        (CategoryListResponse before, string etag) = await queries.ListCategoriesAsync(
            organizationId, "all", null, 1, 100, CancellationToken.None);
        Assert.Equal(2, before.TotalCount);
        Assert.Null(await queries.GetCategoryAsync(otherOrganizationId, drinks.EntityId, CancellationToken.None));

        CatalogOperationResult invalidOrder = await coordinator.ReorderCategoriesAsync(
            organizationId, Guid.NewGuid(), etag, new CategoryOrderRequest([drinks.EntityId]), "category-invalid-order", CancellationToken.None);
        Assert.Equal("catalog.invalid_order", invalidOrder.ErrorCode);
        CatalogOperationResult reordered = await coordinator.ReorderCategoriesAsync(
            organizationId, Guid.NewGuid(), etag, new CategoryOrderRequest([food.EntityId, drinks.EntityId]), "category-order", CancellationToken.None);
        Assert.True(reordered.Succeeded);
        Assert.NotEqual(etag, reordered.CollectionEtag);

        CatalogOperationResult staleOrder = await coordinator.ReorderCategoriesAsync(
            organizationId, Guid.NewGuid(), etag, new CategoryOrderRequest([drinks.EntityId, food.EntityId]), "category-stale-order", CancellationToken.None);
        Assert.Equal("catalog.concurrency_conflict", staleOrder.ErrorCode);
        Assert.Equal(reordered.CollectionEtag, staleOrder.CollectionEtag);

        await using var audit = Audit(database.ConnectionString);
        Assert.Equal(2, await audit.AuditEntries.CountAsync(entry =>
            entry.OrganizationId == organizationId && entry.Action == AuditAction.CategoryCreated));
        Assert.Single(await audit.AuditEntries.Where(entry => entry.Action == AuditAction.CategoriesReordered).ToArrayAsync());
        Assert.DoesNotContain(await audit.AuditEntries.ToArrayAsync(), entry => entry.CorrelationId == "category-no-op");
    }

    [Fact]
    public async Task ProductAggregate_PreservesOmittedVariantsAndReservesArchivedCodes()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        Guid organizationId = Guid.NewGuid();
        CatalogAdministrationAuditTransactionCoordinator coordinator = Coordinator(database.ConnectionString);
        CatalogOperationResult category = await coordinator.CreateCategoryAsync(
            organizationId, Guid.NewGuid(), Category("قهوة", "Coffee", 0), "category", CancellationToken.None);

        CatalogOperationResult created = await coordinator.CreateProductAsync(
            organizationId, Guid.NewGuid(),
            Product(category.EntityId, "لاتيه", "Latte", "LATTE", [Variant("LATTE-M", "622100000001")]),
            "product-create", CancellationToken.None);
        Assert.True(created.Succeeded);
        var queries = new CatalogAdministrationQueries(Catalog(database.ConnectionString));
        VersionedProduct detail = (await queries.GetProductAsync(organizationId, created.EntityId, CancellationToken.None))!;
        Guid firstVariantId = detail.Product.Variants.Single().Id;

        CatalogOperationResult noOp = await coordinator.UpdateProductAsync(
            organizationId, Guid.NewGuid(), created.EntityId, detail.Version,
            Product(category.EntityId, "لاتيه", "Latte", "LATTE", []),
            "product-no-op", CancellationToken.None);
        Assert.Equal(detail.Version, noOp.Version);
        Assert.Single((await queries.GetProductAsync(organizationId, created.EntityId, CancellationToken.None))!.Product.Variants);

        CatalogOperationResult finalActiveRejected = await coordinator.UpdateProductAsync(
            organizationId, Guid.NewGuid(), created.EntityId, noOp.Version,
            Product(category.EntityId, "لاتيه", "Latte", "LATTE",
                [Variant("LATTE-M", "622100000001", firstVariantId, "archived")]),
            "variant-final-active", CancellationToken.None);
        Assert.Equal("catalog.active_variant_required", finalActiveRejected.ErrorCode);

        CatalogOperationResult added = await coordinator.UpdateProductAsync(
            organizationId, Guid.NewGuid(), created.EntityId, noOp.Version,
            Product(category.EntityId, "لاتيه", "Latte", "LATTE",
                [Variant("LATTE-L", "622100000002")]),
            "variant-add", CancellationToken.None);
        Assert.True(added.Succeeded);
        VersionedProduct twoVariants = (await queries.GetProductAsync(organizationId, created.EntityId, CancellationToken.None))!;
        Assert.Equal(2, twoVariants.Product.Variants.Count);

        CatalogOperationResult archivedProduct = await coordinator.ArchiveProductAsync(
            organizationId, Guid.NewGuid(), created.EntityId, twoVariants.Version, "product-archive", CancellationToken.None);
        Assert.True(archivedProduct.Succeeded);
        CatalogOperationResult duplicateSku = await coordinator.CreateProductAsync(
            organizationId, Guid.NewGuid(),
            Product(category.EntityId, "آخر", "Other", "latte", [Variant("OTHER-V", "622100000003")]),
            "duplicate-sku", CancellationToken.None);
        Assert.Equal("catalog.code_reserved", duplicateSku.ErrorCode);
        CatalogOperationResult duplicateVariant = await coordinator.CreateProductAsync(
            organizationId, Guid.NewGuid(),
            Product(category.EntityId, "آخر", "Other", "OTHER", [Variant("latte-m", "622100000004")]),
            "duplicate-variant", CancellationToken.None);
        Assert.Equal("catalog.code_reserved", duplicateVariant.ErrorCode);
        CatalogOperationResult duplicateBarcode = await coordinator.CreateProductAsync(
            organizationId, Guid.NewGuid(),
            Product(category.EntityId, "آخر", "Other", "OTHER-2", [Variant("OTHER-V2", "622100000001")]),
            "duplicate-barcode", CancellationToken.None);
        Assert.Equal("catalog.code_reserved", duplicateBarcode.ErrorCode);
    }

    [Fact]
    public async Task CategoryArchiveAndProductCreation_UseTheSameInvariantLock()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        Guid organizationId = Guid.NewGuid();
        CatalogAdministrationAuditTransactionCoordinator coordinator = Coordinator(database.ConnectionString);
        CatalogOperationResult category = await coordinator.CreateCategoryAsync(
            organizationId, Guid.NewGuid(), Category("حلويات", "Desserts", 0), "category", CancellationToken.None);

        Task<CatalogOperationResult> archiveTask = coordinator.ArchiveCategoryAsync(
            organizationId, Guid.NewGuid(), category.EntityId, category.Version, "archive", CancellationToken.None);
        Task<CatalogOperationResult> productTask = coordinator.CreateProductAsync(
            organizationId, Guid.NewGuid(),
            Product(category.EntityId, "كيك", "Cake", "CAKE", [Variant("CAKE-ONE", null)]),
            "product", CancellationToken.None);
        await Task.WhenAll(archiveTask, productTask);

        CatalogOperationResult archive = await archiveTask;
        CatalogOperationResult product = await productTask;
        Assert.False(archive.Succeeded && product.Succeeded);
        Assert.True(
            (archive.Succeeded && product.ErrorCode == "catalog.active_category_required")
            || (product.Succeeded && archive.ErrorCode == "catalog.category_has_active_products"));
    }

    [Fact]
    public async Task ProductActivationAndFinalVariantArchive_CannotBothCommit()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        Guid organizationId = Guid.NewGuid();
        CatalogAdministrationAuditTransactionCoordinator coordinator = Coordinator(database.ConnectionString);
        CatalogOperationResult category = await coordinator.CreateCategoryAsync(
            organizationId, Guid.NewGuid(), Category("خدمات", "Services", 0), "category", CancellationToken.None);
        CatalogOperationResult product = await coordinator.CreateProductAsync(
            organizationId, Guid.NewGuid(),
            Product(category.EntityId, "خدمة", "Service", "SERVICE", [Variant("SERVICE-ONE", null)]),
            "product", CancellationToken.None);
        CatalogOperationResult archived = await coordinator.ArchiveProductAsync(
            organizationId, Guid.NewGuid(), product.EntityId, product.Version, "archive", CancellationToken.None);
        var queries = new CatalogAdministrationQueries(Catalog(database.ConnectionString));
        VersionedProduct detail = (await queries.GetProductAsync(organizationId, product.EntityId, CancellationToken.None))!;
        Guid variantId = detail.Product.Variants.Single().Id;

        Task<CatalogOperationResult> activateTask = coordinator.ActivateProductAsync(
            organizationId, Guid.NewGuid(), product.EntityId, archived.Version, "activate", CancellationToken.None);
        Task<CatalogOperationResult> variantArchiveTask = coordinator.UpdateProductAsync(
            organizationId, Guid.NewGuid(), product.EntityId, archived.Version,
            Product(category.EntityId, "خدمة", "Service", "SERVICE",
                [Variant("SERVICE-ONE", null, variantId, "archived")]),
            "variant-archive", CancellationToken.None);
        await Task.WhenAll(activateTask, variantArchiveTask);

        Assert.False((await activateTask).Succeeded && (await variantArchiveTask).Succeeded);
        VersionedProduct stored = (await queries.GetProductAsync(organizationId, product.EntityId, CancellationToken.None))!;
        Assert.False(
            stored.Product.Status == "active"
            && stored.Product.Variants.All(value => value.Status == "archived"));
    }

    [Fact]
    public async Task QueriesAreBoundedStableAndCatalogMutationRollsBackWhenAuditFails()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        Guid organizationId = Guid.NewGuid();
        CatalogAdministrationAuditTransactionCoordinator coordinator = Coordinator(database.ConnectionString);
        CatalogOperationResult category = await coordinator.CreateCategoryAsync(
            organizationId, Guid.NewGuid(), Category("مشروبات", "Drinks", 0), "category", CancellationToken.None);
        CatalogOperationResult first = await coordinator.CreateProductAsync(
            organizationId, Guid.NewGuid(),
            Product(category.EntityId, "أ", "Alpha", "ALPHA", [Variant("ALPHA-V", "622100000010")], 0),
            "first", CancellationToken.None);
        CatalogOperationResult second = await coordinator.CreateProductAsync(
            organizationId, Guid.NewGuid(),
            Product(category.EntityId, "ب", "Beta", "BETA", [Variant("BETA-V", "622100000011")], 0),
            "second", CancellationToken.None);
        Assert.True(first.Succeeded && second.Succeeded);

        var queries = new CatalogAdministrationQueries(Catalog(database.ConnectionString));
        ProductListResponse page = await queries.ListProductsAsync(
            organizationId, "active", "62210000001", category.EntityId, "madeToOrder", 1, 1, CancellationToken.None);
        Assert.Equal(2, page.TotalCount);
        Assert.Single(page.Items);
        Assert.Equal("Alpha", page.Items[0].EnglishName);
        await Assert.ThrowsAsync<CatalogQueryException>(() => queries.ListProductsAsync(
            organizationId, "all", null, null, null, 1, 101, CancellationToken.None));

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await new NpgsqlCommand(
                "create function audit.reject_catalog_success() returns trigger language plpgsql as $$ begin if new.action = 'ProductCreated' then raise exception 'forced catalog audit failure'; end if; return new; end; $$; create trigger trg_reject_catalog_success before insert on audit.audit_logs for each row execute function audit.reject_catalog_success();",
                connection).ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<DbUpdateException>(() => coordinator.CreateProductAsync(
            organizationId, Guid.NewGuid(),
            Product(category.EntityId, "ج", "Gamma", "GAMMA", [Variant("GAMMA-V", null)]),
            "rollback", CancellationToken.None));
        await using var catalog = Catalog(database.ConnectionString);
        Assert.False(await catalog.Products.AnyAsync(value => value.Sku == "GAMMA"));
    }

    [Fact]
    public async Task ConcurrentOrganizationCodes_HaveOneWinnerAndArchivedReservationsRemainEffective()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        Guid organizationId = Guid.NewGuid();
        CatalogAdministrationAuditTransactionCoordinator coordinator = Coordinator(database.ConnectionString);
        CatalogOperationResult category = await coordinator.CreateCategoryAsync(
            organizationId, Guid.NewGuid(), Category("مخبوزات", "Bakery", 0), "category", CancellationToken.None);
        ProductWriteRequest request = Product(
            category.EntityId, "كرواسون", "Croissant", "CROISSANT", [Variant("CROISSANT-ONE", "622100000020")]);

        Task<CatalogOperationResult> first = coordinator.CreateProductAsync(
            organizationId, Guid.NewGuid(), request, "code-first", CancellationToken.None);
        Task<CatalogOperationResult> second = coordinator.CreateProductAsync(
            organizationId, Guid.NewGuid(), request, "code-second", CancellationToken.None);
        CatalogOperationResult[] results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => result.ErrorCode == "catalog.code_reserved");
        await using var catalog = Catalog(database.ConnectionString);
        Assert.Single(await catalog.Products.Where(product => product.OrganizationId == organizationId).ToArrayAsync());
        Assert.Single(await catalog.ProductVariants.Where(variant => variant.OrganizationId == organizationId).ToArrayAsync());
    }

    [Fact]
    public async Task DeferredDatabaseTriggers_RejectInactiveCategoryAndFinalActiveVariantForActiveProduct()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        Guid organizationId = Guid.NewGuid();
        CatalogAdministrationAuditTransactionCoordinator coordinator = Coordinator(database.ConnectionString);
        CatalogOperationResult category = await coordinator.CreateCategoryAsync(
            organizationId, Guid.NewGuid(), Category("شاي", "Tea", 0), "category", CancellationToken.None);
        CatalogOperationResult product = await coordinator.CreateProductAsync(
            organizationId, Guid.NewGuid(),
            Product(category.EntityId, "شاي", "Tea", "TEA", [Variant("TEA-ONE", null)]),
            "product", CancellationToken.None);
        var queries = new CatalogAdministrationQueries(Catalog(database.ConnectionString));
        Guid variantId = (await queries.GetProductAsync(organizationId, product.EntityId, CancellationToken.None))!
            .Product.Variants.Single().Id;

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
            await new NpgsqlCommand(
                "update catalog.categories set status = 'Archived' where id = @id",
                connection,
                transaction)
            {
                Parameters = { new("id", category.EntityId) }
            }.ExecuteNonQueryAsync();
            PostgresException exception = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
            Assert.Equal("ck_catalog_active_product_category", exception.ConstraintName);
        }

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
            await new NpgsqlCommand(
                "update catalog.product_variants set status = 'Archived' where id = @id",
                connection,
                transaction)
            {
                Parameters = { new("id", variantId) }
            }.ExecuteNonQueryAsync();
            PostgresException exception = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
            Assert.Equal("ck_catalog_active_product_variant", exception.ConstraintName);
        }
    }

    private static CategoryWriteRequest Category(string arabic, string english, int order)
        => new(arabic, english, order, "sand", "coffee");

    private static ProductWriteRequest Product(
        Guid categoryId,
        string arabic,
        string english,
        string sku,
        IReadOnlyList<VariantWriteRequest> variants,
        int order = 0)
        => new(categoryId, arabic, english, null, null, sku, "madeToOrder", order, variants, null);

    private static VariantWriteRequest Variant(
        string code,
        string? barcode,
        Guid? id = null,
        string status = "active")
        => new(id, "افتراضي", "Default", code, barcode, "medium", "hot", "cup", 0, status);

    private static CatalogAdministrationAuditTransactionCoordinator Coordinator(string connectionString)
        => new(Options.Create(new DatabaseOptions { ConnectionString = connectionString }), new FixedClock(Now));

    private static async Task MigrateAsync(string connectionString)
    {
        await using var catalog = Catalog(connectionString);
        await catalog.Database.MigrateAsync();
        await using var audit = Audit(connectionString);
        await audit.Database.MigrateAsync();
    }

    private static CatalogDbContext Catalog(string connectionString)
        => new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "catalog"))
            .Options);

    private static AuditDbContext Audit(string connectionString)
        => new(new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "audit"))
            .Options);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private const string DefaultAdmin = "Host=localhost;Port=54329;Database=postgres;Username=kalm;Password=kalm_dev_password";
        private readonly string _admin;
        private readonly string _databaseName;

        private TestDatabase(string admin, string connectionString, string databaseName)
        {
            _admin = admin;
            ConnectionString = connectionString;
            _databaseName = databaseName;
        }

        public string ConnectionString { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            string admin = Environment.GetEnvironmentVariable("KALM_TEST_POSTGRES_ADMIN") ?? DefaultAdmin;
            string databaseName = $"kalm_catalog_{Guid.NewGuid():N}";
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await new NpgsqlCommand($"create database \"{databaseName}\"", connection).ExecuteNonQueryAsync();
            return new TestDatabase(
                admin,
                new NpgsqlConnectionStringBuilder(admin) { Database = databaseName }.ConnectionString,
                databaseName);
        }

        public async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_admin) { Pooling = false }.ConnectionString);
            await connection.OpenAsync();
            await new NpgsqlCommand($"drop database if exists \"{_databaseName}\" with (force)", connection).ExecuteNonQueryAsync();
        }
    }
}
