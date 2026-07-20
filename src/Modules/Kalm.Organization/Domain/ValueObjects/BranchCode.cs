using System.Text.RegularExpressions;

namespace Kalm.Organization.Domain.ValueObjects;

public sealed partial record BranchCode
{
    public BranchCode(string value)
    {
        Value = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (!CodePattern().IsMatch(Value))
        {
            throw new ArgumentException("Branch code must contain 2 to 20 uppercase letters, digits, or hyphens.", nameof(value));
        }
    }

    public string Value { get; }

    [GeneratedRegex("^[A-Z0-9-]{2,20}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}
