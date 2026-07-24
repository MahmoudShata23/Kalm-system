using System.Text;
using System.Text.RegularExpressions;

namespace Kalm.Catalog.Domain.ValueObjects;

public static partial class CatalogText
{
    public static string Display(string? value, string field, int maximumLength, bool required = true)
    {
        string normalized = Whitespace().Replace((value ?? string.Empty).Normalize(NormalizationForm.FormKC).Trim(), " ");
        if (required && normalized.Length == 0)
        {
            throw new ArgumentException($"{field} is required.", field);
        }

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"{field} must not exceed {maximumLength} characters.", field);
        }

        return normalized;
    }

    public static string Lookup(string value) => Display(value, nameof(value), 120).ToUpperInvariant();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
