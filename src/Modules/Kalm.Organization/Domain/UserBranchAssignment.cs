namespace Kalm.Organization.Domain;

public sealed class UserBranchAssignment
{
    private UserBranchAssignment()
    {
    }

    private UserBranchAssignment(Guid id, Guid accessId, Guid organizationId, Guid branchId, DateTimeOffset now)
    {
        EnsureUtc(now);
        if (accessId == Guid.Empty || organizationId == Guid.Empty || branchId == Guid.Empty)
        {
            throw new ArgumentException("Access, organization, and branch are required.");
        }

        Id = id;
        AccessId = accessId;
        OrganizationId = organizationId;
        BranchId = branchId;
        AssignedAtUtc = now;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid AccessId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid BranchId { get; private set; }
    public DateTimeOffset AssignedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public long Version { get; private set; }

    public static UserBranchAssignment Assign(Guid id, Guid accessId, Guid organizationId, Guid branchId, DateTimeOffset now)
        => new(id, accessId, organizationId, branchId, now);

    public void Revoke(DateTimeOffset now)
    {
        EnsureUtc(now);
        if (RevokedAtUtc is null)
        {
            RevokedAtUtc = now;
            Version++;
        }
    }

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must be UTC.", nameof(value));
        }
    }
}
