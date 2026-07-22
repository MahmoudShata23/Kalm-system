namespace Kalm.Organization.Domain;

public sealed class DeviceCredential
{
    private DeviceCredential() { }
    private DeviceCredential(Guid id, Guid deviceId, string credentialHash, int securityVersion, DateTimeOffset now)
    { Id = id; DeviceId = deviceId; CredentialHash = credentialHash; SecurityVersion = securityVersion; IssuedAtUtc = now; }
    public Guid Id { get; private set; }
    public Guid DeviceId { get; private set; }
    public string CredentialHash { get; private set; } = string.Empty;
    public int SecurityVersion { get; private set; }
    public DateTimeOffset IssuedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public static DeviceCredential Issue(Guid id, Guid deviceId, string hash, int securityVersion, DateTimeOffset now)
    {
        if (id == Guid.Empty || deviceId == Guid.Empty || string.IsNullOrWhiteSpace(hash) || hash.Length > 128 || securityVersion < 1 || now.Offset != TimeSpan.Zero) throw new ArgumentException("Device credential is invalid.");
        return new(id, deviceId, hash, securityVersion, now);
    }
    public void Revoke(DateTimeOffset now) { if (RevokedAtUtc is null) RevokedAtUtc = now; }
}
