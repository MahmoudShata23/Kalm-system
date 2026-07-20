namespace Kalm.Organization.Domain.ValueObjects;

public sealed record TimeZoneId
{
    public TimeZoneId(string value)
    {
        Value = (value ?? string.Empty).Trim();
        if (Value.Length == 0)
        {
            throw new ArgumentException("Time zone is required.", nameof(value));
        }

        if (!Value.Contains('/', StringComparison.Ordinal) || Value.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Time zone must use an IANA time-zone identifier.", nameof(value));
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(Value);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new ArgumentException("Time zone must be a valid IANA time-zone identifier.", nameof(value), exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new ArgumentException("Time zone data is invalid.", nameof(value), exception);
        }
    }

    public string Value { get; }
}
