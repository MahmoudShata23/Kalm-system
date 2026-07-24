namespace Kalm.Catalog.Domain;

public static class CatalogPresentationOptions
{
    public static readonly IReadOnlySet<string> ColorTokens = new HashSet<string>(
        ["sand", "espresso", "caramel", "sage", "rose", "sky", "plum", "slate"],
        StringComparer.Ordinal);

    public static readonly IReadOnlySet<string> IconCodes = new HashSet<string>(
        ["coffee", "mug", "glass", "bottle", "cookie", "cake", "bread", "sandwich", "addition", "star", "leaf"],
        StringComparer.Ordinal);

    public static readonly IReadOnlySet<string> SizeCodes = new HashSet<string>(
        ["single", "double", "small", "medium", "large"],
        StringComparer.Ordinal);

    public static readonly IReadOnlySet<string> TemperatureCodes = new HashSet<string>(
        ["hot", "iced", "ambient"],
        StringComparer.Ordinal);

    public static readonly IReadOnlySet<string> ServingFormatCodes = new HashSet<string>(
        ["cup", "mug", "glass", "bottle", "can", "plate", "piece"],
        StringComparer.Ordinal);

    public static string? OptionalCode(string? value, IReadOnlySet<string> allowlist, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim().ToLowerInvariant();
        if (!allowlist.Contains(normalized))
        {
            throw new ArgumentException($"{field} is not an approved option.", field);
        }

        return normalized;
    }
}
