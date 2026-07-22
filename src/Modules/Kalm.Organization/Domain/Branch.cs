using Kalm.Organization.Domain.ValueObjects;

namespace Kalm.Organization.Domain;

public sealed class Branch
{
    private Branch()
    {
    }

    private Branch(Guid id, Guid organizationId, OrganizationName name, BranchCode code, LocaleCode locale, TimeZoneId timeZone, BusinessDayRollover rollover, DateTimeOffset now)
    {
        Id = id;
        OrganizationId = organizationId;
        Name = name.Value;
        Code = code.Value;
        LocaleCode = locale.Value;
        TimeZoneId = timeZone.Value;
        BusinessDayRollover = rollover.Value;
        Status = BranchStatus.Setup;
        Version = 1;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string LocaleCode { get; private set; } = string.Empty;
    public string TimeZoneId { get; private set; } = string.Empty;
    public TimeOnly BusinessDayRollover { get; private set; }
    public BranchStatus Status { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Branch Create(Guid id, Guid organizationId, OrganizationName name, BranchCode code, LocaleCode locale, TimeZoneId timeZone, BusinessDayRollover rollover, DateTimeOffset now)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization is required.", nameof(organizationId));
        }

        return new Branch(id, organizationId, name, code, locale, timeZone, rollover, now);
    }

    public bool Update(OrganizationName name, BranchCode code, LocaleCode locale, TimeZoneId timeZone, BusinessDayRollover rollover, DateTimeOffset now)
    {
        if (Name == name.Value
            && Code == code.Value
            && LocaleCode == locale.Value
            && TimeZoneId == timeZone.Value
            && BusinessDayRollover == rollover.Value)
        {
            return false;
        }

        Name = name.Value;
        Code = code.Value;
        LocaleCode = locale.Value;
        TimeZoneId = timeZone.Value;
        BusinessDayRollover = rollover.Value;
        AdvanceVersion(now);
        return true;
    }

    public bool Activate(DateTimeOffset now)
    {
        if (Status == BranchStatus.Active)
        {
            return false;
        }

        if (Status == BranchStatus.Archived)
        {
            throw new InvalidOperationException("Archived branches cannot be reactivated.");
        }

        Status = BranchStatus.Active;
        AdvanceVersion(now);
        return true;
    }

    public bool Deactivate(DateTimeOffset now)
    {
        if (Status == BranchStatus.Suspended)
        {
            return false;
        }

        if (Status == BranchStatus.Archived)
        {
            throw new InvalidOperationException("Archived branches cannot be deactivated.");
        }

        Status = BranchStatus.Suspended;
        AdvanceVersion(now);
        return true;
    }

    public void ChangeStatus(BranchStatus status, DateTimeOffset now)
    {
        if (Status == BranchStatus.Archived && status != BranchStatus.Archived)
        {
            throw new InvalidOperationException("Archived branches cannot be reactivated.");
        }

        Status = status;
        AdvanceVersion(now);
    }

    private void AdvanceVersion(DateTimeOffset now)
    {
        Version++;
        UpdatedAtUtc = now;
    }
}
