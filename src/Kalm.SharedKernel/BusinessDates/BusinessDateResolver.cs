namespace Kalm.SharedKernel.BusinessDates;

public static class BusinessDateResolver
{
    public static DateOnly ResolveBusinessDate(DateTimeOffset occurredAtUtc, TimeZoneInfo branchTimeZone, TimeOnly rolloverTime)
    {
        ArgumentNullException.ThrowIfNull(branchTimeZone);

        DateTimeOffset localTime = TimeZoneInfo.ConvertTime(occurredAtUtc, branchTimeZone);
        DateOnly businessDate = DateOnly.FromDateTime(localTime.DateTime);

        return TimeOnly.FromDateTime(localTime.DateTime) < rolloverTime
            ? businessDate.AddDays(-1)
            : businessDate;
    }
}
