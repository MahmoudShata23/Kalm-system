namespace Kalm.Identity.Domain;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "RolePermission is the approved domain term from IAM-004.")]
public sealed class RolePermission
{
    private RolePermission()
    {
    }

    private RolePermission(Guid id, Guid roleId, Guid permissionId, DateTimeOffset now)
    {
        EnsureUtc(now);
        if (roleId == Guid.Empty || permissionId == Guid.Empty)
        {
            throw new ArgumentException("Role and permission are required.");
        }

        Id = id;
        RoleId = roleId;
        PermissionId = permissionId;
        GrantedAtUtc = now;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public DateTimeOffset GrantedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public long Version { get; private set; }

    public static RolePermission Grant(Guid id, Guid roleId, Guid permissionId, DateTimeOffset now)
        => new(id, roleId, permissionId, now);

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
