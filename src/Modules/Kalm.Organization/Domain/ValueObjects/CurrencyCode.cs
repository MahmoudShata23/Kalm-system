using System.Text.RegularExpressions;

namespace Kalm.Organization.Domain.ValueObjects;

public sealed partial record CurrencyCode
{
    public CurrencyCode(string value)
    {
        Value = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (!CurrencyPattern().IsMatch(Value))
        {
            throw new ArgumentException("Currency code must be exactly three ASCII letters.", nameof(value));
        }
    }

    public string Value { get; }

    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyPattern();
}
