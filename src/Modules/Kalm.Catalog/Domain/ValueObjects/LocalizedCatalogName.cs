namespace Kalm.Catalog.Domain.ValueObjects;

public sealed record LocalizedCatalogName
{
    public LocalizedCatalogName(string arabicName, string englishName)
    {
        Arabic = CatalogText.Display(arabicName, nameof(arabicName), 120);
        English = CatalogText.Display(englishName, nameof(englishName), 120);
        NormalizedArabic = CatalogText.Lookup(Arabic);
        NormalizedEnglish = CatalogText.Lookup(English);
    }

    public string Arabic { get; }
    public string English { get; }
    public string NormalizedArabic { get; }
    public string NormalizedEnglish { get; }
}
