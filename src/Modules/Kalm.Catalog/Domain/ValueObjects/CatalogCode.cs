using System.Text.RegularExpressions;

namespace Kalm.Catalog.Domain.ValueObjects;

public sealed partial record CatalogCode
{
    public CatalogCode(string value)
    {
        Value = (value ?? string.Empty).Normalize().Trim().ToUpperInvariant();
        if (!CodePattern().IsMatch(Value))
        {
            throw new ArgumentException("Catalog codes must contain 2 to 40 uppercase letters, digits, dots, underscores, or hyphens.", nameof(value));
        }
    }

    public string Value { get; }

    [GeneratedRegex("^[A-Z0-9][A-Z0-9._-]{1,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}
