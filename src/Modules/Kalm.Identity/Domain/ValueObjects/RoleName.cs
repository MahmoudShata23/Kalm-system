namespace Kalm.Identity.Domain.ValueObjects;

public sealed class RoleName
{
    public RoleName(string value)
    {
        string normalized = (value ?? string.Empty).Trim();
        if (normalized.Length is < 1 or > 120)
        {
            throw new ArgumentException("Role name is required and cannot exceed 120 characters.", nameof(value));
        }

        Value = normalized;
        NormalizedValue = normalized.Normalize().ToUpperInvariant();
    }

    public string Value { get; }
    public string NormalizedValue { get; }
}
