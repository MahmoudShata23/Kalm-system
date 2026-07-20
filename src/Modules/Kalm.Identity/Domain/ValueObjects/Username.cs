using System.Text;

namespace Kalm.Identity.Domain.ValueObjects;

public sealed record Username
{
    public Username(string value)
    {
        Value = (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormKC);
        if (Value.Length is < 3 or > 64 || Value.Any(char.IsControl))
        {
            throw new ArgumentException("Username must contain between 3 and 64 non-control characters.", nameof(value));
        }

        NormalizedValue = Value.ToUpperInvariant();
    }

    public string Value { get; }

    public string NormalizedValue { get; }
}
