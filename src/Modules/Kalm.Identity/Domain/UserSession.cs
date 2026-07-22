namespace Kalm.Identity.Domain;

public sealed class UserSession
{
    private UserSession()
    {
    }

    private UserSession(Guid id, Guid userId, DateTimeOffset now, TimeSpan inactivityTimeout, TimeSpan absoluteLifetime,
        Guid? deviceId, Guid? branchId, int? deviceSecurityVersion, long? pinCredentialVersion, long authorizationVersion)
    {
        EnsureUtc(now);
        Id = id;
        UserId = userId;
        DeviceId = deviceId;
        BranchId = branchId;
        DeviceSecurityVersion = deviceSecurityVersion;
        PinCredentialVersion = pinCredentialVersion;
        AuthorizationVersion = authorizationVersion;
        CreatedAtUtc = now;
        LastActivityAtUtc = now;
        AbsoluteExpiresAtUtc = now.Add(absoluteLifetime);
        InactivityExpiresAtUtc = Min(now.Add(inactivityTimeout), AbsoluteExpiresAtUtc);
        LastReauthenticatedAtUtc = now;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? DeviceId { get; private set; }
    public Guid? BranchId { get; private set; }
    public int? DeviceSecurityVersion { get; private set; }
    public long? PinCredentialVersion { get; private set; }
    public long AuthorizationVersion { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastActivityAtUtc { get; private set; }
    public DateTimeOffset InactivityExpiresAtUtc { get; private set; }
    public DateTimeOffset AbsoluteExpiresAtUtc { get; private set; }
    public DateTimeOffset LastReauthenticatedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public SessionRevocationReason? RevocationReason { get; private set; }
    public long Version { get; private set; }

    public static UserSession Create(Guid id, Guid userId, DateTimeOffset now, TimeSpan inactivityTimeout, TimeSpan absoluteLifetime)
    {
        if (userId == Guid.Empty || inactivityTimeout <= TimeSpan.Zero || absoluteLifetime <= inactivityTimeout)
        {
            throw new ArgumentException("Session parameters are invalid.");
        }

        return new UserSession(id, userId, now, inactivityTimeout, absoluteLifetime, null, null, null, null, 0);
    }

    public static UserSession CreateDeviceBound(Guid id, Guid userId, Guid deviceId, Guid branchId,
        int deviceSecurityVersion, long pinCredentialVersion, long authorizationVersion,
        DateTimeOffset now, TimeSpan inactivityTimeout, TimeSpan absoluteLifetime)
    {
        if (userId == Guid.Empty || deviceId == Guid.Empty || branchId == Guid.Empty || deviceSecurityVersion < 1
            || pinCredentialVersion < 1 || authorizationVersion < 1 || inactivityTimeout <= TimeSpan.Zero || absoluteLifetime <= inactivityTimeout)
            throw new ArgumentException("Device-bound session parameters are invalid.");
        return new UserSession(id, userId, now, inactivityTimeout, absoluteLifetime,
            deviceId, branchId, deviceSecurityVersion, pinCredentialVersion, authorizationVersion);
    }

    public bool IsValid(DateTimeOffset now) => RevokedAtUtc is null && now < InactivityExpiresAtUtc && now < AbsoluteExpiresAtUtc;

    public void RecordActivity(DateTimeOffset now, TimeSpan inactivityTimeout)
    {
        EnsureUtc(now);
        if (!IsValid(now))
        {
            throw new InvalidOperationException("Expired or revoked sessions cannot record activity.");
        }

        if (now > LastActivityAtUtc)
        {
            LastActivityAtUtc = now;
            InactivityExpiresAtUtc = Min(now.Add(inactivityTimeout), AbsoluteExpiresAtUtc);
            Version++;
        }
    }

    public void Revoke(SessionRevocationReason reason, DateTimeOffset now)
    {
        EnsureUtc(now);
        if (RevokedAtUtc is null)
        {
            RevokedAtUtc = now;
            RevocationReason = reason;
            Version++;
        }
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must be UTC.", nameof(value));
        }
    }
}
