using Kalm.Catalog.Domain;
using Kalm.Catalog.Domain.ValueObjects;

namespace Kalm.UnitTests.Catalog;

public sealed class CatalogDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Category_NormalizesNamesAndValidatesPresentation()
    {
        Category category = Category.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new LocalizedCatalogName("  قهوة   ساخنة ", "  Hot   Coffee "),
            0,
            "espresso",
            "coffee",
            Now);

        Assert.Equal("قهوة ساخنة", category.ArabicName);
        Assert.Equal("Hot Coffee", category.EnglishName);
        Assert.Equal("HOT COFFEE", category.NormalizedEnglishName);
        Assert.Throws<ArgumentException>(() => Category.Create(
            Guid.NewGuid(), Guid.NewGuid(), new LocalizedCatalogName("أ", "A"), 0, "javascript:alert(1)", null, Now));
        Assert.Throws<ArgumentException>(() => Category.Create(
            Guid.NewGuid(), Guid.NewGuid(), new LocalizedCatalogName("أ", "A"), 0, null, "<script>", Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => Category.Create(
            Guid.NewGuid(), Guid.NewGuid(), new LocalizedCatalogName("أ", "A"), -1, null, null, Now));
    }

    [Fact]
    public void Product_RequiresVariantAndPreservesOmittedVariants()
    {
        Guid productId = Guid.NewGuid();
        Guid firstVariantId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => Product.Create(
            productId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new LocalizedCatalogName("لاتيه", "Latte"),
            null,
            null,
            new CatalogCode("LATTE"),
            ProductType.MadeToOrder,
            0,
            [],
            Now));

        Product product = Product.Create(
            productId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new LocalizedCatalogName("لاتيه", "Latte"),
            "وصف",
            "Description",
            new CatalogCode(" latte "),
            ProductType.MadeToOrder,
            0,
            [Draft(firstVariantId, "LATTE-M")],
            Now);
        long version = product.Version;

        Assert.False(product.UpdateDetails(
            product.CategoryId,
            new LocalizedCatalogName("لاتيه", "Latte"),
            "وصف",
            "Description",
            new CatalogCode("LATTE"),
            ProductType.MadeToOrder,
            0,
            Now.AddMinutes(1)));
        Assert.Equal(version, product.Version);
        Assert.Single(product.Variants);
        Assert.Equal(CatalogItemStatus.Active, product.Variants.Single().Status);
    }

    [Fact]
    public void ActiveProduct_CannotArchiveFinalActiveVariantAndReactivationRevalidates()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        Product product = Product.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new LocalizedCatalogName("منتج", "Product"),
            null,
            null,
            new CatalogCode("PRODUCT"),
            ProductType.PurchasedFinishedGood,
            0,
            [Draft(first, "VAR-1"), Draft(second, "VAR-2")],
            Now);

        Assert.True(product.ChangeVariantStatus(first, CatalogItemStatus.Archived, Now.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() =>
            product.ChangeVariantStatus(second, CatalogItemStatus.Archived, Now.AddMinutes(2)));

        Assert.True(product.Archive(Now.AddMinutes(3)));
        Assert.True(product.ChangeVariantStatus(second, CatalogItemStatus.Archived, Now.AddMinutes(4)));
        Assert.Throws<InvalidOperationException>(() => product.Activate(true, Now.AddMinutes(5)));
        Assert.True(product.ChangeVariantStatus(first, CatalogItemStatus.Active, Now.AddMinutes(6)));
        Assert.Throws<InvalidOperationException>(() => product.Activate(false, Now.AddMinutes(7)));
        Assert.True(product.Activate(true, Now.AddMinutes(8)));
    }

    [Fact]
    public void VariantOrdering_MustContainExactAggregateSet()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        Product product = Product.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new LocalizedCatalogName("منتج", "Product"),
            null,
            null,
            new CatalogCode("PRODUCT"),
            ProductType.ServiceNonStock,
            0,
            [Draft(first, "VAR-1"), Draft(second, "VAR-2")],
            Now);

        Assert.Throws<ArgumentException>(() => product.ReorderVariants([first], Now));
        Assert.Throws<ArgumentException>(() => product.ReorderVariants([first, first], Now));
        Assert.Throws<ArgumentException>(() => product.ReorderVariants([first, Guid.NewGuid()], Now));
        Assert.True(product.ReorderVariants([second, first], Now.AddMinutes(1)));
        Assert.Equal(
            [second, first],
            product.Variants.OrderBy(variant => variant.DisplayOrder).Select(variant => variant.Id));
        long version = product.Version;
        Assert.False(product.ReorderVariants([second, first], Now.AddMinutes(2)));
        Assert.Equal(version, product.Version);
    }

    [Fact]
    public void CodesBarcodesAndBoundedAttributes_AreNormalizedAndValidated()
    {
        Assert.Equal("ABC-1", new CatalogCode(" abc-1 ").Value);
        Assert.Equal("6221234567890", new BarcodeValue(" 6221234567890 ").Value);
        Assert.Throws<ArgumentException>(() => new CatalogCode("bad code"));
        Assert.Throws<ArgumentException>(() => new BarcodeValue("javascript:alert(1)"));
        Assert.Throws<ArgumentException>(() => Product.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new LocalizedCatalogName("منتج", "Product"),
            null,
            null,
            new CatalogCode("PRODUCT"),
            ProductType.MadeToOrder,
            0,
            [Draft(Guid.NewGuid(), "VAR", sizeCode: "gigantic")],
            Now));
    }

    private static ProductVariantDraft Draft(Guid id, string code, string? sizeCode = "medium")
        => new(
            id,
            new LocalizedCatalogName("افتراضي", "Default"),
            new CatalogCode(code),
            null,
            sizeCode,
            "hot",
            "cup",
            0);
}
