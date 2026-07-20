using System.Globalization;

namespace Kalm.Organization.Domain.ValueObjects;

public sealed record BusinessDayRollover
{
    public BusinessDayRollover(TimeOnly value)
    {
        if (value.Second != 0 || value.Millisecond != 0 || value.Microsecond != 0 || value.Nanosecond != 0)
        {
            throw new ArgumentException("Business-day rollover must use minute precision.", nameof(value));
        }

        Value = value;
    }

    public TimeOnly Value { get; }

    public override string ToString() => Value.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static BusinessDayRollover Parse(string value)
    {
        if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly time))
        {
            throw new ArgumentException("Business-day rollover must use HH:mm format.", nameof(value));
        }

        return new BusinessDayRollover(time);
    }
}
