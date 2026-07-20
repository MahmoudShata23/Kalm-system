using System.Globalization;

namespace Kalm.Organization.Domain.ValueObjects;

public sealed record LocaleCode
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        "en",
        "ar-EG"
    };

    public LocaleCode(string value)
    {
        Value = (value ?? string.Empty).Trim();
        if (!Supported.Contains(Value))
        {
            throw new ArgumentException("Locale code is not supported by Kalm.", nameof(value));
        }

        _ = CultureInfo.GetCultureInfo(Value);
    }

    public string Value { get; }

    public static IReadOnlyCollection<string> SupportedValues => Supported;
}
