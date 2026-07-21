using System.Text.RegularExpressions;

namespace Kalm.Identity.Domain.ValueObjects;

public sealed partial class PermissionCode
{
    public PermissionCode(string value)
    {
        string candidate = value ?? string.Empty;
        if (candidate.Length is < 3 or > 100
            || !string.Equals(candidate, candidate.Trim(), StringComparison.Ordinal)
            || !ValidCode().IsMatch(candidate))
        {
            throw new ArgumentException("Permission code must be a lowercase dotted identifier.", nameof(value));
        }

        Value = candidate;
    }

    public string Value { get; }

    [GeneratedRegex("^[a-z][a-z0-9_]*(?:\\.[a-z][a-z0-9_]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidCode();
}
