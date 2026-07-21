namespace Kalm.Identity.Domain;

public sealed class UserRoleAssignment
{
    private UserRoleAssignment()
    {
    }

    private UserRoleAssignment(Guid id, Guid organizationId, Guid userId, Guid roleId, DateTimeOffset now)
    {
        EnsureUtc(now);
        if (organizationId == Guid.Empty || userId == Guid.Empty || roleId == Guid.Empty)
        {
            throw new ArgumentException("Organization, user, and role are required.");
        }

        Id = id;
        OrganizationId = organizationId;
        UserId = userId;
        RoleId = roleId;
        AssignedAtUtc = now;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTimeOffset AssignedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public long Version { get; private set; }

    public static UserRoleAssignment Assign(Guid id, Guid organizationId, Guid userId, Guid roleId, DateTimeOffset now)
        => new(id, organizationId, userId, roleId, now);

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
