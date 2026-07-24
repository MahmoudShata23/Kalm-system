using Kalm.Catalog.Domain.ValueObjects;

namespace Kalm.Catalog.Domain;

public sealed class Category
{
    private Category()
    {
    }

    private Category(
        Guid id,
        Guid organizationId,
        LocalizedCatalogName name,
        int displayOrder,
        string? posColorToken,
        string? iconCode,
        DateTimeOffset now)
    {
        Id = id;
        OrganizationId = organizationId;
        ApplyName(name);
        DisplayOrder = NonNegative(displayOrder, nameof(displayOrder));
        PosColorToken = CatalogPresentationOptions.OptionalCode(posColorToken, CatalogPresentationOptions.ColorTokens, nameof(posColorToken));
        IconCode = CatalogPresentationOptions.OptionalCode(iconCode, CatalogPresentationOptions.IconCodes, nameof(iconCode));
        Status = CatalogItemStatus.Active;
        Version = 1;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string ArabicName { get; private set; } = string.Empty;
    public string EnglishName { get; private set; } = string.Empty;
    public string NormalizedArabicName { get; private set; } = string.Empty;
    public string NormalizedEnglishName { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }
    public CatalogItemStatus Status { get; private set; }
    public string? PosColorToken { get; private set; }
    public string? IconCode { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Category Create(
        Guid id,
        Guid organizationId,
        LocalizedCatalogName name,
        int displayOrder,
        string? posColorToken,
        string? iconCode,
        DateTimeOffset now)
    {
        if (id == Guid.Empty) throw new ArgumentException("Category identifier is required.", nameof(id));
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        return new Category(id, organizationId, name, displayOrder, posColorToken, iconCode, now);
    }

    public bool Update(
        LocalizedCatalogName name,
        int displayOrder,
        string? posColorToken,
        string? iconCode,
        DateTimeOffset now)
    {
        int safeOrder = NonNegative(displayOrder, nameof(displayOrder));
        string? safeColor = CatalogPresentationOptions.OptionalCode(posColorToken, CatalogPresentationOptions.ColorTokens, nameof(posColorToken));
        string? safeIcon = CatalogPresentationOptions.OptionalCode(iconCode, CatalogPresentationOptions.IconCodes, nameof(iconCode));
        if (ArabicName == name.Arabic
            && EnglishName == name.English
            && DisplayOrder == safeOrder
            && PosColorToken == safeColor
            && IconCode == safeIcon)
        {
            return false;
        }

        ApplyName(name);
        DisplayOrder = safeOrder;
        PosColorToken = safeColor;
        IconCode = safeIcon;
        AdvanceVersion(now);
        return true;
    }

    public bool SetDisplayOrder(int displayOrder, DateTimeOffset now)
    {
        int safeOrder = NonNegative(displayOrder, nameof(displayOrder));
        if (DisplayOrder == safeOrder) return false;
        DisplayOrder = safeOrder;
        AdvanceVersion(now);
        return true;
    }

    public bool Activate(DateTimeOffset now)
    {
        if (Status == CatalogItemStatus.Active) return false;
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

    private void ApplyName(LocalizedCatalogName name)
    {
        ArabicName = name.Arabic;
        EnglishName = name.English;
        NormalizedArabicName = name.NormalizedArabic;
        NormalizedEnglishName = name.NormalizedEnglish;
    }

    private void AdvanceVersion(DateTimeOffset now)
    {
        Version++;
        UpdatedAtUtc = now;
    }

    private static int NonNegative(int value, string field)
        => value >= 0 ? value : throw new ArgumentOutOfRangeException(field, "Display order must be non-negative.");
}
