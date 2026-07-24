using Kalm.Catalog.Domain.ValueObjects;

namespace Kalm.Catalog.Domain;

public sealed class Product
{
    private readonly List<ProductVariant> _variants = [];

    private Product()
    {
    }

    private Product(
        Guid id,
        Guid organizationId,
        Guid categoryId,
        LocalizedCatalogName name,
        string? arabicDescription,
        string? englishDescription,
        CatalogCode sku,
        ProductType type,
        int displayOrder,
        IReadOnlyCollection<ProductVariantDraft> variants,
        DateTimeOffset now)
    {
        Id = id;
        OrganizationId = organizationId;
        CategoryId = categoryId;
        ApplyDetails(name, arabicDescription, englishDescription, sku, type, displayOrder);
        foreach (ProductVariantDraft variant in variants)
        {
            _variants.Add(CreateVariant(variant, now));
        }

        if (_variants.Count == 0) throw new InvalidOperationException("A product requires at least one variant.");
        Status = CatalogItemStatus.Active;
        Version = 1;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string ArabicName { get; private set; } = string.Empty;
    public string EnglishName { get; private set; } = string.Empty;
    public string NormalizedArabicName { get; private set; } = string.Empty;
    public string NormalizedEnglishName { get; private set; } = string.Empty;
    public string? ArabicDescription { get; private set; }
    public string? EnglishDescription { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public ProductType Type { get; private set; }
    public int DisplayOrder { get; private set; }
    public CatalogItemStatus Status { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<ProductVariant> Variants => _variants.AsReadOnly();

    public static Product Create(
        Guid id,
        Guid organizationId,
        Guid categoryId,
        LocalizedCatalogName name,
        string? arabicDescription,
        string? englishDescription,
        CatalogCode sku,
        ProductType type,
        int displayOrder,
        IReadOnlyCollection<ProductVariantDraft> variants,
        DateTimeOffset now)
    {
        if (id == Guid.Empty) throw new ArgumentException("Product identifier is required.", nameof(id));
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        if (categoryId == Guid.Empty) throw new ArgumentException("Category is required.", nameof(categoryId));
        return new Product(id, organizationId, categoryId, name, arabicDescription, englishDescription, sku, type, displayOrder, variants, now);
    }

    public bool UpdateDetails(
        Guid categoryId,
        LocalizedCatalogName name,
        string? arabicDescription,
        string? englishDescription,
        CatalogCode sku,
        ProductType type,
        int displayOrder,
        DateTimeOffset now)
    {
        if (categoryId == Guid.Empty) throw new ArgumentException("Category is required.", nameof(categoryId));
        string? safeArabicDescription = OptionalDescription(arabicDescription, nameof(arabicDescription));
        string? safeEnglishDescription = OptionalDescription(englishDescription, nameof(englishDescription));
        if (CategoryId == categoryId
            && ArabicName == name.Arabic
            && EnglishName == name.English
            && ArabicDescription == safeArabicDescription
            && EnglishDescription == safeEnglishDescription
            && Sku == sku.Value
            && Type == type
            && DisplayOrder == displayOrder)
        {
            return false;
        }

        CategoryId = categoryId;
        ApplyDetails(name, safeArabicDescription, safeEnglishDescription, sku, type, displayOrder);
        AdvanceVersion(now);
        return true;
    }

    public ProductVariant AddVariant(ProductVariantDraft draft, DateTimeOffset now)
    {
        if (_variants.Count >= 100) throw new InvalidOperationException("A product cannot contain more than 100 variants.");
        ProductVariant variant = CreateVariant(draft, now);
        if (_variants.Any(candidate => candidate.Id == variant.Id))
        {
            throw new InvalidOperationException("Variant identifier is already present.");
        }

        _variants.Add(variant);
        AdvanceVersion(now);
        return variant;
    }

    public bool UpdateVariant(Guid variantId, ProductVariantDraft draft, DateTimeOffset now)
    {
        ProductVariant variant = FindVariant(variantId);
        bool changed = variant.Update(
            draft.Name,
            draft.Code,
            draft.Barcode,
            draft.SizeCode,
            draft.TemperatureCode,
            draft.ServingFormatCode,
            draft.DisplayOrder,
            now);
        if (changed) AdvanceVersion(now);
        return changed;
    }

    public bool ChangeVariantStatus(Guid variantId, CatalogItemStatus status, DateTimeOffset now)
    {
        ProductVariant variant = FindVariant(variantId);
        if (variant.Status == status) return false;
        if (status == CatalogItemStatus.Archived
            && Status == CatalogItemStatus.Active
            && _variants.Count(candidate => candidate.Status == CatalogItemStatus.Active) == 1)
        {
            throw new InvalidOperationException("An active product requires at least one active variant.");
        }

        bool changed = variant.ChangeStatus(status, now);
        if (changed) AdvanceVersion(now);
        return changed;
    }

    public bool ReorderVariants(IReadOnlyCollection<Guid> orderedIds, DateTimeOffset now)
    {
        if (orderedIds.Count != _variants.Count
            || orderedIds.Distinct().Count() != _variants.Count
            || orderedIds.Any(id => _variants.All(variant => variant.Id != id)))
        {
            throw new ArgumentException("Variant ordering must contain every product variant exactly once.", nameof(orderedIds));
        }

        bool changed = false;
        int index = 0;
        foreach (Guid id in orderedIds)
        {
            changed |= FindVariant(id).SetDisplayOrder(index++, now);
        }

        if (changed) AdvanceVersion(now);
        return changed;
    }

    public bool Activate(bool categoryIsActive, DateTimeOffset now)
    {
        if (Status == CatalogItemStatus.Active) return false;
        if (!categoryIsActive) throw new InvalidOperationException("An active product requires an active category.");
        if (_variants.All(variant => variant.Status != CatalogItemStatus.Active))
        {
            throw new InvalidOperationException("An active product requires at least one active variant.");
        }

        Status = CatalogItemStatus.Active;
        AdvanceVersion(now);
        return true;
    }

    public bool Archive(DateTimeOffset now)
    {
        if (Status == CatalogItemStatus.Archived) return false;
        Status = CatalogItemStatus.Archived;
        AdvanceVersion(now);
        return true;
    }

    private ProductVariant CreateVariant(ProductVariantDraft draft, DateTimeOffset now)
        => new(
            draft.Id,
            Id,
            OrganizationId,
            draft.Name,
            draft.Code,
            draft.Barcode,
            draft.SizeCode,
            draft.TemperatureCode,
            draft.ServingFormatCode,
            draft.DisplayOrder,
            now);

    private ProductVariant FindVariant(Guid variantId)
        => _variants.SingleOrDefault(variant => variant.Id == variantId)
            ?? throw new KeyNotFoundException("Variant was not found in this product.");

    private void ApplyDetails(
        LocalizedCatalogName name,
        string? arabicDescription,
        string? englishDescription,
        CatalogCode sku,
        ProductType type,
        int displayOrder)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);
        ArabicName = name.Arabic;
        EnglishName = name.English;
        NormalizedArabicName = name.NormalizedArabic;
        NormalizedEnglishName = name.NormalizedEnglish;
        ArabicDescription = OptionalDescription(arabicDescription, nameof(arabicDescription));
        EnglishDescription = OptionalDescription(englishDescription, nameof(englishDescription));
        Sku = sku.Value;
        Type = type;
        DisplayOrder = displayOrder;
    }

    private void AdvanceVersion(DateTimeOffset now)
    {
        Version++;
        UpdatedAtUtc = now;
    }

    private static string? OptionalDescription(string? value, string field)
    {
        string normalized = CatalogText.Display(value, field, 1000, required: false);
        return normalized.Length == 0 ? null : normalized;
    }
}

public sealed record ProductVariantDraft(
    Guid Id,
    LocalizedCatalogName Name,
    CatalogCode Code,
    BarcodeValue? Barcode,
    string? SizeCode,
    string? TemperatureCode,
    string? ServingFormatCode,
    int DisplayOrder);
