namespace Kalm.Identity.Domain.ValueObjects;

public sealed record DisplayName
{
    public DisplayName(string value)
    {
        Value = value?.Trim() ?? string.Empty;
        if (Value.Length is < 2 or > 120)
        {
            throw new ArgumentException("Display name must contain between 2 and 120 characters.", nameof(value));
        }
    }

    public string Value { get; }
}
