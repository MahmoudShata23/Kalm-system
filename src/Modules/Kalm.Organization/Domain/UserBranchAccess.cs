namespace Kalm.Organization.Domain;

public sealed class UserBranchAccess
{
    private UserBranchAccess()
    {
    }

    private UserBranchAccess(Guid id, Guid organizationId, Guid userId, BranchAccessScope scope, DateTimeOffset now)
    {
        EnsureUtc(now);
        if (organizationId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("Organization and user are required.");
        }

        Id = id;
        OrganizationId = organizationId;
        UserId = userId;
        Scope = scope;
        Version = 1;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public BranchAccessScope Scope { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static UserBranchAccess Create(Guid id, Guid organizationId, Guid userId, BranchAccessScope scope, DateTimeOffset now)
        => new(id, organizationId, userId, scope, now);

    public void ChangeScope(BranchAccessScope scope, DateTimeOffset now)
    {
        EnsureUtc(now);
        if (Scope == scope)
        {
            return;
        }

        Scope = scope;
        Version++;
        UpdatedAtUtc = now;
    }

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must be UTC.", nameof(value));
        }
    }
}
