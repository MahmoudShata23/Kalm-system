using Kalm.SharedKernel.BusinessDates;

namespace Kalm.UnitTests.BusinessDates;

public sealed class BusinessDateResolverTests
{
    private readonly TimeZoneInfo _cairoTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");

    [Fact]
    public void ResolveBusinessDate_UsesPreviousDateBeforeRollover()
    {
        var utc = new DateTimeOffset(2026, 7, 15, 23, 0, 0, TimeSpan.Zero);

        var businessDate = BusinessDateResolver.ResolveBusinessDate(utc, _cairoTimeZone, new TimeOnly(4, 0));

        Assert.Equal(new DateOnly(2026, 7, 15), businessDate);
    }

    [Fact]
    public void ResolveBusinessDate_UsesCurrentDateAtOrAfterRollover()
    {
        var utc = new DateTimeOffset(2026, 7, 16, 2, 0, 0, TimeSpan.Zero);

        var businessDate = BusinessDateResolver.ResolveBusinessDate(utc, _cairoTimeZone, new TimeOnly(4, 0));

        Assert.Equal(new DateOnly(2026, 7, 16), businessDate);
    }
}
