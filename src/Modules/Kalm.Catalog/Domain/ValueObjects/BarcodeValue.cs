using System.Text.RegularExpressions;

namespace Kalm.Catalog.Domain.ValueObjects;

public sealed partial record BarcodeValue
{
    public BarcodeValue(string value)
    {
        Value = (value ?? string.Empty).Normalize().Trim().ToUpperInvariant();
        if (!BarcodePattern().IsMatch(Value))
        {
            throw new ArgumentException("Barcode must contain 4 to 64 letters, digits, dots, or hyphens.", nameof(value));
        }
    }

    public string Value { get; }

    [GeneratedRegex("^[A-Z0-9][A-Z0-9.-]{3,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex BarcodePattern();
}
