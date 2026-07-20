using Kalm.Organization.Domain.ValueObjects;

namespace Kalm.Organization.Domain;

public sealed class Organization
{
    private Organization()
    {
    }

    private Organization(
        Guid id,
        OrganizationName brandName,
        string? legalName,
        CurrencyCode defaultCurrencyCode,
        LocaleCode defaultLocaleCode,
        DateTimeOffset now)
    {
        Id = id;
        BrandName = brandName.Value;
        LegalName = NormalizeOptionalLegalName(legalName);
        DefaultCurrencyCode = defaultCurrencyCode.Value;
        DefaultLocaleCode = defaultLocaleCode.Value;
        Status = OrganizationStatus.Setup;
        Version = 1;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public string BrandName { get; private set; } = string.Empty;
    public string? LegalName { get; private set; }
    public string DefaultCurrencyCode { get; private set; } = string.Empty;
    public string DefaultLocaleCode { get; private set; } = string.Empty;
    public OrganizationStatus Status { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Organization Create(Guid id, OrganizationName brandName, string? legalName, CurrencyCode currency, LocaleCode locale, DateTimeOffset now)
        => new(id, brandName, legalName, currency, locale, now);

    public void Update(OrganizationName brandName, string? legalName, CurrencyCode currency, LocaleCode locale, DateTimeOffset now)
    {
        BrandName = brandName.Value;
        LegalName = NormalizeOptionalLegalName(legalName);
        DefaultCurrencyCode = currency.Value;
        DefaultLocaleCode = locale.Value;
        AdvanceVersion(now);
    }

    public void ChangeStatus(OrganizationStatus status, DateTimeOffset now)
    {
        if (Status == OrganizationStatus.Archived && status != OrganizationStatus.Archived)
        {
            throw new InvalidOperationException("Archived organizations cannot be reactivated.");
        }

        Status = status;
        AdvanceVersion(now);
    }

    private static string? NormalizeOptionalLegalName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        if (normalized.Length > 160)
        {
            throw new ArgumentException("Legal name cannot exceed 160 characters.", nameof(value));
        }

        return normalized;
    }

    private void AdvanceVersion(DateTimeOffset now)
    {
        Version++;
        UpdatedAtUtc = now;
    }
}
