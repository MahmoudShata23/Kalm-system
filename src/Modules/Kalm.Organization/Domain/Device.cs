namespace Kalm.Organization.Domain;

public sealed class Device
{
    private Device() { }

    private Device(Guid id, Guid organizationId, Guid branchId, string name, DeviceType type, string? platform, DateTimeOffset now)
    {
        EnsureUtc(now);
        Id = id;
        OrganizationId = organizationId;
        BranchId = branchId;
        Name = Required(name, 120, nameof(name));
        Type = type;
        Platform = Optional(platform, 120, nameof(platform));
        Status = DeviceStatus.PendingPairing;
        SecurityVersion = 1;
        Version = 1;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DeviceType Type { get; private set; }
    public string? Platform { get; private set; }
    public DeviceStatus Status { get; private set; }
    public int SecurityVersion { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? PairedAtUtc { get; private set; }
    public DateTimeOffset? LastSeenAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public static Device Register(Guid id, Guid organizationId, Guid branchId, string name, DeviceType type, string? platform, DateTimeOffset now)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || branchId == Guid.Empty) throw new ArgumentException("Device identifiers are required.");
        return new Device(id, organizationId, branchId, name, type, platform, now);
    }

    public bool Update(Guid branchId, string name, DeviceType type, string? platform, DateTimeOffset now)
    {
        EnsureMutable();
        EnsureUtc(now);
        if (branchId == Guid.Empty) throw new ArgumentException("Branch is required.", nameof(branchId));
        string nextName = Required(name, 120, nameof(name));
        string? nextPlatform = Optional(platform, 120, nameof(platform));
        bool securityChanged = BranchId != branchId || Type != type || !string.Equals(Platform, nextPlatform, StringComparison.Ordinal);
        bool changed = securityChanged || !string.Equals(Name, nextName, StringComparison.Ordinal);
        if (!changed) return false;
        BranchId = branchId;
        Name = nextName;
        Type = type;
        Platform = nextPlatform;
        if (securityChanged)
        {
            SecurityVersion++;
            Status = DeviceStatus.PendingPairing;
            PairedAtUtc = null;
        }
        Advance(now);
        return securityChanged;
    }

    public void Pair(DateTimeOffset now)
    {
        EnsureMutable();
        EnsureUtc(now);
        SecurityVersion++;
        Status = DeviceStatus.Active;
        PairedAtUtc = now;
        LastSeenAtUtc = now;
        Advance(now);
    }

    public void RecordSeen(DateTimeOffset now)
    {
        EnsureUtc(now);
        if (Status != DeviceStatus.Active) return;
        LastSeenAtUtc = now;
    }

    public bool Revoke(DateTimeOffset now)
    {
        EnsureUtc(now);
        if (Status == DeviceStatus.Revoked) return false;
        Status = DeviceStatus.Revoked;
        RevokedAtUtc = now;
        SecurityVersion++;
        Advance(now);
        return true;
    }

    private void EnsureMutable()
    {
        if (Status == DeviceStatus.Revoked) throw new InvalidOperationException("Revoked devices cannot be changed.");
    }

    private void Advance(DateTimeOffset now) { Version++; UpdatedAtUtc = now; }
    private static string Required(string? value, int maximum, string name) { string v = value?.Trim() ?? string.Empty; return v.Length > 0 && v.Length <= maximum ? v : throw new ArgumentException($"{name} is invalid.", name); }
    private static string? Optional(string? value, int maximum, string name) => string.IsNullOrWhiteSpace(value) ? null : Required(value, maximum, name);
    private static void EnsureUtc(DateTimeOffset value) { if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC."); }
}
