using Kalm.Catalog.Domain.ValueObjects;

namespace Kalm.Catalog.Domain;

public sealed class ProductVariant
{
    private ProductVariant()
    {
    }

    internal ProductVariant(
        Guid id,
        Guid productId,
        Guid organizationId,
        LocalizedCatalogName name,
        CatalogCode code,
        BarcodeValue? barcode,
        string? sizeCode,
        string? temperatureCode,
        string? servingFormatCode,
        int displayOrder,
        DateTimeOffset now)
    {
        if (id == Guid.Empty) throw new ArgumentException("Variant identifier is required.", nameof(id));
        Id = id;
        ProductId = productId;
        OrganizationId = organizationId;
        Apply(
            name,
            code,
            barcode,
            sizeCode,
            temperatureCode,
            servingFormatCode,
            displayOrder);
        Status = CatalogItemStatus.Active;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string ArabicName { get; private set; } = string.Empty;
    public string EnglishName { get; private set; } = string.Empty;
    public string NormalizedArabicName { get; private set; } = string.Empty;
    public string NormalizedEnglishName { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string? Barcode { get; private set; }
    public string? SizeCode { get; private set; }
    public string? TemperatureCode { get; private set; }
    public string? ServingFormatCode { get; private set; }
    public int DisplayOrder { get; private set; }
    public CatalogItemStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    internal bool Update(
        LocalizedCatalogName name,
        CatalogCode code,
        BarcodeValue? barcode,
        string? sizeCode,
        string? temperatureCode,
        string? servingFormatCode,
        int displayOrder,
        DateTimeOffset now)
    {
        string? safeSize = CatalogPresentationOptions.OptionalCode(sizeCode, CatalogPresentationOptions.SizeCodes, nameof(sizeCode));
        string? safeTemperature = CatalogPresentationOptions.OptionalCode(temperatureCode, CatalogPresentationOptions.TemperatureCodes, nameof(temperatureCode));
        string? safeServing = CatalogPresentationOptions.OptionalCode(servingFormatCode, CatalogPresentationOptions.ServingFormatCodes, nameof(servingFormatCode));
        if (ArabicName == name.Arabic
            && EnglishName == name.English
            && Code == code.Value
            && Barcode == barcode?.Value
            && SizeCode == safeSize
            && TemperatureCode == safeTemperature
            && ServingFormatCode == safeServing
            && DisplayOrder == displayOrder)
        {
            return false;
        }

        Apply(name, code, barcode, safeSize, safeTemperature, safeServing, displayOrder);
        UpdatedAtUtc = now;
        return true;
    }

    internal bool ChangeStatus(CatalogItemStatus status, DateTimeOffset now)
    {
        if (Status == status) return false;
        Status = status;
        UpdatedAtUtc = now;
        return true;
    }

    internal bool SetDisplayOrder(int displayOrder, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);
        if (DisplayOrder == displayOrder) return false;
        DisplayOrder = displayOrder;
        UpdatedAtUtc = now;
        return true;
    }

    private void Apply(
        LocalizedCatalogName name,
        CatalogCode code,
        BarcodeValue? barcode,
        string? sizeCode,
        string? temperatureCode,
        string? servingFormatCode,
        int displayOrder)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);
        ArabicName = name.Arabic;
        EnglishName = name.English;
        NormalizedArabicName = name.NormalizedArabic;
        NormalizedEnglishName = name.NormalizedEnglish;
        Code = code.Value;
        Barcode = barcode?.Value;
        SizeCode = CatalogPresentationOptions.OptionalCode(sizeCode, CatalogPresentationOptions.SizeCodes, nameof(sizeCode));
        TemperatureCode = CatalogPresentationOptions.OptionalCode(temperatureCode, CatalogPresentationOptions.TemperatureCodes, nameof(temperatureCode));
        ServingFormatCode = CatalogPresentationOptions.OptionalCode(servingFormatCode, CatalogPresentationOptions.ServingFormatCodes, nameof(servingFormatCode));
        DisplayOrder = displayOrder;
    }
}
